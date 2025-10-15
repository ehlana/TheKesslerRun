using System;

namespace TheKesslerRun2.Services.Model;

internal record RangeDefinition
{
    public double Min { get; init; }
    public double Max { get; init; }

    public RangeDefinition() : this(0, 0) { }
    public RangeDefinition(double min, double max)
    {
        Min = min;
        Max = max;
    }

    public double NextValue(Random random)
    {
        if (Max <= Min)
        {
            return Min;
        }

        return random.NextDouble() * (Max - Min) + Min;
    }
}
