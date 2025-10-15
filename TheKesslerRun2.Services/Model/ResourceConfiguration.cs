using System;

namespace TheKesslerRun2.Services.Model;

internal record ResourceConfiguration
{
    public IReadOnlyList<ResourceDefinition> Resources { get; init; } = Array.Empty<ResourceDefinition>();
    public IReadOnlyList<FieldDefinition> Fields { get; init; } = Array.Empty<FieldDefinition>();
}
