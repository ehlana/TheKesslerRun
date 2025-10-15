using TheKesslerRun2.Services.Interfaces;

namespace TheKesslerRun2.Services.Services;
internal abstract class BaseService : IHeartbeatReceiver
{
    private double _accumulatedTime = 0;

    protected virtual double Threshold { get; } = 0.5;

    protected abstract void OnTick();

    protected BaseService()
    {
        Game.Instance.HeartbeatService!.AddReceiver(this);
        SubscribeToMessages();
    }

    public void Tick(double deltaSeconds)
    {
        _accumulatedTime += deltaSeconds;
        if (_accumulatedTime >= Threshold)
        {
            _accumulatedTime -= Threshold;
            OnTick();
        }
    }

    protected virtual void SubscribeToMessages()
    {

    }
}
