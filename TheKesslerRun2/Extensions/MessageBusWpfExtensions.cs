using System.Windows;
using TheKesslerRun2.Services.Interfaces;

namespace TheKesslerRun2.Extensions;
public static class MessageBusWpfExtensions
{
    public static IDisposable SubscribeOnUI<T>(
        this IMessageBus bus,
        Action<T> handler)
    {
        var dispatcher = Application.Current.Dispatcher;
        return bus.Subscribe<T>(msg =>
        {
            if (dispatcher.CheckAccess())
                handler(msg);
            else
                dispatcher.BeginInvoke(() => handler(msg));
        });
    }
}
