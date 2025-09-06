using TheKesslerRun2.Services.Interfaces;
using TheKesslerRun2.Services.Model;
using static TheKesslerRun2.Services.Messages.Drone;

namespace TheKesslerRun2.Services.Services;
internal partial class DronesService : BaseService, IMessageReceiver<LaunchMessage>
{
    private double _defaultRechargeRate = 10.0;
    private int _dronesLost = 0;
    private ResourceFieldService _resourceService = ResourceFieldService.Instance;
    private List<DroneInstance> _drones = [];

    public DronesService() : base()
    {
        for (int i = 0; i < 3; i++)
        {
            _drones.Add(new DroneInstance());
        }
    }

    public void Receive(LaunchMessage message)
    {
        // find first idle drone
        var drone = _drones.FirstOrDefault(d => d.State == DroneState.Idle);
        if (drone is null) return;

        // configure and launch drone
        drone.State = DroneState.EnRouteToDestination;
        drone.DestinationId = message.DestinationId;
        drone.LaunchTime = DateTime.UtcNow;
        drone.ArrivedAtDestinationTime = null;

        // notify UI / services
        MessageBus.Publish(new LaunchedMessage(drone.Id, message.DestinationId));
    }

    private void HandleDroneEnRouteToDestination(DroneInstance drone, double deltaSeconds)
    {
        var field = _resourceService.GetResourceFieldById(drone.DestinationId!.Value);
        if (field is null)
        {
            // if the field is gone, just reset distance traveled to 0
            drone.TotalDistanceTraveled = 0;
            drone.State = DroneState.Idle;
            drone.DestinationId = null;
            MessageBus.Publish(new ArrivedAtCentreMessage(drone.Id));
            return;
        }

        double distanceRemaining = field.DistanceFromCentre - drone.DistanceFromCentre;
        double travel = drone.Speed * deltaSeconds;
        if (travel >= distanceRemaining)
        {
            // arrived at destination
            drone.TotalDistanceTraveled += travel;
            drone.State = DroneState.Mining;
            drone.ArrivedAtDestinationTime = DateTime.UtcNow;
            // notify UI
            MessageBus.Publish(new ArrivedAtDestinationMessage(drone.Id, drone.DestinationId!.Value));
        }
        else
        {
            drone.DistanceFromCentre += travel;
            drone.TotalDistanceTraveled += travel;
        }

        double costPerUnit = drone.BaseChargePerUnitDistance * (1 + (drone.LoadedChargeMultiplier - 1) * drone.LoadedPc);
        drone.CurrentCharge -= travel * costPerUnit;

        if (drone.CurrentCharge <= 0)
        {
            // drone is out of charge
            drone.State = DroneState.OutOfCharge;
            drone.CurrentCharge = 0;
            MessageBus.Publish(new OutOfChargeMessage(drone.Id));
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
            MessageBus.Publish(new MiningCompletedMessage(drone.Id, drone.DestinationId!.Value));
            return;
        }

        double minedThisTick = drone.MiningSpeed / field.MiningDifficulty * deltaSeconds;
        double availableToMine = Math.Min(minedThisTick, Math.Min(drone.MaxCargoSize - drone.CurrentCargo, field.ResourceAmount));
        if (availableToMine > 0)
        {
            drone.CurrentCargo += availableToMine;
            drone.CargoType = field.ResourceType;

            field.ResourceAmount -= availableToMine;
        }

        if (drone.IsFull || field.IsDepleted(drone.CargoType))
        {
            drone.State = DroneState.ReturningToCentre;
            MessageBus.Publish(new MiningCompletedMessage(drone.Id, drone.DestinationId!.Value));
        }
    }

    private void HandleDroneReturningToCentre(DroneInstance drone, double deltaSeconds)
    {
        var field = _resourceService.GetResourceFieldById(drone.DestinationId!.Value);
        if (field is null)
        {
            // if the field is gone, just reset distance traveled to 0
            drone.State = DroneState.Idle;
            drone.DistanceFromCentre = 0;
            drone.DestinationId = null;

            MessageBus.Publish(new ArrivedAtCentreMessage(drone.Id));
            return;
        }

        double returnDistanceRemaining = field!.DistanceFromCentre;
        double returnTravel = drone.Speed * deltaSeconds;
        if (returnTravel >= returnDistanceRemaining)
        {
            drone.TotalDistanceTraveled += returnTravel;
            drone.State = DroneState.Charging;
            drone.DistanceFromCentre = 0;
            drone.DestinationId = null;
            drone.CurrentCharge = drone.MaxCharge;  // Instant recharge on return for simplicity

            MessageBus.Publish(new ArrivedAtCentreMessage(drone.Id));
        }
        else
        {
            drone.TotalDistanceTraveled += returnTravel;
            drone.DistanceFromCentre -= returnTravel;
            double costPerUnit = drone.BaseChargePerUnitDistance * (1 + (drone.LoadedChargeMultiplier - 1) * drone.LoadedPc);
            drone.CurrentCharge -= returnTravel * costPerUnit;

            if (drone.CurrentCharge <= 0)
            {
                // drone is out of charge
                drone.State = DroneState.OutOfCharge;
                drone.CurrentCharge = 0;
                MessageBus.Publish(new OutOfChargeMessage(drone.Id));
            }
        }
    }

    private void HandleDroneOutOfCharge(DroneInstance drone, double deltaSeconds)
    {
        drone.OutOfChargeTime += deltaSeconds;
        if (drone.OutOfChargeTime >= drone.MaxOutOfChargeTime)
        {
            // Drone is considered lost after max out of charge time
            drone.State = DroneState.Lost;  // We set to Lost, and then next tick, it will be deleted.
            drone.DistanceFromCentre = 0;
            drone.DestinationId = null;
            drone.CurrentCargo = 0;
            drone.CargoType = string.Empty;
            drone.CurrentCharge = drone.MaxCharge; // Reset charge for simplicity
            drone.OutOfChargeTime = 0;
            MessageBus.Publish(new LostMessage(drone.Id));
        }
    }

    private void HandleDroneCharging(DroneInstance drone, double deltaSeconds)
    {
        drone.CurrentCharge += _defaultRechargeRate * deltaSeconds;

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
            case DroneState.EnRouteToDestination: HandleDroneEnRouteToDestination(drone, deltaSeconds); break;
            case DroneState.Mining: HandleDroneMining(drone, deltaSeconds); break;
            case DroneState.ReturningToCentre: HandleDroneReturningToCentre(drone, deltaSeconds); break;
            case DroneState.OutOfCharge: HandleDroneOutOfCharge(drone, deltaSeconds); break;
            case DroneState.Charging: HandleDroneCharging(drone, deltaSeconds); break;

            case DroneState.Idle:
            case DroneState.Lost:
            default:
                // nothing to do
                break;
        }
    }

    protected override void OnTick()
    {
        var lostDrones = _drones.Where(d => d.State == DroneState.Lost).ToList();
        _dronesLost += lostDrones.Count;
        _drones.RemoveAll(d => d.State == DroneState.Lost);// urgh! have to re-do the list to avoid modifying while iterating???
        foreach (var drone in _drones)
        {
            HandleDroneTick(drone, Threshold);
        }
    }
}
