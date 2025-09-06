using TheKesslerRun2.Services.Interfaces;

namespace TheKesslerRun2.Services.Services;
internal abstract class BaseService : IHeartbeatReceiver
{
    private static readonly Type _messageReceiverType = typeof(IMessageReceiver<>);
    private double _accumulatedTime = 0;

    protected virtual double Threshold { get; } = 0.5;
    protected IMessageBus MessageBus { get; } = TypedMessageBus.Instance;

    protected abstract void OnTick();

    protected BaseService()
    {
        Game.Instance.HeartbeatService!.AddReceiver(this);
        SubscribeToAllMessageTypes();
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
    
    private void SubscribeToAllMessageTypes()
    {
        // Get all interfaces implemented by this class
        var interfaces = GetType().GetInterfaces();

        // Only consider IMessageReceiver<T>
        foreach (var iface in interfaces.Where(t => t.IsGenericType && t.GetGenericTypeDefinition() == _messageReceiverType))
        {
            // Use reflection to call TypedMessageBus.Subscribe<T>(this)
            var messageType = iface.GetGenericArguments()[0];
            var subscribeMethod = typeof(TypedMessageBus)
                .GetMethod("Subscribe")
                ?.MakeGenericMethod(messageType);

            subscribeMethod?.Invoke(MessageBus, [this]);
        }
    }
}
