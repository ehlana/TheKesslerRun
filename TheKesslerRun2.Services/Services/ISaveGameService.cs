using System;
using System.Collections.Generic;
using TheKesslerRun2.Services.Model;

namespace TheKesslerRun2.Services.Services;

public interface ISaveGameService
{
    void ConfigureProviders(Func<string?> layoutProvider, Func<(double Width, double Height)> metricsProvider);
    IReadOnlyList<SaveGameSummary> GetSaveGames();
    SaveGameSummary SaveGame(string name, string? layoutXml = null, double? windowWidth = null, double? windowHeight = null, bool isAutoSave = false, string? targetFilePath = null);
    SaveGameSummary SaveAutoGame(int slotIndex, string name);
    SaveGameData LoadGame(string filePath);
    void ApplyGameState(SaveGameData state);
}

