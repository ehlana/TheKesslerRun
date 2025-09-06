using TheKesslerRun2.Services.Interfaces;

namespace TheKesslerRun2.Services;
public class TypedMessageBus : IMessageBus
{
    public static IMessageBus Instance { get; } = new TypedMessageBus();

    private TypedMessageBus() { }

    private readonly Dictionary<Type, List<WeakReference>> _subscribers = new();

    public void Publish<T>(T message)
    {
        var type = typeof(T);
        if (_subscribers.TryGetValue(type, out var list))
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var weakRef = list[i];
                if (weakRef.Target is IMessageReceiver<T> target)
                    target.Receive(message);
                else
                    list.RemoveAt(i);
            }
        }
    }

    public void Subscribe<T>(IMessageReceiver<T> receiver)
    {
        var type = typeof(T);
        if (!_subscribers.TryGetValue(type, out var list))
            _subscribers[type] = list = new List<WeakReference>();

        list.Add(new WeakReference(receiver));
    }
}
