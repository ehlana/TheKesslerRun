namespace TheKesslerRun2.DTOs;

public record ResourceFieldDto(
    Guid Id,
    string FieldId,
    string ResourceType,
    double ResourceAmount,
    double MiningDifficulty,
    double DistanceFromCentre,
    IReadOnlyList<ResourceFieldYieldDto> ResourceBreakdown,
    string OutputSummary);

public record ResourceFieldYieldDto(
    string ResourceId,
    string DisplayName,
    double InitialRatio,
    double RemainingAmount);
