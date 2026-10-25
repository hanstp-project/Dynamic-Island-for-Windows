using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace WindowsDynamicIsland.Services;

public enum LogLevel { Debug, Info, Warning, Error }

/// <summary>
/// Lightweight rotating file logger. Writes happen on a dedicated background thread so the
/// UI thread never blocks on disk I/O. Files rotate at <see cref="MaxFileBytes"/> and only
/// <see cref="MaxFiles"/> files are kept.
/// Privacy: callers must never log media titles, notification text or clipboard data.
/// </summary>
public sealed class LoggingService : IDisposable
{
    public const long MaxFileBytes = 1024 * 1024; // 1 MB
    public const int MaxFiles = 5;

    private readonly BlockingCollection<string> _queue = new(new ConcurrentQueue<string>(), 2000);
    private readonly Thread _writer;
    private readonly string _filePath;
    private bool _disposed;

    public string LogDirectory { get; }
    public LogLevel MinimumLevel { get; set; } = LogLevel.Info;

    public LoggingService(string logDirectory)
    {
        LogDirectory = logDirectory;
        Directory.CreateDirectory(logDirectory);
        _filePath = Path.Combine(logDirectory, "app.log");
        _writer = new Thread(WriterLoop) { IsBackground = true, Name = "LogWriter", Priority = ThreadPriority.BelowNormal };
        _writer.Start();
    }

    public void Write(LogLevel level, string message, Exception? ex = null)
    {
        if (level < MinimumLevel || _disposed) return;
        var sb = new StringBuilder(128);
        sb.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture))
          .Append(" [").Append(level.ToString().ToUpperInvariant()).Append("] ")
          .Append('[').Append(Environment.CurrentManagedThreadId).Append("] ")
          .Append(message);
        if (ex is not null) sb.AppendLine().Append(ex);
        // TryAdd: never block the caller if the disk is slow; drop instead.
        _queue.TryAdd(sb.ToString());
    }

    private void WriterLoop()
    {
        try
        {
            foreach (var line in _queue.GetConsumingEnumerable())
            {
                try
                {
                    RotateIfNeeded();
                    File.AppendAllText(_filePath, line + Environment.NewLine, Encoding.UTF8);
                }
                catch (IOException) { /* disk full / locked: logging must never crash the app */ }
                catch (UnauthorizedAccessException) { }
            }
        }
        catch (ObjectDisposedException) { }
    }

    private void RotateIfNeeded()
    {
        var info = new FileInfo(_filePath);
        if (!info.Exists || info.Length < MaxFileBytes) return;
        for (int i = MaxFiles - 1; i >= 1; i--)
        {
            var src = i == 1 ? _filePath : Path.Combine(LogDirectory, $"app.{i - 1}.log");
            var dst = Path.Combine(LogDirectory, $"app.{i}.log");
            if (!File.Exists(src)) continue;
            if (File.Exists(dst)) File.Delete(dst);
            File.Move(src, dst);
        }
    }

    /// <summary>Flushes pending entries (bounded wait) and stops the writer.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _queue.CompleteAdding();
        _writer.Join(TimeSpan.FromSeconds(2));
        _queue.Dispose();
    }
}

/// <summary>Static facade so any component can log without plumbing a dependency.</summary>
public static class Log
{
    private static LoggingService? _service;

    public static void Initialize(LoggingService service) => _service = service;
    public static string? Directory => _service?.LogDirectory;

    public static void Debug(string message) => _service?.Write(LogLevel.Debug, message);
    public static void Info(string message) => _service?.Write(LogLevel.Info, message);
    public static void Warn(string message, Exception? ex = null) => _service?.Write(LogLevel.Warning, message, ex);
    public static void Error(string message, Exception? ex = null) => _service?.Write(LogLevel.Error, message, ex);

    public static void Shutdown()
    {
        _service?.Dispose();
        _service = null;
    }
}
