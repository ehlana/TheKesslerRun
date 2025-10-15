namespace TheKesslerRun2.Services.Model;

public record SettingsConfiguration
{
    public RecyclingSettings Recycling { get; init; } = new();
    public DroneSettings Drone { get; init; } = new();
    public AutoSaveSettings AutoSave { get; init; } = new();
}

public record RecyclingSettings
{
    public int BinCount { get; init; } = 6;
    public double BinCapacity { get; init; } = 500;
}

public record DroneSettings
{
    public double MaxCharge { get; init; } = 1000;
    public double Speed { get; init; } = 5.0;
    public double GatherSpeed { get; init; } = 1.0;
    public double MaxDamage { get; init; } = 100;
    public double BaseChargePerUnitDistance { get; init; } = 1.0;
    public double LoadedChargeMultiplier { get; init; } = 1.5;
    public double MaxOutOfChargeTime { get; init; } = 300;
    public double CargoCapacity { get; init; } = 125;
    public double RechargeRate { get; init; } = 10.0;
}

public record AutoSaveSettings
{
    public double IntervalMinutes { get; init; } = 15;
    public int SlotCount { get; init; } = 3;
}


