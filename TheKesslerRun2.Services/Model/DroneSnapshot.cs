using System;
using System.Collections.Generic;

namespace TheKesslerRun2.Services.Model;

public record DroneSnapshot(
    Guid Id,
    DroneState State,
    Guid? DestinationId,
    double DistanceFromCentre,
    double CurrentCharge,
    double MaxCharge,
    double CurrentCargo,
    double MaxCargo,
    double CurrentDamage,
    double MaxDamage,
    double TotalDistanceTraveled,
    double Speed,
    double GatherSpeed,
    double BaseChargePerUnitDistance,
    double LoadedChargeMultiplier,
    double MaxOutOfChargeTime,
    double OutOfChargeTime,
    double TimeRemainingSeconds,
    DateTime LaunchTime,
    DateTime? ArrivedAtDestinationTime,
    IReadOnlyDictionary<string, double> CargoManifest);

