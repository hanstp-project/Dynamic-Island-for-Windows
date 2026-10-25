using System;
using System.Diagnostics;
using System.Windows.Threading;

namespace WindowsDynamicIsland.Services;

public enum TimerMode { Stopwatch, Countdown }

public sealed class TimerService
{
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _stopwatch = new();
    private TimeSpan _countdownInitial = TimeSpan.FromMinutes(5);
    private TimeSpan _countdownRemaining = TimeSpan.FromMinutes(5);

    public TimerMode Mode { get; private set; } = TimerMode.Stopwatch;
    public bool IsRunning => _stopwatch.IsRunning;
    public TimeSpan Elapsed => _stopwatch.Elapsed;
    public TimeSpan CountdownRemaining => _countdownRemaining;

    public event Action? Tick;
    public event Action? CountdownCompleted;

    public TimerService()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _timer.Tick += OnTick;
    }

    public void SetMode(TimerMode mode)
    {
        if (Mode == mode) return;
        Reset();
        Mode = mode;
        Tick?.Invoke();
    }

    public void Start()
    {
        if (!_stopwatch.IsRunning)
        {
            _stopwatch.Start();
            _timer.Start();
            Tick?.Invoke();
        }
    }

    public void Pause()
    {
        if (_stopwatch.IsRunning)
        {
            _stopwatch.Stop();
            _timer.Stop();
            Tick?.Invoke();
        }
    }

    public void Toggle()
    {
        if (IsRunning) Pause();
        else Start();
    }

    public void Reset()
    {
        _stopwatch.Reset();
        _timer.Stop();
        _countdownRemaining = _countdownInitial;
        Tick?.Invoke();
    }

    public void SetCountdownDuration(TimeSpan duration)
    {
        _countdownInitial = duration;
        if (!_stopwatch.IsRunning)
        {
            _countdownRemaining = duration;
            Tick?.Invoke();
        }
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (Mode == TimerMode.Countdown)
        {
            var remaining = _countdownInitial - _stopwatch.Elapsed;
            if (remaining <= TimeSpan.Zero)
            {
                _countdownRemaining = TimeSpan.Zero;
                _stopwatch.Reset();
                _timer.Stop();
                CountdownCompleted?.Invoke();
            }
            else
            {
                _countdownRemaining = remaining;
            }
        }
        Tick?.Invoke();
    }
}
