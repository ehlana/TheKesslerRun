using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TheKesslerRun2.Services.Model;
using TheKesslerRun2.Services.Services;

namespace TheKesslerRun2.ViewModels;

public partial class StartupViewModel : ObservableObject
{
    private readonly ISaveGameService _saveGameService;

    public event EventHandler? NewGameRequested;
    public event EventHandler<SaveGameSummary>? LoadGameRequested;
    public event EventHandler<SaveGameSummary?>? SaveGameRequested;

    public ObservableCollection<SaveGameSummary> Saves { get; } = new();

    [ObservableProperty]
    private SaveGameSummary? _selectedSave;

    [ObservableProperty]
    private bool _saveEnabled;

    public StartupViewModel(ISaveGameService saveGameService)
    {
        _saveGameService = saveGameService;
        RefreshSaves();
    }

    [RelayCommand]
    private void RefreshSaves()
    {
        Saves.Clear();
        foreach (var save in _saveGameService.GetSaveGames())
        {
            Saves.Add(save);
        }

        var firstManual = Saves.FirstOrDefault(s => !s.IsAutoSave);
        SelectedSave = firstManual ?? Saves.FirstOrDefault();

        LoadGameCommand.NotifyCanExecuteChanged();
        SaveGameCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void NewGame() => NewGameRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand(CanExecute = nameof(CanExecuteLoad))]
    private void LoadGame()
    {
        if (SelectedSave is null)
        {
            return;
        }

        LoadGameRequested?.Invoke(this, SelectedSave);
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void SaveGame()
    {
        SaveGameRequested?.Invoke(this, SelectedSave);
    }


    private bool CanExecuteLoad() => SelectedSave is not null;

    private bool CanSave() => SaveEnabled;

    partial void OnSelectedSaveChanged(SaveGameSummary? value)
    {
        LoadGameCommand.NotifyCanExecuteChanged();
        SaveGameCommand.NotifyCanExecuteChanged();
    }

    partial void OnSaveEnabledChanged(bool value) => SaveGameCommand.NotifyCanExecuteChanged();
}




