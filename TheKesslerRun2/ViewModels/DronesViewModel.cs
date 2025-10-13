using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Linq;
using TheKesslerRun2.DTOs;
using TheKesslerRun2.Services.Interfaces;
using static TheKesslerRun2.Services.Messages.Drone;
using static TheKesslerRun2.Services.Messages.Scan;

namespace TheKesslerRun2.ViewModels;

public partial class DronesViewModel : ObservableObject,
    IMessageReceiver<FleetStatusMessage>,
    IMessageReceiver<CompletedMessage>,
    IMessageReceiver<LaunchFailedMessage>,
    IMessageReceiver<LaunchedMessage>,
    IMessageReceiver<ArrivedAtDestinationMessage>,
    IMessageReceiver<MiningCompletedMessage>,
    IMessageReceiver<ArrivedAtCentreMessage>,
    IMessageReceiver<OutOfChargeMessage>,
    IMessageReceiver<LostMessage>
{
    private readonly IMessageBus _messageBus;

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

        messageBus.Subscribe<FleetStatusMessage>(this);
        messageBus.Subscribe<CompletedMessage>(this);
        messageBus.Subscribe<LaunchFailedMessage>(this);
        messageBus.Subscribe<LaunchedMessage>(this);
        messageBus.Subscribe<ArrivedAtDestinationMessage>(this);
        messageBus.Subscribe<MiningCompletedMessage>(this);
        messageBus.Subscribe<ArrivedAtCentreMessage>(this);
        messageBus.Subscribe<OutOfChargeMessage>(this);
        messageBus.Subscribe<LostMessage>(this);
    }

    public void Receive(FleetStatusMessage message)
    {
        SyncDrones(message.Drones);
        DronesLost = message.LostCount;
        RefreshSelectedDroneReference();
        SendDroneCommand.NotifyCanExecuteChanged();
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
            StatusMessage = $"Detected {fields.Count} new debris fields.";
        }
    }

    public void Receive(LaunchFailedMessage message)
    {
        StatusMessage = message.Reason;
    }

    public void Receive(LaunchedMessage message)
    {
        var field = _knownFields.FirstOrDefault(f => f.Id == message.DestinationId);
        var fieldLabel = field is null
            ? "target"
            : $"{field.ResourceType} field ({field.DistanceFromCentre:0} km)";

        StatusMessage = $"Drone launched toward {fieldLabel}.";
        RefreshSelectedDroneReference(message.DroneId);
    }

    public void Receive(ArrivedAtDestinationMessage message)
    {
        StatusMessage = "Drone has arrived at the debris field.";
        RefreshSelectedDroneReference(message.DroneId);
    }

    public void Receive(MiningCompletedMessage message)
    {
        StatusMessage = "Drone has completed mining and is returning.";
        RefreshSelectedDroneReference(message.DroneId);
    }

    public void Receive(ArrivedAtCentreMessage message)
    {
        StatusMessage = "Drone has returned to the recycling centre.";
        RefreshSelectedDroneReference(message.DroneId);
    }

    public void Receive(OutOfChargeMessage message)
    {
        StatusMessage = "Drone has run out of charge!";
        RefreshSelectedDroneReference(message.DroneId);
    }

    public void Receive(LostMessage message)
    {
        StatusMessage = "Contact lost with a drone.";
        RefreshSelectedDroneReference(message.DroneId);
    }

    [RelayCommand(CanExecute = nameof(CanSendDrone))]
    private void SendDrone()
    {
        if (SelectedDrone is null || SelectedField is null)
        {
            return;
        }

        _messageBus.Publish(new LaunchMessage(SelectedDrone.Id, SelectedField.Id));
        StatusMessage = $"Requesting launch of {SelectedDrone.DisplayName} to {SelectedField.ResourceType} field.";
    }

    private bool CanSendDrone() => SelectedDrone?.IsIdle == true && SelectedField is not null;

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

    private void UpsertField(ResourceFieldDto field)
    {
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

    partial void OnSelectedDroneChanged(DroneStatusDto? value)
    {
        SendDroneCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedFieldChanged(ResourceFieldDto? value)
    {
        SendDroneCommand.NotifyCanExecuteChanged();
    }
}
