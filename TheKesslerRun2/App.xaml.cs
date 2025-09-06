using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using TheKesslerRun2.Services;
using TheKesslerRun2.ViewModels;

namespace TheKesslerRun2;

public partial class App : Application
{
    public static new App Current => (App)Application.Current;
    public IServiceProvider? ServiceProvider {get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        InitIoc();

        Game.Instance.StartGame(new WpfHeartbeatProvider());

        ServiceProvider!.GetRequiredService<MainWindow>().Show();
    }

    private void InitIoc()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        ServiceProvider = services.BuildServiceProvider();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<MainWindow>();
        services.AddSingleton<MainViewModel>();

        services.AddSingleton<ScanViewModel>();

        services.AddSingleton(TypedMessageBus.Instance);
    }
}
