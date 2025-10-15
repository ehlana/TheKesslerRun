namespace TheKesslerRun2.Services.Interfaces;
public interface IMessageBus
{
    void Publish<T>(T message);
    IDisposable Subscribe<T>(Action<T> receiver);
}
