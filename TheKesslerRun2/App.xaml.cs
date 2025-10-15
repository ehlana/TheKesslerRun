using Microsoft.Extensions.DependencyInjection;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Threading;
using TheKesslerRun2.Services;
using TheKesslerRun2.Services.Model;
using TheKesslerRun2.Services.Services;
using TheKesslerRun2.ViewModels;
using TheKesslerRun2.Views;

namespace TheKesslerRun2;

public partial class App : Application
{
    public new static App Current => (App)Application.Current;
    private IServiceProvider? ServiceProvider { get; set; }
    private WpfHeartbeatProvider? _heartbeatProvider;
    private DispatcherTimer? _autoSaveTimer;
    private AutoSaveSettings _autoSaveSettings = new();
    private int _nextAutoSaveSlot;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

        InitIoc();

        if (!Game.Instance.VerifyOrbpak())
        {
            MessageBox.Show("The data.orbpak file is corrupted or missing.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
            return;
        }

        GetService<StartupWindow>().Show();
    }

    public T GetService<T>() where T : notnull => ServiceProvider!.GetRequiredService<T>();

    public void BeginSession(SaveGameData? state)
    {
        _heartbeatProvider ??= new WpfHeartbeatProvider();
        Game.Instance.StartGame(_heartbeatProvider);

        var mainWindow = GetService<MainWindow>();
        var saveService = GetService<ISaveGameService>();
        saveService.ConfigureProviders(mainWindow.CaptureLayout, mainWindow.GetWindowMetrics);

        if (state is not null)
        {
            saveService.ApplyGameState(state);
        }

        if (state?.WindowWidth > 0)
        {
            mainWindow.Width = state.WindowWidth;
        }

        if (state?.WindowHeight > 0)
        {
            mainWindow.Height = state.WindowHeight;
        }

        mainWindow.RestoreLayout(state?.Layout);

        var startupWindow = GetService<StartupWindow>();
        if (startupWindow.IsVisible)
        {
            startupWindow.Hide();
        }

        Application.Current.MainWindow = mainWindow;

        if (!mainWindow.IsVisible)
        {
            mainWindow.Show();
        }
        else
        {
            mainWindow.Activate();
        }

        GetService<StartupViewModel>().SaveEnabled = true;

        _autoSaveSettings = SettingsManager.Instance.Settings.AutoSave;
        SetupAutoSaveTimer();
    }

    public void EndSession()
    {
        PauseGameLoops();

        var mainWindow = GetService<MainWindow>();
        if (mainWindow.IsVisible)
        {
            mainWindow.Hide();
        }

        var startupViewModel = GetService<StartupViewModel>();
        startupViewModel.SaveEnabled = false;
        startupViewModel.RefreshSavesCommand.Execute(null);

        var startupWindow = GetService<StartupWindow>();
        if (!startupWindow.IsVisible)
        {
            startupWindow.Show();
        }

        startupWindow.WindowState = WindowState.Normal;

        Application.Current.MainWindow = startupWindow;
        startupWindow.Activate();
        startupWindow.Focus();
    }

    public void PauseGameLoops()
    {
        _heartbeatProvider?.Pause();
        _autoSaveTimer?.Stop();
    }

    public void ResumeGameLoops()
    {
        _heartbeatProvider?.Resume();
        if (_autoSaveTimer is not null && _autoSaveSettings.IntervalMinutes > 0)
        {
            _autoSaveTimer.Start();
        }
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
        services.AddSingleton<StartupWindow>();
        services.AddSingleton<ScanView>();
        services.AddSingleton<DronesView>();
        services.AddSingleton<RecyclingCentreView>();

        services.AddSingleton<MainViewModel>();
        services.AddSingleton<StartupViewModel>();
        services.AddSingleton<ScanViewModel>();
        services.AddSingleton<DronesViewModel>();
        services.AddSingleton<RecyclingCentreViewModel>();

        services.AddSingleton<ISaveGameService, SaveGameService>();
        services.AddSingleton(MessageBus.Instance);
    }

    private void SetupAutoSaveTimer()
    {
        if (_autoSaveTimer is not null)
        {
            _autoSaveTimer.Tick -= HandleAutoSaveTick;
            _autoSaveTimer.Stop();
        }

        if (_autoSaveSettings.IntervalMinutes <= 0)
        {
            _autoSaveTimer = null;
            return;
        }

        _nextAutoSaveSlot = 0;

        _autoSaveTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(_autoSaveSettings.IntervalMinutes)
        };
        _autoSaveTimer.Tick += HandleAutoSaveTick;
        _autoSaveTimer.Start();
    }

    private void HandleAutoSaveTick(object? sender, EventArgs e) => PerformAutoSave();

    private void PerformAutoSave()
    {
        if (_autoSaveSettings.IntervalMinutes <= 0)
        {
            return;
        }

        try
        {
            int slotCount = Math.Max(1, _autoSaveSettings.SlotCount);
            int slotIndex = _nextAutoSaveSlot % slotCount;
            string name = $"Auto Save {slotIndex + 1}";
            var saveService = GetService<ISaveGameService>();
            saveService.SaveAutoGame(slotIndex, name);
            _nextAutoSaveSlot = (slotIndex + 1) % slotCount;

            GetService<StartupViewModel>().RefreshSavesCommand.Execute(null);
        }
        catch
        {
            // Ignore auto-save failures
        }
    }
}
