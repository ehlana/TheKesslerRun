using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TheKesslerRun2.Services.Model;
internal class DroneInstance(
    double maxCharge = 1000,
    double speed = 5.0,
    double miningSpeed = 1.0,
    double maxDamage = 100,
    double baseChargePerUnitDistance = 1.0,
    double loadedChargeMultiplier = 1.5,
    double maxOutOfChargeTime = 300)
{
    private readonly Dictionary<string, double> _cargoManifest = new(StringComparer.OrdinalIgnoreCase);

    public Guid Id { get; private set; } = Guid.NewGuid();
    public double TotalDistanceTraveled { get; set; } = 0.0;
    public double DistanceFromCentre { get; set; } = 0;
    public double CurrentDamage { get; set; } = 0.0;
    public double MaxDamage { get; set; } = maxDamage;
    public double MaxCharge { get; set; } = maxCharge;
    public double CurrentCharge { get; set; } = maxCharge;
    public double Speed { get; set; } = speed;
    public double MiningSpeed { get; set; } = miningSpeed;  // Todo: Make configurable per resource type.
    public DateTime LaunchTime { get; set; }
    public DateTime? ArrivedAtDestinationTime { get; set; }
    public double MaxCargoSize { get; set; }
    public double CurrentCargo { get; internal set; }

    public DroneState State { get; set; } = DroneState.Idle;
    public Guid? DestinationId { get; set; }

    private const double CargoTolerance = 0.0001;

    public bool IsFull => RemainingCargoCapacity <= CargoTolerance;
    public double RemainingCargoCapacity => Math.Max(0, MaxCargoSize - CurrentCargo);

    public double BaseChargePerUnitDistance { get; set; } = baseChargePerUnitDistance;
    public double LoadedChargeMultiplier { get; set; } = loadedChargeMultiplier;
    public double MaxOutOfChargeTime { get; set; } = maxOutOfChargeTime; // seconds
    public double OutOfChargeTime { get; set; } = 0; // seconds

    public IReadOnlyDictionary<string, double> CargoManifest => _cargoManifest;

    public void LoadCargo(IEnumerable<KeyValuePair<string, double>> payload)
    {
        foreach (var (resourceId, amount) in payload)
        {
            if (amount <= 0)
            {
                continue;
            }

            var loadAmount = Math.Min(amount, RemainingCargoCapacity);
            if (loadAmount <= 0)
            {
                break;
            }

            CurrentCargo += loadAmount;
            if (_cargoManifest.TryGetValue(resourceId, out var existing))
            {
                _cargoManifest[resourceId] = existing + loadAmount;
            }
            else
            {
                _cargoManifest[resourceId] = loadAmount;
            }
        }

        if (RemainingCargoCapacity <= CargoTolerance)
        {
            CurrentCargo = MaxCargoSize;
        }
    }

    public void SetCargoManifest(IEnumerable<KeyValuePair<string, double>> cargoItems)
    {
        _cargoManifest.Clear();
        double total = 0;

        foreach (var (resourceId, amount) in cargoItems)
        {
            if (amount <= 0)
            {
                continue;
            }

            _cargoManifest[resourceId] = amount;
            total += amount;
        }

        CurrentCargo = Math.Min(total, MaxCargoSize);
    }

    public void ClearCargo()
    {
        _cargoManifest.Clear();
        CurrentCargo = 0;
    }

    public string GetCargoSummary(Func<string, string?>? resourceNameResolver = null)
    {
        if (_cargoManifest.Count == 0)
        {
            return "-";
        }

        var items = _cargoManifest
            .OrderByDescending(kvp => kvp.Value)
            .Select(kvp =>
            {
                var name = resourceNameResolver?.Invoke(kvp.Key) ?? kvp.Key;
                return $"{name} {kvp.Value.ToString("0", CultureInfo.InvariantCulture)}";
            });

        return string.Join(", ", items);
    }

    public bool TotalTravelCostIsPossible(double distance)
    {
        double outboundCost = distance * BaseChargePerUnitDistance;
        double returnCost = distance * BaseChargePerUnitDistance * LoadedChargeMultiplier;
        return outboundCost + returnCost <= CurrentCharge;
    }

    public double LoadedPc => MaxCargoSize <= 0 ? 0 : CurrentCargo / MaxCargoSize;

    internal static DroneInstance FromSnapshot(DroneSnapshot snapshot)
    {
        var drone = new DroneInstance(
            snapshot.MaxCharge,
            snapshot.Speed,
            snapshot.GatherSpeed,
            snapshot.MaxDamage,
            snapshot.BaseChargePerUnitDistance,
            snapshot.LoadedChargeMultiplier,
            snapshot.MaxOutOfChargeTime)
        {
            Id = snapshot.Id,
            State = snapshot.State,
            DestinationId = snapshot.DestinationId,
            DistanceFromCentre = snapshot.DistanceFromCentre,
            TotalDistanceTraveled = snapshot.TotalDistanceTraveled,
            CurrentCharge = snapshot.CurrentCharge,
            MaxCargoSize = snapshot.MaxCargo,
            CurrentDamage = snapshot.CurrentDamage,
            LaunchTime = snapshot.LaunchTime,
            ArrivedAtDestinationTime = snapshot.ArrivedAtDestinationTime,
            OutOfChargeTime = snapshot.OutOfChargeTime
        };

        drone.SetCargoManifest(snapshot.CargoManifest);
        drone.CurrentCargo = Math.Min(snapshot.CurrentCargo, drone.MaxCargoSize);
        return drone;
    }
}

public enum DroneState
{
    Idle,
    EnRouteToDestination,
    Gathering,
    ReturningToCentre,
    Charging,
    OutOfCharge,
    Lost
}
