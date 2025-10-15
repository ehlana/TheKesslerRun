using System;
using System.Collections.Generic;
using System.Linq;
using TheKesslerRun2.DTOs;
using TheKesslerRun2.Services.Model;
using static TheKesslerRun2.Services.Messages.Drone;
using static TheKesslerRun2.Services.Messages.RecyclingCentre;
using static TheKesslerRun2.Services.Messages.Scan;

namespace TheKesslerRun2.Services.Services;

internal partial class DronesService : BaseService
{
    private readonly DroneSettings _droneSettings;
    private readonly double _rechargeRate;
    private int _dronesLost = 0;
    private readonly ResourceFieldService _resourceService = ResourceFieldService.Instance;
    private readonly List<DroneInstance> _drones = [];

    internal int DronesLost => _dronesLost;

    public DronesService()
    {
        _droneSettings = SettingsManager.Instance.Drone;
        _rechargeRate = _droneSettings.RechargeRate;

        for (int i = 0; i < 3; i++)
        {
            var drone = new DroneInstance(
                _droneSettings.MaxCharge,
                _droneSettings.Speed,
                _droneSettings.GatherSpeed,
                _droneSettings.MaxDamage,
                _droneSettings.BaseChargePerUnitDistance,
                _droneSettings.LoadedChargeMultiplier,
                _droneSettings.MaxOutOfChargeTime)
            {
                MaxCargoSize = _droneSettings.CargoCapacity
            };

            _drones.Add(drone);
        }

        PublishFleetStatus();
    }

    protected override void SubscribeToMessages()
    {
        MessageBus.Instance.Subscribe<LaunchMessage>(Receive);
        MessageBus.Instance.Subscribe<RecallMessage>(Receive);
    }

    internal IReadOnlyList<DroneSnapshot> CaptureSnapshots()
    {
        var snapshots = new List<DroneSnapshot>(_drones.Count);
        foreach (var drone in _drones)
        {
            var cargo = new Dictionary<string, double>(drone.CargoManifest, StringComparer.OrdinalIgnoreCase);
            double timeRemaining = CalculateTimeRemainingSeconds(drone);

            snapshots.Add(new DroneSnapshot(
                drone.Id,
                drone.State,
                drone.DestinationId,
                drone.DistanceFromCentre,
                drone.CurrentCharge,
                drone.MaxCharge,
                drone.CurrentCargo,
                drone.MaxCargoSize,
                drone.CurrentDamage,
                drone.MaxDamage,
                drone.TotalDistanceTraveled,
                drone.Speed,
                drone.MiningSpeed,
                drone.BaseChargePerUnitDistance,
                drone.LoadedChargeMultiplier,
                drone.MaxOutOfChargeTime,
                drone.OutOfChargeTime,
                timeRemaining,
                drone.LaunchTime,
                drone.ArrivedAtDestinationTime,
                cargo));
        }

        return snapshots;
    }

    internal void RestoreSnapshots(IEnumerable<DroneSnapshot> snapshots, int dronesLost)
    {
        _drones.Clear();

        foreach (var snapshot in snapshots)
        {
            var instance = DroneInstance.FromSnapshot(snapshot);
            _drones.Add(instance);
        }

        _dronesLost = dronesLost;
        PublishFleetStatus();
    }

    internal void BroadcastFleetStatus() => PublishFleetStatus();

    public void Receive(LaunchMessage message)
    {
        var drone = _drones.FirstOrDefault(d => d.Id == message.DroneId);
        if (drone is null)
        {
            MessageBus.Instance.Publish(new LaunchFailedMessage(message.DroneId, message.DestinationId, "Drone not found."));
            return;
        }

        if (drone.State != DroneState.Idle)
        {
            MessageBus.Instance.Publish(new LaunchFailedMessage(message.DroneId, message.DestinationId, "Drone is not idle."));
            return;
        }

        var field = _resourceService.GetResourceFieldById(message.DestinationId);
        if (field is null)
        {
            MessageBus.Instance.Publish(new LaunchFailedMessage(message.DroneId, message.DestinationId, "Resource field unavailable."));
            return;
        }

        if (!drone.TotalTravelCostIsPossible(field.DistanceFromCentre))
        {
            MessageBus.Instance.Publish(new LaunchFailedMessage(message.DroneId, message.DestinationId, "Insufficient charge for round trip."));
            return;
        }

        drone.State = DroneState.EnRouteToDestination;
        drone.DestinationId = message.DestinationId;
        drone.LaunchTime = DateTime.UtcNow;
        drone.ArrivedAtDestinationTime = null;
        drone.DistanceFromCentre = 0;
        drone.ClearCargo();

        MessageBus.Instance.Publish(new LaunchedMessage(drone.Id, message.DestinationId));
        PublishFleetStatus();
    }

    public void Receive(RecallMessage message)
    {
        var drone = _drones.FirstOrDefault(d => d.Id == message.DroneId);
        if (drone is null)
        {
            MessageBus.Instance.Publish(new RecallFailedMessage(message.DroneId, "Drone not found."));
            return;
        }

        switch (drone.State)
        {
            case DroneState.EnRouteToDestination:
            case DroneState.Gathering:
                drone.State = DroneState.ReturningToCentre;
                MessageBus.Instance.Publish(new RecallAcknowledgedMessage(drone.Id, drone.DestinationId));
                PublishFleetStatus();
                break;
            case DroneState.ReturningToCentre:
                MessageBus.Instance.Publish(new RecallFailedMessage(message.DroneId, "Drone is already returning to the recycling centre."));
                break;
            case DroneState.Idle:
                MessageBus.Instance.Publish(new RecallFailedMessage(message.DroneId, "Drone is idle."));
                break;
            case DroneState.Charging:
                MessageBus.Instance.Publish(new RecallFailedMessage(message.DroneId, "Drone is currently charging."));
                break;
            case DroneState.OutOfCharge:
                MessageBus.Instance.Publish(new RecallFailedMessage(message.DroneId, "Drone is out of charge and awaiting recovery."));
                break;
            case DroneState.Lost:
                MessageBus.Instance.Publish(new RecallFailedMessage(message.DroneId, "Drone has been lost."));
                break;
            default:
                MessageBus.Instance.Publish(new RecallFailedMessage(message.DroneId, "Drone cannot be recalled right now."));
                break;
        }
    }

    private void HandleDroneEnRouteToDestination(DroneInstance drone, double deltaSeconds)
    {
        if (drone.DestinationId is null)
        {
            ResetToIdle(drone);
            return;
        }

        var field = _resourceService.GetResourceFieldById(drone.DestinationId.Value);
        if (field is null)
        {
            ResetToIdle(drone);
            return;
        }

        double distanceRemaining = Math.Max(0, field.DistanceFromCentre - drone.DistanceFromCentre);
        double travel = drone.Speed * deltaSeconds;
        double actualTravel = Math.Min(travel, distanceRemaining);

        drone.DistanceFromCentre += actualTravel;
        drone.TotalDistanceTraveled += actualTravel;

        double costPerUnit = CalculateTravelCostPerUnit(drone);
        drone.CurrentCharge = Math.Max(0, drone.CurrentCharge - actualTravel * costPerUnit);

        if (drone.CurrentCharge <= 0)
        {
            TransitionToOutOfCharge(drone);
            return;
        }

        if (distanceRemaining <= travel)
        {
            drone.DistanceFromCentre = field.DistanceFromCentre;
            drone.State = DroneState.Gathering;
            drone.ArrivedAtDestinationTime = DateTime.UtcNow;
            MessageBus.Instance.Publish(new ArrivedAtDestinationMessage(drone.Id, drone.DestinationId.Value));
        }
    }

    private void HandleDroneGathering(DroneInstance drone, double deltaSeconds)
    {
        if (drone.DestinationId is null)
        {
            drone.State = DroneState.ReturningToCentre;
            return;
        }

        var field = _resourceService.GetResourceFieldById(drone.DestinationId.Value);
        if (field is null)
        {
            drone.State = DroneState.ReturningToCentre;
            return;
        }

        if (field.IsDepleted)
        {
            drone.State = DroneState.ReturningToCentre;
            PublishFieldUpdate(field);
            MessageBus.Instance.Publish(new GatheringCompletedMessage(drone.Id, drone.DestinationId.Value));
            return;
        }

        double gatheredThisTick = drone.MiningSpeed / Math.Max(field.MiningDifficulty, 0.1) * deltaSeconds;
        double availableCapacity = drone.RemainingCargoCapacity;
        double requestedAmount = Math.Min(gatheredThisTick, availableCapacity);

        if (requestedAmount <= 0)
        {
            drone.State = DroneState.ReturningToCentre;
            PublishFieldUpdate(field);
            MessageBus.Instance.Publish(new GatheringCompletedMessage(drone.Id, drone.DestinationId.Value));
            return;
        }

        var gathered = field.Mine(requestedAmount);
        if (gathered.Count == 0)
        {
            drone.State = DroneState.ReturningToCentre;
            PublishFieldUpdate(field);
            MessageBus.Instance.Publish(new GatheringCompletedMessage(drone.Id, drone.DestinationId.Value));
            return;
        }

        drone.LoadCargo(gathered);
        PublishFieldUpdate(field);

        if (drone.IsFull || field.IsDepleted)
        {
            drone.State = DroneState.ReturningToCentre;
            MessageBus.Instance.Publish(new GatheringCompletedMessage(drone.Id, drone.DestinationId.Value));
        }
    }

    private void HandleDroneReturningToCentre(DroneInstance drone, double deltaSeconds)
    {
        double distanceRemaining = drone.DistanceFromCentre;
        double travel = drone.Speed * deltaSeconds;
        double actualTravel = Math.Min(travel, distanceRemaining);

        drone.DistanceFromCentre = Math.Max(0, drone.DistanceFromCentre - actualTravel);
        drone.TotalDistanceTraveled += actualTravel;

        double costPerUnit = CalculateTravelCostPerUnit(drone);
        drone.CurrentCharge = Math.Max(0, drone.CurrentCharge - actualTravel * costPerUnit);

        if (distanceRemaining <= travel)
        {
            drone.State = DroneState.Charging;
            drone.DistanceFromCentre = 0;
            drone.DestinationId = null;
            var manifest = drone.CargoManifest;

            foreach (var entry in manifest)
            {
                if (entry.Value <= 0)
                {
                    continue;
                }

                MessageBus.Instance.Publish(new DepositCargoMessage(drone.Id, entry.Key, entry.Value));
            }

            drone.CurrentCharge = drone.MaxCharge;
            drone.ClearCargo();

            MessageBus.Instance.Publish(new ArrivedAtCentreMessage(drone.Id));
            return;
        }

        if (drone.CurrentCharge <= 0)
        {
            TransitionToOutOfCharge(drone);
        }
    }

    private void HandleDroneOutOfCharge(DroneInstance drone, double deltaSeconds)
    {
        drone.OutOfChargeTime += deltaSeconds;
        if (drone.OutOfChargeTime >= drone.MaxOutOfChargeTime)
        {
            drone.State = DroneState.Lost;
            drone.DistanceFromCentre = 0;
            drone.DestinationId = null;
            drone.ClearCargo();
            drone.CurrentCharge = drone.MaxCharge;
            drone.OutOfChargeTime = 0;
            MessageBus.Instance.Publish(new LostMessage(drone.Id));
        }
    }

    private void HandleDroneCharging(DroneInstance drone, double deltaSeconds)
    {
        drone.CurrentCharge += _rechargeRate * deltaSeconds;

        if (drone.CurrentCharge >= drone.MaxCharge)
        {
            drone.CurrentCharge = drone.MaxCharge;
            drone.State = DroneState.Idle;
            MessageBus.Instance.Publish(new RechargedMessage(drone.Id));
        }
    }

    private void HandleDroneTick(DroneInstance drone, double deltaSeconds)
    {
        switch (drone.State)
        {
            case DroneState.EnRouteToDestination:
                HandleDroneEnRouteToDestination(drone, deltaSeconds);
                break;
            case DroneState.Gathering:
                HandleDroneGathering(drone, deltaSeconds);
                break;
            case DroneState.ReturningToCentre:
                HandleDroneReturningToCentre(drone, deltaSeconds);
                break;
            case DroneState.OutOfCharge:
                HandleDroneOutOfCharge(drone, deltaSeconds);
                break;
            case DroneState.Charging:
                HandleDroneCharging(drone, deltaSeconds);
                break;
            case DroneState.Idle:
            case DroneState.Lost:
            default:
                break;
        }
    }

    protected override void OnTick()
    {
        var lostDrones = _drones.Where(d => d.State == DroneState.Lost).ToList();
        if (lostDrones.Count > 0)
        {
            _dronesLost += lostDrones.Count;
            _drones.RemoveAll(d => d.State == DroneState.Lost);
        }

        foreach (var drone in _drones)
        {
            HandleDroneTick(drone, Threshold);
        }

        PublishFleetStatus();
    }

    private double CalculateTimeRemainingSeconds(DroneInstance drone)
    {
        const double epsilon = 0.0001;

        return drone.State switch
        {
            DroneState.EnRouteToDestination =>
                drone.Speed <= epsilon ? 0 : GetDistanceRemainingToDestination(drone) / Math.Max(drone.Speed, epsilon),
            DroneState.Gathering => CalculateGatheringTimeRemaining(drone),
            DroneState.ReturningToCentre =>
                drone.Speed <= epsilon ? 0 : drone.DistanceFromCentre / Math.Max(drone.Speed, epsilon),
            DroneState.Charging =>
                _rechargeRate <= epsilon ? 0 : Math.Max(0, drone.MaxCharge - drone.CurrentCharge) / Math.Max(_rechargeRate, epsilon),
            DroneState.OutOfCharge => Math.Max(0, drone.MaxOutOfChargeTime - drone.OutOfChargeTime),
            _ => 0
        };
    }

    private double CalculateGatheringTimeRemaining(DroneInstance drone)
    {
        if (drone.DestinationId is null)
        {
            return 0;
        }

        var field = _resourceService.GetResourceFieldById(drone.DestinationId.Value);
        if (field is null)
        {
            return 0;
        }

        double rate = drone.MiningSpeed / Math.Max(field.MiningDifficulty, 0.1);
        if (rate <= 0)
        {
            return 0;
        }

        double capacityRemaining = Math.Max(0, drone.RemainingCargoCapacity);
        double available = Math.Max(0, field.TotalRemainingAmount);
        double target = Math.Min(capacityRemaining, available);
        return target <= 0 ? 0 : target / rate;
    }

    private double GetDistanceRemainingToDestination(DroneInstance drone)
    {
        if (drone.DestinationId is null)
        {
            return 0;
        }

        var field = _resourceService.GetResourceFieldById(drone.DestinationId.Value);
        if (field is null)
        {
            return 0;
        }

        return Math.Max(0, field.DistanceFromCentre - drone.DistanceFromCentre);
    }

    private static double CalculateTravelCostPerUnit(DroneInstance drone)
    {
        if (drone.MaxCargoSize <= 0)
        {
            return drone.BaseChargePerUnitDistance;
        }

        double loadedMultiplier = 1 + (drone.LoadedChargeMultiplier - 1) * Math.Clamp(drone.LoadedPc, 0, 1);
        return drone.BaseChargePerUnitDistance * loadedMultiplier;
    }

    private void ResetToIdle(DroneInstance drone)
    {
        drone.State = DroneState.Idle;
        drone.DistanceFromCentre = 0;
        drone.DestinationId = null;
        drone.ClearCargo();
        MessageBus.Instance.Publish(new ArrivedAtCentreMessage(drone.Id));
    }

    private void TransitionToOutOfCharge(DroneInstance drone)
    {
        drone.State = DroneState.OutOfCharge;
        drone.CurrentCharge = 0;
        MessageBus.Instance.Publish(new OutOfChargeMessage(drone.Id));
    }

    private void PublishFleetStatus()
    {
        var snapshot = _drones
            .Select((drone, index) => ToStatusDto(index, drone))
            .ToList();

        MessageBus.Instance.Publish(new FleetStatusMessage(snapshot, _dronesLost));
    }

    private void PublishFieldUpdate(ResourceField field)
    {
        var dto = ResourceFieldMapper.ToDto(field);
        MessageBus.Instance.Publish(new FieldUpdatedMessage(dto));
    }

    private DroneStatusDto ToStatusDto(int index, DroneInstance drone)
    {
        string? destinationLabel = null;
        if (drone.DestinationId is { } destinationId)
        {
            var field = _resourceService.GetResourceFieldById(destinationId);
            if (field is not null)
            {
                destinationLabel = $"{field.DisplayName} @ {field.DistanceFromCentre:0} km";
            }
        }

        double chargePercent = drone.MaxCharge > 0 ? Math.Clamp(drone.CurrentCharge / drone.MaxCharge, 0, 1) : 0;
        double cargoPercent = drone.MaxCargoSize > 0 ? Math.Clamp(drone.CurrentCargo / drone.MaxCargoSize, 0, 1) : 0;
        string cargoSummary = drone.GetCargoSummary(id => ResourceManager.Instance.Get(id)?.DisplayName);
        bool isRecallable = drone.State is DroneState.EnRouteToDestination or DroneState.Gathering;

        return new DroneStatusDto(
            drone.Id,
            $"Drone {index + 1}",
            drone.State.ToString(),
            drone.DestinationId,
            destinationLabel,
            Math.Max(0, drone.DistanceFromCentre),
            Math.Max(0, drone.CurrentCharge),
            drone.MaxCharge,
            chargePercent,
            Math.Max(0, drone.CurrentCargo),
            drone.MaxCargoSize,
            cargoPercent,
            cargoSummary,
            Math.Max(0, drone.TotalDistanceTraveled),
            drone.State == DroneState.Idle,
            isRecallable);
    }
}
