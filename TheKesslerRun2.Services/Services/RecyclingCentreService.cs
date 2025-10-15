using System.Linq;
using TheKesslerRun2.DTOs;
using TheKesslerRun2.Services.Model;
using static TheKesslerRun2.Services.Messages.RecyclingCentre;

namespace TheKesslerRun2.Services.Services;

internal class RecyclingCentreService : BaseService
{
    private readonly RecyclingSettings _recyclingSettings;
    private readonly List<RecyclingBin> _bins = [];
    private double _credits;

    public RecyclingCentreService()
    {
        _recyclingSettings = SettingsManager.Instance.Recycling;

        for (int i = 0; i < _recyclingSettings.BinCount; i++)
        {
            _bins.Add(new RecyclingBin(i, _recyclingSettings.BinCapacity));
        }
    }

    protected override void OnTick()
    {
        // No recurring processing required for the recycling centre yet.
    }

    protected override void SubscribeToMessages()
    {
        MessageBus.Instance.Subscribe<DepositCargoMessage>(Receive);
        MessageBus.Instance.Subscribe<RequestInventorySnapshotMessage>(Receive);
        MessageBus.Instance.Subscribe<SellBinRequestMessage>(Receive);
    }

    public void Receive(DepositCargoMessage message)
    {
        if (message.Amount <= 0 || string.IsNullOrWhiteSpace(message.ResourceId))
        {
            PublishSnapshot();
            return;
        }

        var definition = ResourceManager.Instance.Get(message.ResourceId);
        string resourceName = definition?.DisplayName ?? message.ResourceId;
        double unitValue = definition?.BaseValue ?? 0;

        double remaining = Math.Max(0, message.Amount);

        foreach (var bin in _bins.Where(b => b.ResourceMatches(message.ResourceId) && !b.IsFull))
        {
            remaining = bin.Fill(message.ResourceId, resourceName, unitValue, remaining);
            if (remaining <= 0)
            {
                break;
            }
        }

        if (remaining > 0)
        {
            foreach (var bin in _bins.Where(b => b.IsEmpty))
            {
                remaining = bin.Fill(message.ResourceId, resourceName, unitValue, remaining);
                if (remaining <= 0)
                {
                    break;
                }
            }
        }

        double stored = Math.Max(0, message.Amount - remaining);
        double overflow = Math.Max(0, remaining);
        var summary = CreateResourceSummary(message.ResourceId);

        PublishSnapshot();
        MessageBus.Instance.Publish(new CargoReceivedMessage(message.DroneId, summary, message.Amount, stored, overflow));
    }

    public void Receive(RequestInventorySnapshotMessage _)
    {
        PublishSnapshot();
    }

    public void Receive(SellBinRequestMessage message)
    {
        var bin = _bins.FirstOrDefault(b => b.Id == message.BinId);
        if (bin is null)
        {
            return;
        }

        double amountSold = bin.Amount;
        double saleValue = amountSold * bin.UnitValue;
        string? resourceId = bin.ResourceId;
        string? resourceName = bin.ResourceName;

        bin.Clear();
        _credits += saleValue;

        PublishSnapshot();
        MessageBus.Instance.Publish(new BinSoldMessage(message.BinId, resourceId, resourceName, amountSold, saleValue, _credits));
    }

    private void PublishSnapshot()
    {
        var bins = _bins
            .Select(b => b.ToDto())
            .ToList();

        double totalAmount = bins.Sum(b => b.Amount);
        double totalValue = bins.Sum(b => b.TotalValue);
        int occupied = _bins.Count(b => !b.IsEmpty);

        var snapshot = new RecyclingCentreSnapshotDto(
            bins,
            totalAmount,
            totalValue,
            _credits,
            _bins.Count,
            occupied,
            _recyclingSettings.BinCapacity);

        MessageBus.Instance.Publish(new InventorySnapshotMessage(snapshot));
    }

    internal RecyclingCentreSnapshot CaptureSnapshot()
    {
        var bins = _bins
            .Select(b => new RecyclingBinSnapshot(b.Id, b.Label, b.ResourceId, b.ResourceName, b.UnitValue, b.Amount, b.Capacity))
            .ToList();

        return new RecyclingCentreSnapshot(_credits, bins);
    }

    internal void RestoreSnapshot(RecyclingCentreSnapshot snapshot)
    {
        _credits = snapshot.Credits;

        for (int i = 0; i < _bins.Count; i++)
        {
            if (i < snapshot.Bins.Count)
            {
                _bins[i].Restore(snapshot.Bins[i]);
            }
            else
            {
                _bins[i].Clear();
            }
        }

        PublishSnapshot();
    }

    internal void BroadcastInventory() => PublishSnapshot();

    private RecyclingResourceDto CreateResourceSummary(string resourceId)
    {
        var definition = ResourceManager.Instance.Get(resourceId);
        string resourceName = definition?.DisplayName ?? resourceId;
        double unitValue = definition?.BaseValue ?? 0;

        double totalAmount = _bins
            .Where(b => b.ResourceMatches(resourceId))
            .Sum(b => b.Amount);

        double totalValue = totalAmount * unitValue;
        return new RecyclingResourceDto(resourceId, resourceName, totalAmount, unitValue, totalValue);
    }

    private sealed class RecyclingBin
    {
        public RecyclingBin(int index, double capacity)
        {
            Id = Guid.NewGuid();
            Index = index;
            Capacity = capacity;
            Label = $"Bin {index + 1}";
        }

        public Guid Id { get; }
        public int Index { get; }
        public double Capacity { get; }
        public string Label { get; }
        public string? ResourceId { get; private set; }
        public string? ResourceName { get; private set; }
        public double UnitValue { get; private set; }
        public double Amount { get; private set; }
        public double RemainingCapacity => Math.Max(0, Capacity - Amount);
        public bool IsEmpty => Amount <= 0.0001;
        public bool IsFull => RemainingCapacity <= 0.0001;

        public bool ResourceMatches(string resourceId) =>
            !IsEmpty && string.Equals(ResourceId, resourceId, StringComparison.OrdinalIgnoreCase);

        public double Fill(string resourceId, string resourceName, double unitValue, double amount)
        {
            if (amount <= 0)
            {
                return amount;
            }

            if (IsEmpty)
            {
                ResourceId = resourceId;
                ResourceName = resourceName;
                UnitValue = unitValue;
            }
            else if (!string.Equals(ResourceId, resourceId, StringComparison.OrdinalIgnoreCase))
            {
                return amount;
            }

            if (IsFull)
            {
                return amount;
            }

            double accepted = Math.Min(RemainingCapacity, amount);
            Amount += accepted;
            return amount - accepted;
        }

        public void Clear()
        {
            Amount = 0;
            ResourceId = null;
            ResourceName = null;
            UnitValue = 0;
        }

        public void Restore(RecyclingBinSnapshot snapshot)
        {
            ResourceId = snapshot.ResourceId;
            ResourceName = snapshot.ResourceName;
            UnitValue = snapshot.UnitValue;
            Amount = Math.Min(snapshot.Amount, Capacity);
        }

        public RecyclingBinDto ToDto()
        {
            double fill = Capacity <= 0 ? 0 : Math.Clamp(Amount / Capacity, 0, 1);
            return new RecyclingBinDto(
                Id,
                Label,
                ResourceId,
                ResourceName,
                Amount,
                Capacity,
                UnitValue,
                Amount * UnitValue,
                fill,
                RemainingCapacity);
        }
    }
}
