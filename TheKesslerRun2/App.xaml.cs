using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using TheKesslerRun2.Services;
using TheKesslerRun2.ViewModels;
using TheKesslerRun2.Views;

namespace TheKesslerRun2;

public partial class App : Application
{
    public static new App Current => (App)Application.Current;
    private IServiceProvider? ServiceProvider { get; set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        InitIoc();

        Game.Instance.StartGame(new WpfHeartbeatProvider());

        ServiceProvider!.GetRequiredService<MainWindow>().Show();
    }

    public T GetService<T>() where T : notnull => ServiceProvider!.GetRequiredService<T>();

    private void InitIoc()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        ServiceProvider = services.BuildServiceProvider();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<MainWindow>();
        services.AddSingleton<ScanView>();
        services.AddSingleton<DronesView>();

        services.AddSingleton<MainViewModel>();
        services.AddSingleton<ScanViewModel>();
        services.AddSingleton<DronesViewModel>();

        services.AddSingleton(TypedMessageBus.Instance);
    }
}
