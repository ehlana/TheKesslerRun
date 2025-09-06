namespace TheKesslerRun2.Services.Model;
internal class DroneInstance(double maxCharge = 1000, double speed = 5.0, double miningSpeed = 1.0, double maxDamage = 100,
    double baseChargePerUnitDistance = 1.0, double loadedChargeMultiplier = 1.5, double maxOutOfChargeTime = 300)
{
    public Guid Id { get; } = Guid.NewGuid();
    public double TotalDistanceTraveled { get; set; } = 0.0;
    public double DistanceFromCentre { get; set; } = 0;
    public double CurrentDamage { get; set; } = 0.0;
    public double MaxDamage { get; set; } = maxDamage;
    public double MaxCharge { get; set; } = maxCharge;
    public double CurrentCharge { get; set; } = maxCharge;
    public double Speed { get; set; } = speed;
    public double MiningSpeed { get; set; } = miningSpeed;  // Todo: Make configurable and expand to many resource types.
    public DateTime LaunchTime { get; set; }
    public DateTime? ArrivedAtDestinationTime { get; set; }
    public double MaxCargoSize { get; set; }
    public double CurrentCargo { get; set; }
    public string CargoType { get; set; } = string.Empty;

    public DroneState State { get; set; } = DroneState.Idle;
    public Guid? DestinationId { get; set; }

    public bool IsFull => CurrentCargo >= MaxCargoSize;
    public double RemainingCargoCapacity => Math.Max(0, MaxCargoSize - CurrentCargo);

    public double BaseChargePerUnitDistance { get; set; } = baseChargePerUnitDistance;
    public double LoadedChargeMultiplier { get; set; } = loadedChargeMultiplier;
    public double MaxOutOfChargeTime {get;set; } = maxOutOfChargeTime; // seconds
    public double OutOfChargeTime {get;set; } = 0; // seconds

    public void LoadCargo(double amount, string type)
    {
        if (IsFull) return;
        var loadAmount = Math.Min(amount, RemainingCargoCapacity);
        CurrentCargo += loadAmount;
        CargoType = type;
    }

    public bool TotalTravelCostIsPossible(double distance)
    {
        double outboundCost = distance * BaseChargePerUnitDistance;
        double returnCost = distance * BaseChargePerUnitDistance * LoadedChargeMultiplier;
        return outboundCost + returnCost <= CurrentCharge;
    }

    public double LoadedPc => CurrentCargo / MaxCargoSize;
}

public enum DroneState
{
    Idle,
    EnRouteToDestination,
    Mining,
    ReturningToCentre,
    Charging,
    OutOfCharge,
    Lost
}
