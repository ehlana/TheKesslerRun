using System.Collections.Generic;
using System.Linq;
using TheKesslerRun2.Services.Model;

namespace TheKesslerRun2.Services.Services;
internal class ResourceFieldService
{
    internal static ResourceFieldService Instance { get; } = new();

    private readonly Random _random = new();
    private readonly List<ResourceField> _resourceFields = [];
    private readonly List<FieldDefinition> _fieldDefinitions = [];

    private int _maxActiveFields = 10;  // Upgradable limit
    private int _fieldsFoundPerScan = 3; // Upgradable limit
    private double _totalSpawnWeight = 0;
    private readonly double _maxSpawnDistance = 350.0;

    private ResourceFieldService()
    {
        ReloadFieldDefinitions();
    }

    public ResourceField? GetResourceFieldById(Guid id) => _resourceFields.FirstOrDefault(rf => rf.Id == id);
    internal IReadOnlyList<ResourceField> GetAllFields() => _resourceFields.ToList();

    internal IReadOnlyList<FieldSnapshot> CaptureSnapshots() =>
        _resourceFields.Select(field => field.ToSnapshot()).ToList();

    internal void RestoreSnapshots(IEnumerable<FieldSnapshot> snapshots)
    {
        _resourceFields.Clear();

        foreach (var snapshot in snapshots)
        {
            var definition = ResourceManager.Instance.GetFieldDefinition(snapshot.FieldDefinitionId);
            if (definition is null)
            {
                continue;
            }

            var field = ResourceField.FromSnapshot(snapshot, definition);
            _resourceFields.Add(field);
        }
    }

    internal IEnumerable<ResourceField> FindNewFieldsInRange(double scanRange)
    {
        if (_resourceFields.Count >= _maxActiveFields || _fieldDefinitions.Count == 0)
        {
            return Array.Empty<ResourceField>();
        }

        var foundFields = new List<ResourceField>();
        for (int i = 0; i < _fieldsFoundPerScan; i++)
        {
            if (_resourceFields.Count >= _maxActiveFields)
            {
                break;
            }

            var definition = ChooseFieldDefinition();
            if (definition is null)
            {
                break;
            }

            var maxDistance = Math.Min(scanRange, _maxSpawnDistance);
            if (maxDistance <= 0)
            {
                break;
            }

            var distance = _random.NextDouble() * maxDistance;
            var amount = Math.Max(0, definition.Amount?.NextValue(_random) ?? 0);
            if (amount <= 0)
            {
                continue;
            }

            var difficulty = definition.Difficulty?.NextValue(_random) ?? 1.0;

            if (definition.Outputs is null || definition.Outputs.Count == 0)
            {
                continue;
            }

            var field = new ResourceField(definition, distance, amount, difficulty);
            _resourceFields.Add(field);
            foundFields.Add(field);
        }

        return foundFields;
    }

    internal void ReloadFieldDefinitions()
    {
        _fieldDefinitions.Clear();
        _fieldDefinitions.AddRange(ResourceManager.Instance.GetFieldDefinitions().Where(f => f.Outputs?.Count > 0));
        _totalSpawnWeight = _fieldDefinitions.Sum(f => Math.Max(f.SpawnWeight, 0));

        if (_resourceFields.Count > 0)
        {
            foreach (var field in _resourceFields)
            {
                if (field.DistanceFromCentre > _maxSpawnDistance)
                {
                    field.DistanceFromCentre = _maxSpawnDistance;
                }
            }
        }
    }

    private FieldDefinition? ChooseFieldDefinition()
    {
        if (_fieldDefinitions.Count == 0)
        {
            return null;
        }

        if (_totalSpawnWeight <= 0)
        {
            return _fieldDefinitions[_random.Next(_fieldDefinitions.Count)];
        }

        var roll = _random.NextDouble() * _totalSpawnWeight;
        double cumulative = 0;
        foreach (var definition in _fieldDefinitions)
        {
            cumulative += Math.Max(definition.SpawnWeight, 0);
            if (roll <= cumulative)
            {
                return definition;
            }
        }

        return _fieldDefinitions.Last();
    }
}
