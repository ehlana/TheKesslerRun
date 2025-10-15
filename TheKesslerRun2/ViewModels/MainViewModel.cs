using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TheKesslerRun2.Services.Model;

namespace TheKesslerRun2.ViewModels;

public partial class MainViewModel : ObservableObject
{
    public event EventHandler? SaveRequested;
    public event EventHandler<SaveGameSummary>? GameSaved;
    public event EventHandler? ExitRequested;

    [RelayCommand]
    private void SaveGame() => SaveRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void ExitSession() => ExitRequested?.Invoke(this, EventArgs.Empty);

    public void NotifyGameSaved(SaveGameSummary summary)
        => GameSaved?.Invoke(this, summary);
}
