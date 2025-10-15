using System;
using System.Windows.Threading;
using TheKesslerRun2.Services.Interfaces;

namespace TheKesslerRun2;
public class WpfHeartbeatProvider : IHeartbeatProvider
{
    private readonly DispatcherTimer _timer;
    private DateTime _lastTick;
    private bool _isPaused;
    public event Action<double>? Tick;

    public WpfHeartbeatProvider(int intervalMs = 100)
    {
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(intervalMs)
        };
        _lastTick = DateTime.Now;
        _timer.Tick += (s, e) =>
        {
            var now = DateTime.Now;
            var delta = (now - _lastTick).TotalSeconds;
            _lastTick = now;
            if (!_isPaused)
            {
                Tick?.Invoke(delta);
            }
        };
        _timer.Start();
    }

    public void Pause()
    {
        if (_isPaused)
        {
            return;
        }

        _isPaused = true;
        _timer.Stop();
    }

    public void Resume()
    {
        if (!_isPaused)
        {
            return;
        }

        _lastTick = DateTime.Now;
        _isPaused = false;
        _timer.Start();
    }

    public bool IsPaused => _isPaused;
}
