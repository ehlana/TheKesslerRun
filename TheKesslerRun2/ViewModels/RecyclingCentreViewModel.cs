using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Linq;
using TheKesslerRun2.DTOs;
using TheKesslerRun2.Extensions;
using TheKesslerRun2.Services.Interfaces;
using static TheKesslerRun2.Services.Messages.RecyclingCentre;

namespace TheKesslerRun2.ViewModels;

public partial class RecyclingCentreViewModel : ObservableObject
{
    private readonly IMessageBus _messageBus;

    [ObservableProperty]
    private ObservableCollection<RecyclingBinDto> _bins = [];

    [ObservableProperty]
    private RecyclingBinDto? _selectedBin;

    [ObservableProperty]
    private string _statusMessage = "Awaiting recycling centre status...";

    [ObservableProperty]
    private string _lastDeliveryMessage = "No deliveries yet.";

    [ObservableProperty]
    private double _totalStoredAmount;

    [ObservableProperty]
    private double _totalStoredValue;

    [ObservableProperty]
    private double _credits;

    [ObservableProperty]
    private int _binCount;

    [ObservableProperty]
    private int _occupiedBins;

    [ObservableProperty]
    private double _binCapacity;

    public RecyclingCentreViewModel(IMessageBus messageBus)
    {
        _messageBus = messageBus;

        messageBus.SubscribeOnUI<InventorySnapshotMessage>(Receive);
        messageBus.SubscribeOnUI<CargoReceivedMessage>(Receive);
        messageBus.SubscribeOnUI<BinSoldMessage>(Receive);

        _messageBus.Publish(new RequestInventorySnapshotMessage());
    }

    public void Receive(InventorySnapshotMessage message)
    {
        ApplySnapshot(message.Snapshot);
    }

    public void Receive(CargoReceivedMessage message)
    {
        if (message.AmountStored > 0)
        {
            var overflowNote = message.Overflow > 0
                ? $" ({message.Overflow:0} units overflowed capacity)"
                : string.Empty;

            LastDeliveryMessage =
                $"Stored {message.AmountStored:0} units of {message.Resource.DisplayName}{overflowNote}. Total stored: {message.Resource.Amount:0} units ({message.Resource.TotalValue:0.00}cr).";
        }
        else if (message.AmountDelivered > 0)
        {
            LastDeliveryMessage =
                $"Unable to store {message.AmountDelivered:0} units of {message.Resource.DisplayName}. All bins are full.";
        }
    }

    public void Receive(BinSoldMessage message)
    {
        if (message.AmountSold > 0)
        {
            var resourceLabel = string.IsNullOrWhiteSpace(message.ResourceName)
                ? "unknown resource"
                : message.ResourceName;

            LastDeliveryMessage =
                $"Sold {message.AmountSold:0} units of {resourceLabel} for {message.SaleValue:0.00}cr. Credits balance: {message.CreditsAfterSale:0.00}cr.";
        }
        else
        {
            LastDeliveryMessage = "Selected bin is empty. Nothing to sell.";
        }
    }

    [RelayCommand(CanExecute = nameof(CanSellSelectedBin))]
    private void SellSelectedBin()
    {
        if (SelectedBin is null)
        {
            return;
        }

        _messageBus.Publish(new SellBinRequestMessage(SelectedBin.Id));
    }

    private void ApplySnapshot(RecyclingCentreSnapshotDto snapshot)
    {
        BinCount = snapshot.BinCount;
        OccupiedBins = snapshot.OccupiedBins;
        BinCapacity = snapshot.BinCapacity;

        TotalStoredAmount = snapshot.TotalStoredAmount;
        TotalStoredValue = snapshot.TotalStoredValue;
        Credits = snapshot.Credits;

        var selectedId = SelectedBin?.Id;

        Bins = new ObservableCollection<RecyclingBinDto>(snapshot.Bins);

        SelectedBin = Bins.FirstOrDefault(b => b.Id == selectedId);

        StatusMessage = BuildStatusMessage(snapshot);
        SellSelectedBinCommand.NotifyCanExecuteChanged();
    }

    private bool CanSellSelectedBin() => SelectedBin?.Amount > 0.0001;

    private string BuildStatusMessage(RecyclingCentreSnapshotDto snapshot)
    {
        if (snapshot.BinCount <= 0)
        {
            return "Recycling centre bins unavailable.";
        }

        return $"{snapshot.OccupiedBins}/{snapshot.BinCount} bins in use. Stored value {snapshot.TotalStoredValue:0.00}cr.";
    }

    partial void OnSelectedBinChanged(RecyclingBinDto? value)
    {
        SellSelectedBinCommand.NotifyCanExecuteChanged();
    }
}

