using TheKesslerRun2.DTOs;
using TheKesslerRun2.Services.Model;

namespace TheKesslerRun2.Services.Services;
internal class ResourceFieldService
{
    internal static ResourceFieldService Instance { get; } = new ResourceFieldService();

    private ResourceFieldService() { }

    private List<ResourceField> _resourceFields = [];
    private int _maxActiveFields = 10;  // Upgradable limit
    private int _fieldsFoundPerScan = 3; // Upgradable limit
    private readonly Random _random = new();

    public ResourceField? GetResourceFieldById(Guid id) => _resourceFields.FirstOrDefault(rf => rf.Id == id);

    internal IReadOnlyList<ResourceField> GetAllFields() => _resourceFields.ToList();

    internal IEnumerable<ResourceField> FindNewFieldsInRange(double scanRange)
    {
        if(_resourceFields.Count >= _maxActiveFields) return [];

        List<ResourceField> foundFields = [];

        for(int i = 0; i < _fieldsFoundPerScan; i++)
        {
            if(_resourceFields.Count >= _maxActiveFields) break;
            var distance = _random.NextDouble() * scanRange;
            string resourceType = _random.NextDouble() < 0.7 ? "ore" : "crystal";

            double amount = _random.Next(500, 2000);
            double difficulty = resourceType == "ore" ? _random.NextDouble() * 2 + 0.5 : _random.NextDouble() * 3 + 1.0;

            var field = new ResourceField(distance, amount, resourceType, difficulty);
            _resourceFields.Add(field);
            foundFields.Add(field);
        }
        return foundFields;
    }
}
