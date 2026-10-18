using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Threading;

namespace DynamicIsland.Services;

public class StopwatchService
{
    private readonly Stopwatch _stopwatch = new();
    private readonly DispatcherTimer _timer = new();

    // Countdown timer support
    private TimeSpan _countdownTotal = TimeSpan.Zero;
    private TimeSpan _countdownRemaining = TimeSpan.Zero;
    private bool _isCountdownActive = false;

    public event Action<string, bool>? TimeUpdated; // formatted string, isRunning
    public event Action? CountdownCompleted;

    public ObservableCollection<string> Laps { get; } = new();

    public bool IsRunning => _stopwatch.IsRunning;
    public bool IsCountdownRunning => _isCountdownActive && _timer.IsEnabled;
    public TimeSpan Elapsed => _stopwatch.Elapsed;
    public TimeSpan CountdownRemaining => _countdownRemaining;

    public StopwatchService()
    {
        _timer.Interval = TimeSpan.FromMilliseconds(50);
        _timer.Tick += Timer_Tick;
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (_isCountdownActive)
        {
            var remaining = _countdownRemaining - _stopwatch.Elapsed;
            if (remaining <= TimeSpan.Zero)
            {
                remaining = TimeSpan.Zero;
                _stopwatch.Stop();
                _timer.Stop();
                _isCountdownActive = false;
                TimeUpdated?.Invoke("00:00.0", false);
                CountdownCompleted?.Invoke();
            }
            else
            {
                var formatted = $"{(int)remaining.TotalMinutes:00}:{remaining.Seconds:00}.{remaining.Milliseconds / 100:0}";
                TimeUpdated?.Invoke(formatted, true);
            }
        }
        else if (_stopwatch.IsRunning)
        {
            var elapsed = _stopwatch.Elapsed;
            var formatted = $"{(int)elapsed.TotalMinutes:00}:{elapsed.Seconds:00}.{elapsed.Milliseconds / 100:0}";
            TimeUpdated?.Invoke(formatted, true);
        }
    }

    // --- Stopwatch controls ---
    public void StartStopwatch()
    {
        _isCountdownActive = false;
        _stopwatch.Start();
        _timer.Start();
        TimeUpdated?.Invoke(GetStopwatchString(), true);
    }

    public void PauseStopwatch()
    {
        _stopwatch.Stop();
        _timer.Stop();
        TimeUpdated?.Invoke(GetStopwatchString(), false);
    }

    public void ResetStopwatch()
    {
        _stopwatch.Reset();
        _timer.Stop();
        _isCountdownActive = false;
        Laps.Clear();
        TimeUpdated?.Invoke("00:00.0", false);
    }

    public void AddLap()
    {
        if (_stopwatch.IsRunning)
        {
            var lapNum = Laps.Count + 1;
            Laps.Insert(0, $"Tur {lapNum}: {GetStopwatchString()}");
        }
    }

    // --- Countdown controls ---
    public void StartCountdown(TimeSpan duration)
    {
        _stopwatch.Reset();
        _countdownTotal = duration;
        _countdownRemaining = duration;
        _isCountdownActive = true;
        _stopwatch.Start();
        _timer.Start();

        var formatted = $"{(int)duration.TotalMinutes:00}:{duration.Seconds:00}.0";
        TimeUpdated?.Invoke(formatted, true);
    }

    public void PauseCountdown()
    {
        if (_isCountdownActive)
        {
            _countdownRemaining -= _stopwatch.Elapsed;
            _stopwatch.Reset();
            _timer.Stop();
            var formatted = $"{(int)_countdownRemaining.TotalMinutes:00}:{_countdownRemaining.Seconds:00}.{_countdownRemaining.Milliseconds / 100:0}";
            TimeUpdated?.Invoke(formatted, false);
        }
    }

    public void ResetCountdown()
    {
        _stopwatch.Reset();
        _timer.Stop();
        _isCountdownActive = false;
        _countdownRemaining = TimeSpan.Zero;
        TimeUpdated?.Invoke("00:00.0", false);
    }

    public string GetStopwatchString()
    {
        var elapsed = _stopwatch.Elapsed;
        return $"{(int)elapsed.TotalMinutes:00}:{elapsed.Seconds:00}.{elapsed.Milliseconds / 100:0}";
    }
}
