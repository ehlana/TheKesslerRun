using System;
using System.IO;
using System.Text.Json;
using TheKesslerRun2.ResourceEditor.Models;

namespace TheKesslerRun2.ResourceEditor.Services;

internal sealed class ResourceDataService
{
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        WriteIndented = true
    };

    public ResourceFileModel Load(string path)
    {
        if (!File.Exists(path))
        {
            return new ResourceFileModel();
        }

        var json = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new ResourceFileModel();
        }

        var model = JsonSerializer.Deserialize<ResourceFileModel>(json, _serializerOptions);
        return model ?? new ResourceFileModel();
    }

    public void Save(string path, ResourceFileModel model)
    {
        if (model is null)
        {
            throw new ArgumentNullException(nameof(model));
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var json = JsonSerializer.Serialize(model, _serializerOptions);
        File.WriteAllText(path, json);
    }
}
