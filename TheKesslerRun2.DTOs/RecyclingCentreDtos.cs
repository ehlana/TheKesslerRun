namespace TheKesslerRun2.DTOs;

public record RecyclingBinDto(
    Guid Id,
    string Label,
    string? ResourceId,
    string? ResourceName,
    double Amount,
    double Capacity,
    double UnitValue,
    double TotalValue,
    double FillPercentage,
    double RemainingCapacity);

public record RecyclingCentreSnapshotDto(
    IReadOnlyList<RecyclingBinDto> Bins,
    double TotalStoredAmount,
    double TotalStoredValue,
    double Credits,
    int BinCount,
    int OccupiedBins,
    double BinCapacity);
