namespace TheKesslerRun2.Services.Model;

internal record FieldDefinition
{
    public string Id { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Colour { get; init; } = "#FFFFFF";
    public double SpawnWeight { get; init; } = 1.0;
    public RangeDefinition Amount { get; init; } = new();
    public RangeDefinition Difficulty { get; init; } = new();
    public IReadOnlyList<FieldOutputDefinition>? Outputs { get; init; } = Array.Empty<FieldOutputDefinition>();
}
