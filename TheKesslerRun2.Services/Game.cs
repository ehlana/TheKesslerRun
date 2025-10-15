using System;
using System.Collections.Generic;
using System.Linq;
using OrbPak;
using TheKesslerRun2.Services.Interfaces;
using TheKesslerRun2.Services.Services;

namespace TheKesslerRun2.Services;
public class Game : IDisposable
{
    public static Game Instance { get; } = new();
    private Game() { }
    public HeartbeatService? HeartbeatService { get ; private set; }
    private readonly List<BaseService> _services = [];
    private bool _started;
    public OrbPakArchive OrbPak = OrbPakArchive.Open(Constants.OrbpakDataFile);

    public void StartGame(IHeartbeatProvider heartbeatProvider)
    {
        if (_started)
        {
            return;
        }

        SettingsManager.Instance.LoadFromFile(Constants.SettingsDataPath);
        ResourceManager.Instance.LoadFromFiles(Constants.ResourceDataPath, Constants.FieldDataPath);
        ResourceFieldService.Instance.ReloadFieldDefinitions();

        HeartbeatService = new HeartbeatService(heartbeatProvider);
        _services.Add(new ScanService());
        _services.Add(new DronesService());
        _services.Add(new RecyclingCentreService());

        _started = true;
    }

    internal T GetService<T>() where T : BaseService
    {
        if (!_started)
        {
            throw new InvalidOperationException("Game has not been started.");
        }

        return (T)_services.First(s => s is T);
    }

    public bool VerifyOrbpak()
    {
        try
        {
            // Only verifies if ManifestHash flag is present
            OrbPak.VerifyManifest();
        }
        catch (Exception ex)
        {
            return false;
        }

        return true;
    }

    public void Dispose()
    {
        OrbPak.Dispose();
    }
}
