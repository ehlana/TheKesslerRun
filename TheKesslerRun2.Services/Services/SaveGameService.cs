using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using TheKesslerRun2.Services.Model;

namespace TheKesslerRun2.Services.Services;

public sealed class SaveGameService : ISaveGameService
{
    private const int CurrentVersion = 1;
    private const string AutoSaveFilePrefix = "autosave_";

    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    private readonly ResourceFieldService _fieldService = ResourceFieldService.Instance;
    private readonly string _saveDirectory;

    private Func<string?>? _layoutProvider;
    private Func<(double Width, double Height)>? _metricsProvider;

    private DronesService DronesService => Game.Instance.GetService<DronesService>();
    private RecyclingCentreService RecyclingService => Game.Instance.GetService<RecyclingCentreService>();

    public SaveGameService()
    {
        var basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TheKesslerRun2", "Saves");
        Directory.CreateDirectory(basePath);
        _saveDirectory = basePath;
    }

    public void ConfigureProviders(Func<string?> layoutProvider, Func<(double Width, double Height)> metricsProvider)
    {
        _layoutProvider = layoutProvider;
        _metricsProvider = metricsProvider;
    }

    public IReadOnlyList<SaveGameSummary> GetSaveGames()
    {
        if (!Directory.Exists(_saveDirectory))
        {
            return Array.Empty<SaveGameSummary>();
        }

        var summaries = new List<SaveGameSummary>();
        foreach (var file in Directory.EnumerateFiles(_saveDirectory, "*.json"))
        {
            try
            {
                using var stream = File.OpenRead(file);
                using var document = JsonDocument.Parse(stream);
                var root = document.RootElement;

                string? name = root.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
                if (string.IsNullOrWhiteSpace(name))
                {
                    name = Path.GetFileNameWithoutExtension(file);
                }

                DateTime savedAt = root.TryGetProperty("savedAtUtc", out var savedAtElement)
                    ? savedAtElement.GetDateTime()
                    : File.GetLastWriteTimeUtc(file);

                bool inferredAuto = Path.GetFileNameWithoutExtension(file).StartsWith(AutoSaveFilePrefix, StringComparison.OrdinalIgnoreCase);
                bool isAuto = root.TryGetProperty("isAutoSave", out var isAutoElement)
                    ? isAutoElement.GetBoolean()
                    : inferredAuto;

                summaries.Add(new SaveGameSummary(name!, savedAt, file, isAuto));
            }
            catch
            {
                // Ignore malformed save
            }
        }

        return summaries
            .OrderByDescending(s => s.SavedAtUtc)
            .ToList();
    }

    public SaveGameSummary SaveGame(string name, string? layoutXml = null, double? windowWidth = null, double? windowHeight = null, bool isAutoSave = false, string? targetFilePath = null)
    {
        name = string.IsNullOrWhiteSpace(name) ? "Save" : name.Trim();

        layoutXml ??= _layoutProvider?.Invoke();
        var metrics = GetWindowMetrics();
        double width = windowWidth ?? metrics.Width;
        double height = windowHeight ?? metrics.Height;

        string filePath = targetFilePath ?? GenerateNewSavePath(name);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

        var state = CaptureState(name, layoutXml, width, height, isAutoSave);
        var json = JsonSerializer.Serialize(state, _serializerOptions);
        File.WriteAllText(filePath, json);

        return new SaveGameSummary(name, state.SavedAtUtc, filePath, isAutoSave);
    }

    public SaveGameSummary SaveAutoGame(int slotIndex, string name)
    {
        string filePath = Path.Combine(_saveDirectory, $"{AutoSaveFilePrefix}{slotIndex + 1}.json");
        return SaveGame(name, null, null, null, true, filePath);
    }

    public SaveGameData LoadGame(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Save game not found", filePath);
        }

        var json = File.ReadAllText(filePath);
        var state = JsonSerializer.Deserialize<SaveGameData>(json, _serializerOptions)
            ?? throw new InvalidOperationException("Unable to read save file");
        return state;
    }

    public void ApplyGameState(SaveGameData state)
    {
        _fieldService.RestoreSnapshots(state.Fields);
        DronesService.RestoreSnapshots(state.Drones, state.DronesLost);
        RecyclingService.RestoreSnapshot(state.RecyclingCentre);

        BroadcastState();
    }

    private SaveGameData CaptureState(string name, string? layoutXml, double windowWidth, double windowHeight, bool isAutoSave)
    {
        var fields = _fieldService.CaptureSnapshots();
        var drones = DronesService.CaptureSnapshots();
        var recycling = RecyclingService.CaptureSnapshot();

        return new SaveGameData(
            CurrentVersion,
            name,
            DateTime.UtcNow,
            fields,
            drones,
            DronesService.DronesLost,
            recycling,
            layoutXml,
            windowWidth,
            windowHeight,
            isAutoSave);
    }

    private void BroadcastState()
    {
        var fieldDtos = _fieldService.GetAllFields()
            .Select(ResourceFieldMapper.ToDto)
            .ToList();
        MessageBus.Instance.Publish(new Messages.Scan.FieldsKnownMessage(fieldDtos));

        DronesService.BroadcastFleetStatus();
        RecyclingService.BroadcastInventory();
    }

    private (double Width, double Height) GetWindowMetrics()
    {
        if (_metricsProvider is null)
        {
            return (0, 0);
        }

        return _metricsProvider();
    }

    private string GenerateNewSavePath(string name)
    {
        var safeName = SanitizeFileName(name);
        string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        return Path.Combine(_saveDirectory, $"{safeName}_{timestamp}.json");
    }

    private static string SanitizeFileName(string name)
    {
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalid, '_');
        }

        return string.IsNullOrWhiteSpace(name) ? "Save" : name;
    }
}
