using System;

namespace TheKesslerRun2.Services.Model;
internal class ResourceField(double distanceFromCentre, double resourceAmount = 1000, string resourceType = "ore", double miningDifficulty = 1)
{
    public Guid Id { get; } = Guid.NewGuid();
    public double InitialAmount { get; } = resourceAmount;
    public double ResourceAmount { get; set; } = resourceAmount;
    public string ResourceType { get; set; } = resourceType;
    public double MiningDifficulty { get; set; } = miningDifficulty;
    public double DistanceFromCentre { get; set; } = distanceFromCentre;

    public double CalculateMiningTime(double miningSpeed)
    {
        // Simple formula: time = (amount * difficulty) / speed
        return (ResourceAmount * MiningDifficulty) / miningSpeed;
    }

    internal bool IsDepleted(string? cargoType)
    {
        if (ResourceAmount <= 0)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(cargoType))
        {
            return false;
        }

        return !string.Equals(ResourceType, cargoType, StringComparison.OrdinalIgnoreCase);
    }
}
