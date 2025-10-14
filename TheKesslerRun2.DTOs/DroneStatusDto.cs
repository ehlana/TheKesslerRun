namespace TheKesslerRun2.DTOs;

public record DroneStatusDto(
    Guid Id,
    string DisplayName,
    string State,
    Guid? DestinationId,
    string? DestinationLabel,
    double DistanceFromCentre,
    double CurrentCharge,
    double MaxCharge,
    double ChargePercent,
    double CurrentCargo,
    double MaxCargo,
    double CargoPercent,
    string CargoType,
    double TotalDistanceTraveled,
    bool IsIdle);
