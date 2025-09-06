using TheKesslerRun2.Services.Interfaces;
using TheKesslerRun2.Services.Services;

namespace TheKesslerRun2.Services;
public class Game
{
    public static Game Instance { get; } = new Game();
    private Game() { }
    public HeartbeatService? HeartbeatService { get ; private set; }
    private List<BaseService> _services = [];

    public void StartGame(IHeartbeatProvider heartbeatProvider)
    {
        HeartbeatService = new HeartbeatService(heartbeatProvider);
        _services.Add(new ScanService());
    }
}
