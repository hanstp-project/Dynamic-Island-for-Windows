using System;
using System.IO;
using Microsoft.Win32;

namespace WindowsDynamicIsland.Services;

/// <summary>
/// Per-user "start with Windows" registration via
/// <c>HKCU\Software\Microsoft\Windows\CurrentVersion\Run</c>.
/// - No administrator rights required.
/// - A single fixed value name means re-registering can never create duplicates.
/// - The value points at the stable installed executable path and passes <c>--startup</c>.
/// - The app never relaunches itself, so a crash cannot cause a startup loop.
/// - The uninstaller deletes the same value.
/// </summary>
public sealed class StartupService
{
    public const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public const string ValueName = "WindowsDynamicIsland";
    public const string StartupArgument = "--startup";

    private readonly string _executablePath;
    private readonly string _arguments;
    private readonly string _valueName;

    public StartupService(string? executablePath = null, string valueName = ValueName)
    {
        _valueName = valueName;
        var proc = Environment.ProcessPath ?? "";
        if (executablePath != null)
        {
            _executablePath = executablePath;
            _arguments = StartupArgument;
        }
        else if (proc.EndsWith("dotnet.exe", StringComparison.OrdinalIgnoreCase))
        {
            var dllPath = Path.Combine(AppContext.BaseDirectory, "WindowsDynamicIsland.dll");
            _executablePath = proc;
            _arguments = $"\"{dllPath}\" {StartupArgument}";
        }
        else
        {
            _executablePath = proc;
            _arguments = StartupArgument;
        }
    }

    public string ExpectedCommand => string.IsNullOrWhiteSpace(_arguments)
        ? $"\"{_executablePath}\" {StartupArgument}"
        : $"\"{_executablePath}\" {_arguments}";

    internal static string BuildCommand(string exePath) => $"\"{exePath}\" {StartupArgument}";

    public bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            return key?.GetValue(_valueName) is string s && !string.IsNullOrWhiteSpace(s);
        }
        catch (Exception ex)
        {
            Log.Warn("Could not read startup registration.", ex);
            return false;
        }
    }

    /// <summary>True when registered but pointing at a different (e.g. moved/old) executable.</summary>
    public bool IsStale()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            return key?.GetValue(_valueName) is string s
                   && !string.Equals(s, ExpectedCommand, StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    /// <returns>True on success. Failures are logged and reported, never swallowed silently.</returns>
    public bool SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            if (enabled)
            {
                if (!File.Exists(_executablePath))
                {
                    Log.Warn("Refusing to register startup: executable path does not exist.");
                    return false;
                }
                key.SetValue(_valueName, ExpectedCommand, RegistryValueKind.String);
                Log.Info("Startup registration enabled.");
            }
            else
            {
                key.DeleteValue(_valueName, throwOnMissingValue: false);
                Log.Info("Startup registration removed.");
            }
            return true;
        }
        catch (Exception ex)
        {
            Log.Error("Failed to change startup registration.", ex);
            return false;
        }
    }

    /// <summary>If the user enabled startup and the exe moved (e.g. upgrade), repoint the entry.</summary>
    public void RepairIfStale()
    {
        if (IsEnabled() && IsStale() && IsInstalledLocation())
        {
            Log.Info("Startup entry points to an old path – updating.");
            SetEnabled(true);
        }
    }

    /// <summary>Avoids registering transient paths such as a build output folder by accident during repair.</summary>
    private bool IsInstalledLocation()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        return _executablePath.StartsWith(local, StringComparison.OrdinalIgnoreCase)
            || _executablePath.StartsWith(programFiles, StringComparison.OrdinalIgnoreCase)
            || File.Exists(_executablePath);
    }
}
