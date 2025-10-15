using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace TheKesslerRun2.Services.Model;
internal class ResourceField
{
    private static readonly ReadOnlyDictionary<string, double> EmptyPayload =
        new(new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase));

    private readonly Dictionary<string, double> _remainingResources;
    private readonly ReadOnlyDictionary<string, double> _remainingResourcesView;
    private readonly Dictionary<string, double> _initialResources;
    private readonly ReadOnlyDictionary<string, double> _initialResourcesView;

    public ResourceField(
        FieldDefinition definition,
        double distanceFromCentre,
        double totalYield,
        double miningDifficulty)
    {
        ArgumentNullException.ThrowIfNull(definition);

        Id = Guid.NewGuid();
        FieldId = definition.Id;
        DisplayName = definition.DisplayName;
        Colour = definition.Colour;
        DistanceFromCentre = distanceFromCentre;
        MiningDifficulty = miningDifficulty;
        Outputs = definition.Outputs ?? Array.Empty<FieldOutputDefinition>();

        var allocations = AllocateResources(totalYield, Outputs);

        _initialResources = new Dictionary<string, double>(allocations, StringComparer.OrdinalIgnoreCase);
        _initialResourcesView = new ReadOnlyDictionary<string, double>(_initialResources);

        _remainingResources = new Dictionary<string, double>(allocations, StringComparer.OrdinalIgnoreCase);
        _remainingResourcesView = new ReadOnlyDictionary<string, double>(_remainingResources);

        TotalInitialAmount = _initialResources.Values.Sum();
    }

    public Guid Id { get; private set; }
    public string FieldId { get; private set; }
    public string DisplayName { get; private set; }
    public string Colour { get; private set; }
    public double DistanceFromCentre { get; set; }
    public double MiningDifficulty { get; set; }
    public IReadOnlyList<FieldOutputDefinition> Outputs { get; }
    public IReadOnlyDictionary<string, double> InitialResources => _initialResourcesView;
    public IReadOnlyDictionary<string, double> RemainingResources => _remainingResourcesView;
    public double TotalInitialAmount { get; private set; }
    public double TotalRemainingAmount => _remainingResources.Values.Sum();
    public bool IsDepleted => TotalRemainingAmount <= 0.0001;

    public IReadOnlyDictionary<string, double> Mine(double requestedAmount)
    {
        if (requestedAmount <= 0 || IsDepleted)
        {
            return EmptyPayload;
        }

        var totalRemaining = TotalRemainingAmount;
        var actualAmount = Math.Min(requestedAmount, totalRemaining);

        var mined = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        double allocated = 0;
        string? lastKey = null;

        foreach (var kvp in _remainingResources.Where(r => r.Value > 0))
        {
            lastKey = kvp.Key;
            var fraction = kvp.Value / totalRemaining;
            var amountForResource = Math.Min(kvp.Value, actualAmount * fraction);
            if (amountForResource <= 0)
            {
                continue;
            }

            mined[kvp.Key] = amountForResource;
            allocated += amountForResource;
        }

        if (lastKey is not null && allocated < actualAmount)
        {
            var delta = Math.Min(_remainingResources[lastKey], actualAmount - allocated);
            if (delta > 0)
            {
                mined[lastKey] = mined.TryGetValue(lastKey, out var existing)
                    ? existing + delta
                    : delta;
                allocated += delta;
            }
        }

        foreach (var kvp in mined)
        {
            _remainingResources[kvp.Key] = Math.Max(0, _remainingResources[kvp.Key] - kvp.Value);
        }

        if (mined.Count == 0)
        {
            return EmptyPayload;
        }

        return new ReadOnlyDictionary<string, double>(mined);
    }

    public Dictionary<string, double> GetRemainingSnapshot() =>
        _remainingResources.ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);

    internal FieldSnapshot ToSnapshot() =>
        new FieldSnapshot(
            Id,
            FieldId,
            DisplayName,
            Colour,
            DistanceFromCentre,
            MiningDifficulty,
            new Dictionary<string, double>(_initialResources, StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, double>(_remainingResources, StringComparer.OrdinalIgnoreCase));

    internal static ResourceField FromSnapshot(FieldSnapshot snapshot, FieldDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(definition);

        var field = new ResourceField(definition, snapshot.DistanceFromCentre, snapshot.InitialResources.Values.Sum(), snapshot.MiningDifficulty)
        {
            Id = snapshot.Id,
            DisplayName = snapshot.DisplayName,
            Colour = snapshot.Colour,
            DistanceFromCentre = snapshot.DistanceFromCentre,
            MiningDifficulty = snapshot.MiningDifficulty
        };

        field._initialResources.Clear();
        foreach (var kvp in snapshot.InitialResources)
        {
            field._initialResources[kvp.Key] = kvp.Value;
        }

        field._remainingResources.Clear();
        foreach (var kvp in snapshot.RemainingResources)
        {
            field._remainingResources[kvp.Key] = kvp.Value;
        }

        field.TotalInitialAmount = field._initialResources.Values.Sum();
        return field;
    }

    private static Dictionary<string, double> AllocateResources(
        double totalYield,
        IReadOnlyList<FieldOutputDefinition> outputs)
    {
        var allocations = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        if (outputs is null || outputs.Count == 0 || totalYield <= 0)
        {
            return allocations;
        }

        var ratioSum = outputs.Sum(o => o.Ratio);
        if (ratioSum <= 0)
        {
            return allocations;
        }

        double remaining = totalYield;
        FieldOutputDefinition? last = null;

        foreach (var output in outputs)
        {
            last = output;
            var portion = totalYield * (output.Ratio / ratioSum);
            var clamped = Math.Max(0, Math.Min(portion, remaining));
            allocations[output.ResourceId] = clamped;
            remaining -= clamped;
        }

        if (remaining > 0 && last is not null)
        {
            allocations[last.ResourceId] = allocations.TryGetValue(last.ResourceId, out var existing)
                ? existing + remaining
                : remaining;
        }

        return allocations;
    }
}
