using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using TheKesslerRun2.DTOs;
using TheKesslerRun2.Extensions;
using TheKesslerRun2.Services;
using TheKesslerRun2.Services.Interfaces;
using static TheKesslerRun2.Services.Messages.Drone;
using static TheKesslerRun2.Services.Messages.RecyclingCentre;
using static TheKesslerRun2.Services.Messages.Scan;

namespace TheKesslerRun2.ViewModels;

public partial class DronesViewModel : ObservableObject, IHeartbeatReceiver
{
    private readonly IMessageBus _messageBus;
    private readonly List<DroneStatusDto> _lastFleetSnapshot = [];

    private int _lastReportedFleetSeconds = -1;
    private double? _secondsSinceFleetChange;
    private bool _trackingFleetStatusMessage;
    private string _lastFleetSummary = "Awaiting fleet telemetry";
    private int _lastLostCount;

    [ObservableProperty]
    private ObservableCollection<DroneStatusDto> _drones = [];

    [ObservableProperty]
    private ObservableCollection<ResourceFieldDto> _knownFields = [];

    [ObservableProperty]
    private DroneStatusDto? _selectedDrone;

    [ObservableProperty]
    private ResourceFieldDto? _selectedField;

    [ObservableProperty]
    private string _statusMessage = "Awaiting fleet telemetry...";

    [ObservableProperty]
    private int _dronesLost;

    public DronesViewModel(IMessageBus messageBus)
    {
        _messageBus = messageBus;

        Game.Instance.HeartbeatService!.AddReceiver(this);

        messageBus.SubscribeOnUI<FleetStatusMessage>(Receive);
        messageBus.SubscribeOnUI<CompletedMessage>(Receive);
        messageBus.SubscribeOnUI<FieldsKnownMessage>(Receive);
        messageBus.SubscribeOnUI<FieldUpdatedMessage>(Receive);
        messageBus.SubscribeOnUI<LaunchFailedMessage>(Receive);
        messageBus.SubscribeOnUI<LaunchedMessage>(Receive);
        messageBus.SubscribeOnUI<ArrivedAtDestinationMessage>(Receive);
        messageBus.SubscribeOnUI<GatheringCompletedMessage>(Receive);
        messageBus.SubscribeOnUI<ArrivedAtCentreMessage>(Receive);
        messageBus.SubscribeOnUI<OutOfChargeMessage>(Receive);
        messageBus.SubscribeOnUI<LostMessage>(Receive);
        messageBus.SubscribeOnUI<CargoReceivedMessage>(Receive);
        messageBus.SubscribeOnUI<RecallAcknowledgedMessage>(Receive);
        messageBus.SubscribeOnUI<RecallFailedMessage>(Receive);

        _messageBus.Publish(new RequestKnownFieldsMessage());
    }

    public void Receive(FleetStatusMessage message)
    {
        Guid? preferredSelection = SelectedDrone?.Id;
        bool fleetChanged = FleetChanged(message);

        SyncDrones(message.Drones);
        DronesLost = message.LostCount;
        RefreshSelectedDroneReference(preferredSelection);
        StoreFleetSnapshot(message);

        if (fleetChanged)
        {
            _secondsSinceFleetChange = 0;
            _lastReportedFleetSeconds = -1;
            _lastFleetSummary = BuildFleetSummary(message);
            _trackingFleetStatusMessage = true;
            ApplyFleetStatusMessage(force: true);
        }

        SendDroneCommand.NotifyCanExecuteChanged();
        RecallDroneCommand.NotifyCanExecuteChanged();
    }

    public void Receive(CompletedMessage message)
    {
        var fields = message.FieldsInRange.ToList();
        foreach (var field in fields)
        {
            UpsertField(field);
        }

        if (fields.Count > 0)
        {
            SetStatusMessage($"Detected {fields.Count} new debris fields.");
        }

        RefreshSelectedFieldReference();
    }

    public void Receive(FieldsKnownMessage message)
    {
        var fields = message.Fields.ToList();
        ReplaceFields(fields);

        if (fields.Count > 0)
        {
            SetStatusMessage($"Tracking {fields.Count} known debris fields.");
        }
    }

    public void Receive(FieldUpdatedMessage message)
    {
        if (message.Field.ResourceAmount <= 0)
        {
            RemoveField(message.Field.Id);
        }
        else
        {
            UpsertField(message.Field);
        }

        RefreshSelectedFieldReference();
    }

    public void Receive(LaunchFailedMessage message)
    {
        SetStatusMessage(message.Reason);
        RecallDroneCommand.NotifyCanExecuteChanged();
    }

    public void Receive(LaunchedMessage message)
    {
        var field = KnownFields.FirstOrDefault(f => f.Id == message.DestinationId);
        var fieldLabel = field is null
            ? "target"
            : $"{field.ResourceType} field ({field.DistanceFromCentre:0} km)";

        SetStatusMessage($"Drone launched toward {fieldLabel}.");
        RefreshSelectedDroneReference(message.DroneId);
    }

    public void Receive(ArrivedAtDestinationMessage message)
    {
        SetStatusMessage("Drone has arrived at the debris field.");
        RefreshSelectedDroneReference(message.DroneId);
    }

    public void Receive(GatheringCompletedMessage message)
    {
        SetStatusMessage("Drone has completed gathering and is returning.");
        RefreshSelectedDroneReference(message.DroneId);
    }

    public void Receive(ArrivedAtCentreMessage message)
    {
        SetStatusMessage("Drone has returned to the recycling centre.");
        RefreshSelectedDroneReference(message.DroneId);
    }

    public void Receive(OutOfChargeMessage message)
    {
        SetStatusMessage("Drone has run out of charge!");
        RefreshSelectedDroneReference(message.DroneId);
    }

    public void Receive(LostMessage message)
    {
        SetStatusMessage("Contact lost with a drone.");
        RefreshSelectedDroneReference(message.DroneId);
    }

    public void Receive(CargoReceivedMessage message)
    {
        string status;
        if (message.AmountStored > 0)
        {
            var overflowNote = message.Overflow > 0
                ? $" with {message.Overflow:0} units unable to be stored"
                : string.Empty;
            status = $"Drone delivered {message.AmountStored:0} units of {message.Resource.DisplayName}{overflowNote}. Total stored: {message.Resource.Amount:0} units.";
        }
        else
        {
            status = $"Recycling centre could not accept {message.Resource.DisplayName}; storage is full.";
        }

        SetStatusMessage(status);
        RefreshSelectedDroneReference(message.DroneId);
        RecallDroneCommand.NotifyCanExecuteChanged();
    }

    public void Receive(RecallAcknowledgedMessage message)
    {
        RefreshSelectedDroneReference(message.DroneId);
        SetStatusMessage("Recall acknowledged. Drone is returning to the recycling centre.");
        RecallDroneCommand.NotifyCanExecuteChanged();
    }

    public void Receive(RecallFailedMessage message)
    {
        if (SelectedDrone?.Id == message.DroneId)
        {
            SetStatusMessage(message.Reason);
        }

        RecallDroneCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanSendDrone))]
    private void SendDrone()
    {
        if (SelectedDrone is null || SelectedField is null)
        {
            return;
        }

        _messageBus.Publish(new LaunchMessage(SelectedDrone.Id, SelectedField.Id));
        SetStatusMessage($"Requesting launch of {SelectedDrone.DisplayName} to {SelectedField.ResourceType} field.");
    }

    private bool CanSendDrone() => SelectedDrone?.IsIdle == true && SelectedField is not null;

    [RelayCommand(CanExecute = nameof(CanRecallDrone))]
    private void RecallDrone()
    {
        if (SelectedDrone is null || !SelectedDrone.IsRecallable)
        {
            return;
        }

        _messageBus.Publish(new RecallMessage(SelectedDrone.Id));
        SetStatusMessage($"Requested recall of {SelectedDrone.DisplayName}.");
    }

    private bool CanRecallDrone() => SelectedDrone?.IsRecallable == true;

    public void Tick(double deltaSeconds)
    {
        if (!_trackingFleetStatusMessage || _secondsSinceFleetChange is null)
        {
            return;
        }

        _secondsSinceFleetChange += deltaSeconds;
        ApplyFleetStatusMessage();
    }

    private void SyncDrones(IReadOnlyList<DroneStatusDto> updated)
    {
        for (int i = Drones.Count - 1; i >= 0; i--)
        {
            if (!updated.Any(d => d.Id == Drones[i].Id))
            {
                Drones.RemoveAt(i);
            }
        }

        for (int i = 0; i < updated.Count; i++)
        {
            var drone = updated[i];
            int existingIndex = FindDroneIndex(drone.Id);
            if (existingIndex >= 0)
            {
                if (existingIndex != i)
                {
                    Drones.RemoveAt(existingIndex);
                    Drones.Insert(Math.Min(i, Drones.Count), drone);
                }
                else
                {
                    Drones[i] = drone;
                }
            }
            else
            {
                Drones.Insert(Math.Min(i, Drones.Count), drone);
            }
        }
    }

    private void RemoveField(Guid id)
    {
        for (int i = 0; i < KnownFields.Count; i++)
        {
            if (KnownFields[i].Id == id)
            {
                bool wasSelected = SelectedField?.Id == id;
                KnownFields.RemoveAt(i);
                if (wasSelected)
                {
                    SelectedField = null;
                }
                return;
            }
        }
    }

    private void UpsertField(ResourceFieldDto field)
    {
        if (field.ResourceAmount <= 0)
        {
            RemoveField(field.Id);
            return;
        }

        for (int i = 0; i < KnownFields.Count; i++)
        {
            if (KnownFields[i].Id == field.Id)
            {
                KnownFields[i] = field;
                return;
            }
        }

        int insertIndex = 0;
        while (insertIndex < KnownFields.Count && KnownFields[insertIndex].DistanceFromCentre <= field.DistanceFromCentre)
        {
            insertIndex++;
        }

        KnownFields.Insert(insertIndex, field);
    }

    private void ReplaceFields(IEnumerable<ResourceFieldDto> fields)
    {
        KnownFields.Clear();

        foreach (var field in fields.OrderBy(f => f.DistanceFromCentre))
        {
            KnownFields.Add(field);
        }

        RefreshSelectedFieldReference();
    }

    private int FindDroneIndex(Guid id)
    {
        for (int i = 0; i < Drones.Count; i++)
        {
            if (Drones[i].Id == id)
            {
                return i;
            }
        }

        return -1;
    }

    private void RefreshSelectedDroneReference(Guid? preferredId = null)
    {
        var id = preferredId ?? SelectedDrone?.Id;
        if (id is null)
        {
            return;
        }

        var updated = Drones.FirstOrDefault(d => d.Id == id);
        if (updated is not null)
        {
            SelectedDrone = updated;
        }
    }

    private void RefreshSelectedFieldReference()
    {
        if (SelectedField is null)
        {
            return;
        }

        var updated = KnownFields.FirstOrDefault(f => f.Id == SelectedField.Id);
        SelectedField = updated;
    }

    private bool FleetChanged(FleetStatusMessage message)
    {
        if (_lastFleetSnapshot.Count != message.Drones.Count || _lastLostCount != message.LostCount)
        {
            return true;
        }

        for (int i = 0; i < message.Drones.Count; i++)
        {
            if (!_lastFleetSnapshot[i].Equals(message.Drones[i]))
            {
                return true;
            }
        }

        return false;
    }

    private void StoreFleetSnapshot(FleetStatusMessage message)
    {
        _lastFleetSnapshot.Clear();
        foreach (var drone in message.Drones)
        {
            _lastFleetSnapshot.Add(drone);
        }

        _lastLostCount = message.LostCount;
    }

    private string BuildFleetSummary(FleetStatusMessage message)
    {
        int total = message.Drones.Count;
        int idle = message.Drones.Count(d => d.IsIdle);
        return total == 0
            ? "No drones available"
            : $"{idle}/{total} drones idle";
    }

    private void ApplyFleetStatusMessage(bool force = false)
    {
        if (!_trackingFleetStatusMessage || _secondsSinceFleetChange is null)
        {
            return;
        }

        int seconds = (int)Math.Floor(_secondsSinceFleetChange.Value);
        if (!force && seconds == _lastReportedFleetSeconds)
        {
            return;
        }

        _lastReportedFleetSeconds = seconds;
        StatusMessage = $"{_lastFleetSummary} ({seconds}s since last change)";
    }

    private void SetStatusMessage(string message)
    {
        _trackingFleetStatusMessage = false;
        StatusMessage = message;
    }

    partial void OnSelectedDroneChanged(DroneStatusDto? value)
    {
        SendDroneCommand.NotifyCanExecuteChanged();
        RecallDroneCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedFieldChanged(ResourceFieldDto? value)
    {
        SendDroneCommand.NotifyCanExecuteChanged();
        RecallDroneCommand.NotifyCanExecuteChanged();
    }
}
