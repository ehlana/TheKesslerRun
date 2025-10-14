namespace TheKesslerRun2.DTOs;

public record RecyclingResourceDto(
    string ResourceId,
    string DisplayName,
    double Amount,
    double UnitValue,
    double TotalValue);
