using System.Text.Json;
using OrbPak;
using TheKesslerRun2.Services.Model;

namespace TheKesslerRun2.Services.Services;
internal class ResourceManager
{
    public static ResourceManager Instance { get; } = new();

    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private Dictionary<string, ResourceDefinition> _resourceDefinitions = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, FieldDefinition> _fieldDefinitions = new(StringComparer.OrdinalIgnoreCase);

    private ResourceManager() { }

    public void LoadFromFiles(string resourcesPath, string fieldsPath)
    {
        using var archive = OrbPakArchive.Open(Constants.OrbpakDataFile);

        var resources = ReadConfiguration(archive, resourcesPath)?.Resources
            ?? Array.Empty<ResourceDefinition>();

        var fields = ReadConfiguration(archive, fieldsPath)?.Fields
            ?? Array.Empty<FieldDefinition>();

        _resourceDefinitions = resources.ToDictionary(r => r.Id, r => r, StringComparer.OrdinalIgnoreCase);
        _fieldDefinitions = fields.ToDictionary(f => f.Id, f => f, StringComparer.OrdinalIgnoreCase);

        ValidateFieldOutputs();
    }

    private ResourceConfiguration? ReadConfiguration(OrbPakArchive archive, string path)
    {
        var json = archive.ReadAsText(path);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return JsonSerializer.Deserialize<ResourceConfiguration>(json, _serializerOptions);
    }

    public ResourceDefinition? Get(string id) => _resourceDefinitions.GetValueOrDefault(id);
    public IReadOnlyCollection<ResourceDefinition> GetResourceDefinitions() => _resourceDefinitions.Values;
    public IReadOnlyCollection<FieldDefinition> GetFieldDefinitions() => _fieldDefinitions.Values;
    public FieldDefinition? GetFieldDefinition(string id) => _fieldDefinitions.GetValueOrDefault(id);

    private void ValidateFieldOutputs()
    {
        foreach (var field in _fieldDefinitions.Values)
        {
            if (field.Outputs is null || field.Outputs.Count == 0)
            {
                continue;
            }

            var ratioSum = field.Outputs.Sum(output => output.Ratio);
            if (ratioSum <= 0)
            {
                throw new InvalidOperationException($"Field '{field.Id}' must define at least one positive output ratio.");
            }

            foreach (var output in field.Outputs)
            {
                if (!_resourceDefinitions.ContainsKey(output.ResourceId))
                {
                    throw new InvalidOperationException($"Field '{field.Id}' references unknown resource '{output.ResourceId}'.");
                }

                if (output.Ratio < 0)
                {
                    throw new InvalidOperationException($"Field '{field.Id}' has a negative ratio for resource '{output.ResourceId}'.");
                }
            }
        }
    }
}
