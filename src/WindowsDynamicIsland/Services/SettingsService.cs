using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using WindowsDynamicIsland.Models;

namespace WindowsDynamicIsland.Services;

/// <summary>
/// Loads, validates and persists <see cref="AppSettings"/> as JSON.
/// - Corrupted files are renamed to <c>settings.corrupt-*.json</c> (kept for diagnostics) and defaults are used.
/// - Saves are debounced and written atomically (temp file + replace) off the UI thread.
/// </summary>
public sealed class SettingsService : IDisposable
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _filePath;
    private readonly SemaphoreSlim _ioLock = new(1, 1);
    private readonly TimeSpan _debounce;
    private CancellationTokenSource? _pendingSave;

    public AppSettings Current { get; private set; } = new();
    public string FilePath => _filePath;
    public string Directory { get; }

    /// <summary>Set when the last load found a corrupted file (path of the preserved copy).</summary>
    public string? RecoveredCorruptFile { get; private set; }

    /// <summary>Raised on the caller's thread after a setting changed. Argument = property name (or null for "all").</summary>
    public event Action<string?>? Changed;

    public SettingsService(string directory, TimeSpan? debounce = null)
    {
        Directory = directory;
        System.IO.Directory.CreateDirectory(directory);
        _filePath = Path.Combine(directory, "settings.json");
        _debounce = debounce ?? TimeSpan.FromMilliseconds(400);
    }

    /// <summary>Synchronous load used once at startup, before any window exists.</summary>
    public AppSettings Load()
    {
        RecoveredCorruptFile = null;
        AppSettings settings;
        if (!File.Exists(_filePath))
        {
            settings = new AppSettings();
            Log.Info("No settings file found – using defaults.");
        }
        else
        {
            try
            {
                var json = File.ReadAllText(_filePath);
                settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions)
                           ?? throw new JsonException("Settings file deserialised to null.");
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException or IOException or UnauthorizedAccessException)
            {
                Log.Warn("Settings file is unreadable or corrupted – restoring defaults.", ex);
                RecoveredCorruptFile = PreserveCorruptFile();
                settings = new AppSettings();
            }
        }

        if (settings.Validate())
            Log.Info("Some settings were out of range and have been corrected.");

        Current = settings;
        return settings;
    }

    private string? PreserveCorruptFile()
    {
        try
        {
            var dest = Path.Combine(Directory, $"settings.corrupt-{DateTime.Now:yyyyMMdd-HHmmss}.json");
            File.Move(_filePath, dest, overwrite: true);
            return dest;
        }
        catch (Exception ex)
        {
            Log.Warn("Could not preserve corrupted settings file.", ex);
            return null;
        }
    }

    /// <summary>Notify listeners that a value changed and schedule a debounced save.</summary>
    public void NotifyChanged(string? propertyName)
    {
        Current.Validate();
        Changed?.Invoke(propertyName);
        ScheduleSave();
    }

    public void ScheduleSave()
    {
        var cts = new CancellationTokenSource();
        var previous = Interlocked.Exchange(ref _pendingSave, cts);
        previous?.Cancel();
        previous?.Dispose();
        var snapshot = Current.Clone();
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(_debounce, cts.Token).ConfigureAwait(false);
                await SaveAsync(snapshot).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
        });
    }

    /// <summary>Writes settings atomically. Safe to call from any thread.</summary>
    public async Task SaveAsync(AppSettings? settings = null)
    {
        settings ??= Current.Clone();
        await _ioLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var tmp = _filePath + ".tmp";
            await using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
            {
                await JsonSerializer.SerializeAsync(fs, settings, JsonOptions).ConfigureAwait(false);
            }
            File.Move(tmp, _filePath, overwrite: true);
        }
        catch (Exception ex)
        {
            Log.Error("Failed to save settings.", ex);
        }
        finally
        {
            _ioLock.Release();
        }
    }

    /// <summary>Cancels a pending debounced save and writes immediately (used during shutdown).</summary>
    public void FlushSync()
    {
        var pending = Interlocked.Exchange(ref _pendingSave, null);
        if (pending is null) return;
        pending.Cancel();
        pending.Dispose();
        try { SaveAsync().Wait(TimeSpan.FromSeconds(2)); }
        catch (Exception ex) { Log.Error("Failed to flush settings.", ex); }
    }

    public void Dispose()
    {
        FlushSync();
        _ioLock.Dispose();
    }
}
