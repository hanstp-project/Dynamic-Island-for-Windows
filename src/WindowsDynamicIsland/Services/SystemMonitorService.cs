using System;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Microsoft.Win32;
using Windows.Networking.Connectivity;
using WindowsDynamicIsland.Interop;
using WindowsDynamicIsland.Models;

namespace WindowsDynamicIsland.Services;

/// <summary>
/// Local, lightweight system monitoring.
/// - CPU via GetSystemTimes deltas, memory via GlobalMemoryStatusEx, battery via GetSystemPowerStatus
///   (microsecond-cost P/Invoke calls; no PerformanceCounter/WMI overhead).
/// - Fast sampling (≈1.5 s) only runs while something is visible that needs it (<see cref="SetLiveSampling"/>).
/// - Battery and network changes are event-driven (PowerModeChanged, NetworkChange) so notifications
///   still work while the island is idle.
/// Nothing is stored or transmitted.
/// </summary>
public sealed class SystemMonitorService : IDisposable
{
    public static readonly TimeSpan LiveInterval = TimeSpan.FromMilliseconds(1500);
    private static readonly TimeSpan BatteryPollInterval = TimeSpan.FromSeconds(60);

    private readonly Dispatcher _dispatcher;
    private readonly CpuUsageCalculator _cpu = new();
    private readonly DispatcherTimer _liveTimer;
    private readonly DispatcherTimer _batteryTimer;
    private CancellationTokenSource? _networkDebounce;
    private bool _started, _disposed;
    private double _lastCpu;

    public SystemMetrics Latest { get; private set; }
    public BatteryStatus Battery { get; private set; } = BatteryStatus.Unavailable;
    public NetworkStatus Network { get; private set; } = NetworkStatus.Disconnected;

    /// <summary>Raised on the UI thread for every live sample.</summary>
    public event Action<SystemMetrics>? MetricsUpdated;
    /// <summary>Raised on the UI thread when battery state changes meaningfully.</summary>
    public event Action<BatteryStatus, BatteryStatus>? BatteryChanged;
    /// <summary>Raised on the UI thread when network state changes.</summary>
    public event Action<NetworkStatus, NetworkStatus>? NetworkChanged;

    public SystemMonitorService(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        _liveTimer = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = LiveInterval };
        _liveTimer.Tick += (_, _) => SampleNow();
        _batteryTimer = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = BatteryPollInterval };
        _batteryTimer.Tick += (_, _) => RefreshBattery();
    }

    /// <summary>Starts event subscriptions (battery + network). Cheap; no polling while idle on AC power.</summary>
    public void Start()
    {
        if (_started || _disposed) return;
        _started = true;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        NetworkChange.NetworkAvailabilityChanged += OnNetworkChanged;
        NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;
        RefreshBattery();
        _ = RefreshNetworkAsync();
    }

    public void Stop()
    {
        if (!_started) return;
        _started = false;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        NetworkChange.NetworkAvailabilityChanged -= OnNetworkChanged;
        NetworkChange.NetworkAddressChanged -= OnNetworkAddressChanged;
        _liveTimer.Stop();
        _batteryTimer.Stop();
        _networkDebounce?.Cancel();
        _cpu.Reset();
    }

    /// <summary>Enable ~1.5 s sampling (only while the System page is visible).</summary>
    public void SetLiveSampling(bool enabled)
    {
        if (!_started) enabled = false;
        if (enabled == _liveTimer.IsEnabled) return;
        if (enabled)
        {
            _cpu.Reset();
            SampleNow(); // primes the CPU calculator and publishes memory immediately
            _liveTimer.Start();
        }
        else
        {
            _liveTimer.Stop();
        }
    }

    public void SampleNow()
    {
        double cpu = _lastCpu;
        if (NativeMethods.GetSystemTimes(out var idle, out var kernel, out var user))
        {
            var value = _cpu.Next(idle.Value, kernel.Value, user.Value);
            if (value.HasValue) cpu = _lastCpu = value.Value;
        }

        var mem = new NativeMethods.MEMORYSTATUSEX { dwLength = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MEMORYSTATUSEX>() };
        double memPct = 0; ulong used = 0, total = 0;
        if (NativeMethods.GlobalMemoryStatusEx(ref mem))
        {
            total = mem.ullTotalPhys;
            used = total - mem.ullAvailPhys;
            memPct = total > 0 ? used * 100.0 / total : 0;
        }

        RefreshBattery();
        Latest = new SystemMetrics(cpu, memPct, used, total, Battery, Network, DateTimeOffset.Now);
        MetricsUpdated?.Invoke(Latest);
    }

    // ------------------------------------------------------------------ Battery

    public static BatteryStatus ReadBattery()
    {
        if (!NativeMethods.GetSystemPowerStatus(out var s)) return BatteryStatus.Unavailable;
        bool noBattery = (s.BatteryFlag & 128) != 0 || s.BatteryFlag == 255 || s.BatteryLifePercent == 255;
        if (noBattery) return BatteryStatus.Unavailable;
        bool plugged = s.ACLineStatus == 1;
        bool charging = (s.BatteryFlag & 8) != 0 || (plugged && s.BatteryLifePercent < 100);
        return new BatteryStatus(true, Math.Clamp((int)s.BatteryLifePercent, 0, 100), charging, plugged, s.SystemStatusFlag == 1);
    }

    private void RefreshBattery()
    {
        var next = ReadBattery();
        var prev = Battery;
        Battery = next;
        // Poll slowly only when discharging (for low-battery warnings); otherwise rely on events.
        bool needPoll = _started && next.HasBattery && !next.IsPluggedIn;
        if (needPoll && !_batteryTimer.IsEnabled) _batteryTimer.Start();
        else if (!needPoll && _batteryTimer.IsEnabled) _batteryTimer.Stop();
        if (next != prev) BatteryChanged?.Invoke(prev, next);
    }

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.StatusChange)
            _dispatcher.BeginInvoke(RefreshBattery, DispatcherPriority.Background);
        else if (e.Mode == PowerModes.Resume)
            _dispatcher.BeginInvoke(() => { _cpu.Reset(); RefreshBattery(); _ = RefreshNetworkAsync(); }, DispatcherPriority.Background);
    }

    // ------------------------------------------------------------------ Network

    private void OnNetworkChanged(object? sender, NetworkAvailabilityEventArgs e) => DebounceNetwork();
    private void OnNetworkAddressChanged(object? sender, EventArgs e) => DebounceNetwork();

    private void DebounceNetwork()
    {
        var cts = new CancellationTokenSource();
        var old = Interlocked.Exchange(ref _networkDebounce, cts);
        old?.Cancel();
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(1200, cts.Token).ConfigureAwait(false);
                await RefreshNetworkAsync().ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
        });
    }

    private async Task RefreshNetworkAsync()
    {
        var status = await Task.Run(ReadNetwork).ConfigureAwait(false);
        await _dispatcher.InvokeAsync(() =>
        {
            var prev = Network;
            Network = status;
            if (prev != status) NetworkChanged?.Invoke(prev, status);
        }, DispatcherPriority.Background);
    }

    public static NetworkStatus ReadNetwork()
    {
        try
        {
            var profile = NetworkInformation.GetInternetConnectionProfile();
            if (profile is null)
                return NetworkInterface.GetIsNetworkAvailable()
                    ? new NetworkStatus(NetworkKind.Other, NetworkConnectivity.LocalOnly, "")
                    : NetworkStatus.Disconnected;

            var level = profile.GetNetworkConnectivityLevel();
            var connectivity = level switch
            {
                NetworkConnectivityLevel.InternetAccess => NetworkConnectivity.Internet,
                NetworkConnectivityLevel.ConstrainedInternetAccess => NetworkConnectivity.Limited,
                NetworkConnectivityLevel.LocalAccess => NetworkConnectivity.LocalOnly,
                _ => NetworkConnectivity.None,
            };
            var kind = profile.IsWlanConnectionProfile ? NetworkKind.WiFi
                     : profile.IsWwanConnectionProfile ? NetworkKind.Cellular
                     : profile.NetworkAdapter?.IanaInterfaceType == 6 ? NetworkKind.Ethernet
                     : NetworkKind.Other;
            if (connectivity == NetworkConnectivity.None) kind = NetworkKind.None;
            return new NetworkStatus(kind, connectivity, profile.ProfileName ?? "");
        }
        catch (Exception ex)
        {
            Log.Debug("Network status query failed: " + ex.GetType().Name);
            try
            {
                return NetworkInterface.GetIsNetworkAvailable()
                    ? new NetworkStatus(NetworkKind.Other, NetworkConnectivity.LocalOnly, "")
                    : NetworkStatus.Disconnected;
            }
            catch { return NetworkStatus.Disconnected; }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        Stop();
        _disposed = true;
    }
}
