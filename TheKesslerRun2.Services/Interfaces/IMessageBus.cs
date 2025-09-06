namespace TheKesslerRun2.Services.Interfaces;
public interface IMessageBus
{
    void Publish<T>(T message);
    void Subscribe<T>(IMessageReceiver<T> receiver);
}
