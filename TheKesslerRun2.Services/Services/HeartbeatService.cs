using TheKesslerRun2.Services.Interfaces;

namespace TheKesslerRun2.Services.Services;
public class HeartbeatService
{
    private readonly List<IHeartbeatReceiver> _receivers = [];

    public HeartbeatService(IHeartbeatProvider heartbeatProvider)
    {
        heartbeatProvider.Tick += (deltaSeconds) =>
        {
            foreach (var receiver in _receivers)
            {
                receiver.Tick(deltaSeconds);
            }
        };
    }

    public void AddReceiver(IHeartbeatReceiver receiver)
    {
        ArgumentNullException.ThrowIfNull(receiver);
        _receivers.Add(receiver);
    }

    public void RemoveReceiver(IHeartbeatReceiver receiver)
    {
        ArgumentNullException.ThrowIfNull(receiver);
        _receivers.Remove(receiver);
    }
}
