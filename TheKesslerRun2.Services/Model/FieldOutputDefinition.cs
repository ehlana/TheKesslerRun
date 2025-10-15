namespace TheKesslerRun2.Services.Model;

internal record FieldOutputDefinition
{
    public string ResourceId { get; init; } = string.Empty;
    public double Ratio { get; init; }
}
