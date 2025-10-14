using System;
using System.Collections.Generic;
using TheKesslerRun2.DTOs;

namespace TheKesslerRun2.Services.Messages;

public static class RecyclingCentre
{
    public record DepositCargoMessage(Guid DroneId, string ResourceId, double Amount);
    public record CargoReceivedMessage(Guid DroneId, RecyclingResourceDto Resource, double AmountDelivered);
    public record InventorySnapshotMessage(IReadOnlyList<RecyclingResourceDto> Resources);
    public record RequestInventorySnapshotMessage();
}
