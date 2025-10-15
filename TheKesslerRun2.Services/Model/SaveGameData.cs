using System;
using System.Collections.Generic;

namespace TheKesslerRun2.Services.Model;

public record SaveGameData(
    int Version,
    string Name,
    DateTime SavedAtUtc,
    IReadOnlyList<FieldSnapshot> Fields,
    IReadOnlyList<DroneSnapshot> Drones,
    int DronesLost,
    RecyclingCentreSnapshot RecyclingCentre,
    string? Layout,
    double WindowWidth,
    double WindowHeight,
    bool IsAutoSave);

public record SaveGameSummary(
    string Name,
    DateTime SavedAtUtc,
    string FilePath,
    bool IsAutoSave);

