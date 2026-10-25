using System;

namespace WindowsDynamicIsland.Models;

public enum NetworkKind { None, Ethernet, WiFi, Cellular, Other }
public enum NetworkConnectivity { None, LocalOnly, Limited, Internet }

/// <summary>Battery state. <see cref="HasBattery"/> is false on desktops without a battery.</summary>
public readonly record struct BatteryStatus(bool HasBattery, int Percent, bool IsCharging, bool IsPluggedIn, bool BatterySaver)
{
    public static readonly BatteryStatus Unavailable = new(false, 0, false, true, false);
}

public readonly record struct NetworkStatus(NetworkKind Kind, NetworkConnectivity Connectivity, string Name)
{
    public static readonly NetworkStatus Disconnected = new(NetworkKind.None, NetworkConnectivity.None, "");
    public bool IsConnected => Kind != NetworkKind.None && Connectivity != NetworkConnectivity.None;
}

/// <summary>A single sample of local system metrics. Never leaves the machine.</summary>
public readonly record struct SystemMetrics(
    double CpuPercent,
    double MemoryPercent,
    ulong MemoryUsedBytes,
    ulong MemoryTotalBytes,
    BatteryStatus Battery,
    NetworkStatus Network,
    DateTimeOffset Timestamp);

/// <summary>
/// Converts successive GetSystemTimes samples into CPU utilisation.
/// Kernel time reported by Windows includes idle time.
/// </summary>
public sealed class CpuUsageCalculator
{
    private ulong _lastIdle, _lastKernel, _lastUser;
    private bool _hasSample;

    /// <returns>CPU usage 0..100, or null for the very first sample.</returns>
    public double? Next(ulong idle, ulong kernel, ulong user)
    {
        if (!_hasSample)
        {
            (_lastIdle, _lastKernel, _lastUser, _hasSample) = (idle, kernel, user, true);
            return null;
        }
        ulong dIdle = idle >= _lastIdle ? idle - _lastIdle : 0;
        ulong dKernel = kernel >= _lastKernel ? kernel - _lastKernel : 0;
        ulong dUser = user >= _lastUser ? user - _lastUser : 0;
        (_lastIdle, _lastKernel, _lastUser) = (idle, kernel, user);

        ulong total = dKernel + dUser;
        if (total == 0) return 0;
        ulong busy = total > dIdle ? total - dIdle : 0;
        return Math.Clamp(busy * 100.0 / total, 0, 100);
    }

    public void Reset() => _hasSample = false;
}
