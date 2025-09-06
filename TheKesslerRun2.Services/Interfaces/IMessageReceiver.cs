using TheKesslerRun2.Services.Messages;

namespace TheKesslerRun2.Services.Interfaces;
public interface IMessageReceiver<T>
{
    void Receive(T message);
}
