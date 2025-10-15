using TheKesslerRun2.Services.Interfaces;

namespace TheKesslerRun2.Services;

public sealed class MessageBus : IMessageBus
{
    public static IMessageBus Instance { get; } = new MessageBus();
    private readonly Dictionary<Type, List<Delegate>> _subscribers = new();

    private MessageBus() {}

    public void Publish<T>(T message)
    {
        var type = typeof(T);
        if (!_subscribers.TryGetValue(type, out var list) || list.Count == 0)
            return;

        var snapshot = list.ToArray();
        foreach (var handler in snapshot)
        {
            if (handler is Action<T> typed)
            {
                typed(message);
            }
        }
    }

    public IDisposable Subscribe<T>(Action<T> handler)
    {
        var type = typeof(T);
        if (!_subscribers.TryGetValue(type, out var list))
        {
            list = new List<Delegate>();
            _subscribers[type] = list;
        }

        list.Add(handler);
        return new Subscription(this, type, handler);
    }

    private void Unsubscribe(Type type, Delegate handler)
    {
        if (!_subscribers.TryGetValue(type, out var list))
        {
            return;
        }

        list.Remove(handler);
        if (list.Count == 0)
        {
            _subscribers.Remove(type);
        }
    }

    private sealed class Subscription : IDisposable
    {
        private readonly MessageBus _owner;
        private readonly Type _messageType;
        private readonly Delegate _handler;
        private bool _disposed;

        public Subscription(MessageBus owner, Type messageType, Delegate handler)
        {
            _owner = owner;
            _messageType = messageType;
            _handler = handler;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _owner.Unsubscribe(_messageType, _handler);
            _disposed = true;
        }
    }
}
