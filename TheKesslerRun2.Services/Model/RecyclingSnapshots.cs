using System;
using System.Collections.Generic;

namespace TheKesslerRun2.Services.Model;

public record RecyclingBinSnapshot(
    Guid Id,
    string Label,
    string? ResourceId,
    string? ResourceName,
    double UnitValue,
    double Amount,
    double Capacity);

public record RecyclingCentreSnapshot(
    double Credits,
    IReadOnlyList<RecyclingBinSnapshot> Bins);

