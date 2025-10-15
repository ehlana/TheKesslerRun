using System;

namespace TheKesslerRun2.Services.Interfaces;
public interface IHeartbeatProvider
{
    event Action<double> Tick;
    void Pause();
    void Resume();
    bool IsPaused { get; }
}
