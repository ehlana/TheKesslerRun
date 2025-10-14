using System.Linq;
using TheKesslerRun2.DTOs;
using TheKesslerRun2.Services.Interfaces;
using TheKesslerRun2.Services.Model;
using static TheKesslerRun2.Services.Messages.Drone;

namespace TheKesslerRun2.Services.Services;

internal partial class DronesService : BaseService, IMessageReceiver<LaunchMessage>
{
    private const double DefaultRechargeRate = 10.0;
    private const double DefaultCargoCapacity = 250.0;

    private int _dronesLost = 0;
    private readonly ResourceFieldService _resourceService = ResourceFieldService.Instance;
    private readonly List<DroneInstance> _drones = [];

    public DronesService() : base()
    {
        for (int i = 0; i < 3; i++)
        {
            var drone = new DroneInstance
            {
                MaxCargoSize = DefaultCargoCapacity,
                CurrentCargo = 0,
                CargoType = string.Empty
            };

            _drones.Add(drone);
        }

        PublishFleetStatus();
    }

    public void Receive(LaunchMessage message)
    {
        var drone = _drones.FirstOrDefault(d => d.Id == message.DroneId);
        if (drone is null)
        {
            MessageBus.Publish(new LaunchFailedMessage(message.DroneId, message.DestinationId, "Drone not found."));
            return;
        }

        if (drone.State != DroneState.Idle)
        {
            MessageBus.Publish(new LaunchFailedMessage(message.DroneId, message.DestinationId, "Drone is not idle."));
            return;
        }

        var field = _resourceService.GetResourceFieldById(message.DestinationId);
        if (field is null)
        {
            MessageBus.Publish(new LaunchFailedMessage(message.DroneId, message.DestinationId, "Resource field unavailable."));
            return;
        }

        if (!drone.TotalTravelCostIsPossible(field.DistanceFromCentre))
        {
            MessageBus.Publish(new LaunchFailedMessage(message.DroneId, message.DestinationId, "Insufficient charge for round trip."));
            return;
        }

        drone.State = DroneState.EnRouteToDestination;
        drone.DestinationId = message.DestinationId;
        drone.LaunchTime = DateTime.UtcNow;
        drone.ArrivedAtDestinationTime = null;
        drone.DistanceFromCentre = 0;
        drone.CurrentCargo = 0;
        drone.CargoType = string.Empty;

        MessageBus.Publish(new LaunchedMessage(drone.Id, message.DestinationId));
        PublishFleetStatus();
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
            drone.State = DroneState.Mining;
            drone.ArrivedAtDestinationTime = DateTime.UtcNow;
            MessageBus.Publish(new ArrivedAtDestinationMessage(drone.Id, drone.DestinationId.Value));
        }
    }

    private void HandleDroneMining(DroneInstance drone, double deltaSeconds)
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

        if (field.IsDepleted(drone.CargoType))
        {
            drone.State = DroneState.ReturningToCentre;
            MessageBus.Publish(new MiningCompletedMessage(drone.Id, drone.DestinationId.Value));
            return;
        }

        double minedThisTick = drone.MiningSpeed / field.MiningDifficulty * deltaSeconds;
        double availableCapacity = drone.MaxCargoSize - drone.CurrentCargo;
        double availableToMine = Math.Min(minedThisTick, Math.Min(availableCapacity, field.ResourceAmount));

        if (availableToMine > 0)
        {
            drone.CurrentCargo += availableToMine;
            drone.CargoType = field.ResourceType;
            field.ResourceAmount -= availableToMine;
        }

        if (drone.IsFull || field.IsDepleted(drone.CargoType))
        {
            drone.State = DroneState.ReturningToCentre;
            MessageBus.Publish(new MiningCompletedMessage(drone.Id, drone.DestinationId.Value));
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
            drone.CurrentCharge = drone.MaxCharge;
            drone.CurrentCargo = 0;
            drone.CargoType = string.Empty;

            MessageBus.Publish(new ArrivedAtCentreMessage(drone.Id));
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
            drone.CurrentCargo = 0;
            drone.CargoType = string.Empty;
            drone.CurrentCharge = drone.MaxCharge;
            drone.OutOfChargeTime = 0;
            MessageBus.Publish(new LostMessage(drone.Id));
        }
    }

    private void HandleDroneCharging(DroneInstance drone, double deltaSeconds)
    {
        drone.CurrentCharge += DefaultRechargeRate * deltaSeconds;

        if (drone.CurrentCharge >= drone.MaxCharge)
        {
            drone.CurrentCharge = drone.MaxCharge;
            drone.State = DroneState.Idle;
            MessageBus.Publish(new RechargedMessage(drone.Id));
        }
    }

    private void HandleDroneTick(DroneInstance drone, double deltaSeconds)
    {
        switch (drone.State)
        {
            case DroneState.EnRouteToDestination:
                HandleDroneEnRouteToDestination(drone, deltaSeconds);
                break;
            case DroneState.Mining:
                HandleDroneMining(drone, deltaSeconds);
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
        drone.CurrentCargo = 0;
        drone.CargoType = string.Empty;
        MessageBus.Publish(new ArrivedAtCentreMessage(drone.Id));
    }

    private void TransitionToOutOfCharge(DroneInstance drone)
    {
        drone.State = DroneState.OutOfCharge;
        drone.CurrentCharge = 0;
        MessageBus.Publish(new OutOfChargeMessage(drone.Id));
    }

    private void PublishFleetStatus()
    {
        var snapshot = _drones
            .Select((drone, index) => ToStatusDto(index, drone))
            .ToList();

        MessageBus.Publish(new FleetStatusMessage(snapshot, _dronesLost));
    }

    private DroneStatusDto ToStatusDto(int index, DroneInstance drone)
    {
        string? destinationLabel = null;
        if (drone.DestinationId is Guid destinationId)
        {
            var field = _resourceService.GetResourceFieldById(destinationId);
            if (field is not null)
            {
                destinationLabel = $"{field.ResourceType} field @ {field.DistanceFromCentre:0} km";
            }
        }

        double chargePercent = drone.MaxCharge > 0 ? Math.Clamp(drone.CurrentCharge / drone.MaxCharge, 0, 1) : 0;
        double cargoPercent = drone.MaxCargoSize > 0 ? Math.Clamp(drone.CurrentCargo / drone.MaxCargoSize, 0, 1) : 0;

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
            string.IsNullOrWhiteSpace(drone.CargoType) ? "-" : drone.CargoType,
            Math.Max(0, drone.TotalDistanceTraveled),
            drone.State == DroneState.Idle);
    }
}
