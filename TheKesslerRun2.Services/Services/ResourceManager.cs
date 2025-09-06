using System.Text.Json;

namespace TheKesslerRun2.Services.Services;
internal class ResourceManager
{
    public static ResourceManager Instance { get; } = new ResourceManager();

    private Dictionary<string, Model.ResourceDefinition> _resourceDefinitions = [];

    private ResourceManager() { }

    public void LoadFromFile(string path)
    {
        var json = File.ReadAllText(path);
        var defs = JsonSerializer.Deserialize<List<Model.ResourceDefinition>>(json);
        _resourceDefinitions = defs?.ToDictionary(r => r.Id, r => r) ?? [];
    }

    public Model.ResourceDefinition? Get(string id) => _resourceDefinitions.TryGetValue(id, out var def) ? def : null;
}
