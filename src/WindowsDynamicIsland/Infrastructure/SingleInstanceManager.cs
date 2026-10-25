using System;
using System.Threading;
using WindowsDynamicIsland.Services;

namespace WindowsDynamicIsland.Infrastructure;

/// <summary>
/// Guarantees one instance per user session. A second launch signals the running instance
/// ("show settings" or "shut down") through named events and then exits.
/// </summary>
public sealed class SingleInstanceManager : IDisposable
{
    private const string Prefix = @"Local\WindowsDynamicIsland.7F3C2A91";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activateEvent;
    private readonly EventWaitHandle _shutdownEvent;
    private RegisteredWaitHandle? _activateWait;
    private RegisteredWaitHandle? _shutdownWait;

    public bool IsPrimary { get; }

    public SingleInstanceManager()
    {
        _mutex = new Mutex(initiallyOwned: true, Prefix + ".Mutex", out bool createdNew);
        IsPrimary = createdNew;
        _activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, Prefix + ".Activate");
        _shutdownEvent = new EventWaitHandle(false, EventResetMode.AutoReset, Prefix + ".Shutdown");
        if (IsPrimary)
        {
            _activateEvent.Reset();
            _shutdownEvent.Reset();
        }
    }

    /// <summary>Primary instance: start listening. Callbacks run on a thread-pool thread.</summary>
    public void Listen(Action onActivate, Action onShutdown)
    {
        _activateWait = ThreadPool.RegisterWaitForSingleObject(_activateEvent,
            (_, _) => Safe(onActivate), null, Timeout.Infinite, executeOnlyOnce: false);
        _shutdownWait = ThreadPool.RegisterWaitForSingleObject(_shutdownEvent,
            (_, _) => Safe(onShutdown), null, Timeout.Infinite, executeOnlyOnce: false);
    }

    public void SignalActivate() => _activateEvent.Set();
    public void SignalShutdown() => _shutdownEvent.Set();

    /// <summary>
    /// Used by "--shutdown" (installer/uninstaller). Signals the running instance and waits
    /// until it releases the mutex. Returns true if no instance is running afterwards.
    /// </summary>
    public static bool RequestShutdownOfRunningInstance(TimeSpan timeout)
    {
        try
        {
            if (!Mutex.TryOpenExisting(Prefix + ".Mutex", out var mutex)) return true;
            using (mutex)
            {
                if (EventWaitHandle.TryOpenExisting(Prefix + ".Shutdown", out var evt))
                    using (evt) evt.Set();
                try
                {
                    if (mutex.WaitOne(timeout)) { mutex.ReleaseMutex(); return true; }
                }
                catch (AbandonedMutexException) { return true; }
                return false;
            }
        }
        catch (Exception) { return false; }
    }

    private static void Safe(Action action)
    {
        try { action(); }
        catch (Exception ex) { Log.Error("Single-instance callback failed.", ex); }
    }

    public void Dispose()
    {
        _activateWait?.Unregister(null);
        _shutdownWait?.Unregister(null);
        if (IsPrimary)
        {
            try { _mutex.ReleaseMutex(); } catch (ApplicationException) { }
        }
        _mutex.Dispose();
        _activateEvent.Dispose();
        _shutdownEvent.Dispose();
    }
}
