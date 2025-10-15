using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TheKesslerRun2.ResourceEditor.Models;

internal sealed class ResourceFileModel
{
    [JsonPropertyName("resources")]
    public List<ResourceDefinition> Resources { get; set; } = new();

    [JsonPropertyName("fields")]
    public List<FieldDefinition> Fields { get; set; } = new();
}

internal sealed class ResourceDefinition
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public double BaseMiningDifficulty { get; set; }
    public double BaseValue { get; set; }
    public string Colour { get; set; } = "#FFFFFF";
}

internal sealed class FieldDefinition
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Colour { get; set; } = "#FFFFFF";
    public double SpawnWeight { get; set; }
    public RangeDefinition Amount { get; set; } = new();
    public RangeDefinition Difficulty { get; set; } = new();
    public List<FieldOutputDefinition> Outputs { get; set; } = new();
}

internal sealed class RangeDefinition
{
    public double Min { get; set; }
    public double Max { get; set; }
}

internal sealed class FieldOutputDefinition
{
    public string ResourceId { get; set; } = string.Empty;
    public double Ratio { get; set; }
}
