namespace TheKesslerRun2.DTOs;

public record ResourceFieldDto(Guid Id, double ResourceAmount, string ResourceType, double MiningDifficulty, double DistanceFromCentre);
