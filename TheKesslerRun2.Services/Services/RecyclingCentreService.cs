using System.Collections.Generic;
using System.Linq;
using TheKesslerRun2.DTOs;
using TheKesslerRun2.Services.Interfaces;
using static TheKesslerRun2.Services.Messages.RecyclingCentre;

namespace TheKesslerRun2.Services.Services;

internal class RecyclingCentreService : BaseService,
    IMessageReceiver<DepositCargoMessage>,
    IMessageReceiver<RequestInventorySnapshotMessage>
{
    private readonly Dictionary<string, double> _inventory = new();

    protected override void OnTick()
    {
        // No recurring processing required for the recycling centre yet.
    }

    public void Receive(DepositCargoMessage message)
    {
        if (message.Amount <= 0 || string.IsNullOrWhiteSpace(message.ResourceId))
        {
            PublishSnapshot();
            return;
        }

        if (_inventory.TryGetValue(message.ResourceId, out var existing))
        {
            _inventory[message.ResourceId] = existing + message.Amount;
        }
        else
        {
            _inventory[message.ResourceId] = message.Amount;
        }

        var updatedResource = CreateDto(message.ResourceId, _inventory[message.ResourceId]);
        PublishSnapshot();
        MessageBus.Publish(new CargoReceivedMessage(message.DroneId, updatedResource, message.Amount));
    }

    public void Receive(RequestInventorySnapshotMessage message)
    {
        PublishSnapshot();
    }

    private void PublishSnapshot()
    {
        var snapshot = _inventory
            .Select(pair => CreateDto(pair.Key, pair.Value))
            .OrderBy(dto => dto.DisplayName)
            .ToList();

        MessageBus.Publish(new InventorySnapshotMessage(snapshot));
    }

    private RecyclingResourceDto CreateDto(string resourceId, double amount)
    {
        var definition = ResourceManager.Instance.Get(resourceId);
        string displayName = definition?.DisplayName ?? resourceId;
        double unitValue = definition?.BaseValue ?? 0;
        double totalValue = unitValue * amount;
        return new RecyclingResourceDto(resourceId, displayName, amount, unitValue, totalValue);
    }
}
