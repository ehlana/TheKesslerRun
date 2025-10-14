using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using TheKesslerRun2.DTOs;
using TheKesslerRun2.Services.Interfaces;
using static TheKesslerRun2.Services.Messages.RecyclingCentre;

namespace TheKesslerRun2.ViewModels;

public partial class RecyclingCentreViewModel : ObservableObject,
    IMessageReceiver<InventorySnapshotMessage>,
    IMessageReceiver<CargoReceivedMessage>
{
    private readonly IMessageBus _messageBus;

    [ObservableProperty]
    private ObservableCollection<RecyclingResourceDto> _resources = [];

    [ObservableProperty]
    private string _statusMessage = "Awaiting inventory snapshot...";

    [ObservableProperty]
    private string _lastDeliveryMessage = "No deliveries yet.";

    [ObservableProperty]
    private double _totalStoredAmount;

    [ObservableProperty]
    private double _totalStoredValue;

    public RecyclingCentreViewModel(IMessageBus messageBus)
    {
        _messageBus = messageBus;

        messageBus.Subscribe<InventorySnapshotMessage>(this);
        messageBus.Subscribe<CargoReceivedMessage>(this);

        _messageBus.Publish(new RequestInventorySnapshotMessage());
    }

    public void Receive(InventorySnapshotMessage message)
    {
        ReplaceResources(message.Resources);
    }

    public void Receive(CargoReceivedMessage message)
    {
        if (message.AmountDelivered <= 0)
        {
            return;
        }

        LastDeliveryMessage =
            $"Received {message.AmountDelivered:0} units of {message.Resource.DisplayName}. Total stored: {message.Resource.Amount:0} units ({message.Resource.TotalValue:C}).";
    }

    private void ReplaceResources(IReadOnlyList<RecyclingResourceDto> resources)
    {
        Resources.Clear();
        foreach (var resource in resources)
        {
            Resources.Add(resource);
        }

        TotalStoredAmount = resources.Sum(r => r.Amount);
        TotalStoredValue = resources.Sum(r => r.TotalValue);

        StatusMessage = resources.Count == 0
            ? "No materials stored at the recycling centre."
            : $"Tracking {resources.Count} stored resource type{(resources.Count == 1 ? string.Empty : "s")}.";
    }
}
