using System.Text.Json;
using OrbPak;
using TheKesslerRun2.Services.Model;

namespace TheKesslerRun2.Services.Services;

public sealed class SettingsManager
{
    public static SettingsManager Instance { get; } = new();

    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private SettingsConfiguration _settings = new();

    private SettingsManager() { }

    public void LoadFromFile(string path)
    {
        using var archive = OrbPakArchive.Open(Constants.OrbpakDataFile);
        var json = archive.ReadAsText(path);
        if (string.IsNullOrWhiteSpace(json))
        {
            _settings = new SettingsConfiguration();
            return;
        }

        var parsed = JsonSerializer.Deserialize<SettingsConfiguration>(json, _serializerOptions);
        _settings = parsed ?? new SettingsConfiguration();
    }

    public SettingsConfiguration Settings => _settings;
    public RecyclingSettings Recycling => _settings.Recycling;
    public DroneSettings Drone => _settings.Drone;
    public AutoSaveSettings AutoSave => _settings.AutoSave;
}


