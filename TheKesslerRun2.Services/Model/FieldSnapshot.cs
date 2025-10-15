using System;
using System.Collections.Generic;

namespace TheKesslerRun2.Services.Model;

public record FieldSnapshot(
    Guid Id,
    string FieldDefinitionId,
    string DisplayName,
    string Colour,
    double DistanceFromCentre,
    double MiningDifficulty,
    IReadOnlyDictionary<string, double> InitialResources,
    IReadOnlyDictionary<string, double> RemainingResources);

