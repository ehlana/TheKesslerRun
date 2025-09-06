using System.Windows.Threading;
using TheKesslerRun2.Services.Interfaces;

namespace TheKesslerRun2;
public class WpfHeartbeatProvider : IHeartbeatProvider
{
    private DispatcherTimer _timer;
    private DateTime _lastTick;
    public event Action<double>? Tick;

    public WpfHeartbeatProvider(int intervalMs = 250)
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
            Tick?.Invoke(delta);
        };
        _timer.Start();
    }
}
