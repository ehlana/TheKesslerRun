using System;
using System.ComponentModel;
using System.Windows;
using TheKesslerRun2.Services.Model;
using TheKesslerRun2.Services.Services;
using TheKesslerRun2.ViewModels;

namespace TheKesslerRun2;

public partial class StartupWindow : Window
{
    private readonly ISaveGameService _saveGameService;
    private readonly StartupViewModel _viewModel;

    public StartupWindow(StartupViewModel viewModel, ISaveGameService saveGameService)
    {
        InitializeComponent();
        _saveGameService = saveGameService;
        _viewModel = viewModel;
        DataContext = _viewModel;

        _viewModel.NewGameRequested += OnNewGameRequested;
        _viewModel.LoadGameRequested += OnLoadGameRequested;
        _viewModel.SaveGameRequested += OnSaveGameRequested;

        Closed += OnClosed;
        Closing += OnClosing;

        Loaded += (_, _) => _viewModel.RefreshSavesCommand.Execute(null);
    }

    private void OnNewGameRequested(object? sender, EventArgs e)
    {
        App.Current.BeginSession(null);
        Hide();
    }

    private void OnLoadGameRequested(object? sender, SaveGameSummary summary)
    {
        try
        {
            var state = _saveGameService.LoadGame(summary.FilePath);
            App.Current.BeginSession(state);
            Hide();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Failed to load save: {ex.Message}", "Load Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            _viewModel.RefreshSavesCommand.Execute(null);
        }
    }

    private void OnSaveGameRequested(object? sender, SaveGameSummary? summary)
    {
        var mainViewModel = App.Current.GetService<MainViewModel>();
        mainViewModel.SaveGameCommand.Execute(null);
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
        {
            return;
        }

        App.Current.Shutdown();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _viewModel.NewGameRequested -= OnNewGameRequested;
        _viewModel.LoadGameRequested -= OnLoadGameRequested;
        _viewModel.SaveGameRequested -= OnSaveGameRequested;
        Closing -= OnClosing;
    }
}
