using System.Globalization;
using System.Linq;
using TheKesslerRun2.DTOs;
using TheKesslerRun2.Services.Model;

namespace TheKesslerRun2.Services.Services;

internal static class ResourceFieldMapper
{
    public static ResourceFieldDto ToDto(ResourceField field)
    {
        var resourceManager = ResourceManager.Instance;
        var remaining = field.RemainingResources;

        var breakdown = field.InitialResources
            .OrderByDescending(kvp => kvp.Value)
            .Select(kvp =>
            {
                var definition = resourceManager.Get(kvp.Key);
                var displayName = definition?.DisplayName ?? kvp.Key;
                var ratio = field.TotalInitialAmount <= 0 ? 0 : kvp.Value / field.TotalInitialAmount;
                remaining.TryGetValue(kvp.Key, out var remainingAmount);
                return new ResourceFieldYieldDto(kvp.Key, displayName, ratio, remainingAmount);
            })
            .ToList();

        var outputSummary = breakdown.Count == 0
            ? "No recoverable resources"
            : string.Join(", ", breakdown.Select(b => $"{b.DisplayName} {(b.InitialRatio * 100).ToString("0", CultureInfo.InvariantCulture)}%"));

        return new ResourceFieldDto(
            field.Id,
            field.FieldId,
            field.DisplayName,
            field.TotalRemainingAmount,
            field.MiningDifficulty,
            field.DistanceFromCentre,
            breakdown,
            outputSummary);
    }
}
