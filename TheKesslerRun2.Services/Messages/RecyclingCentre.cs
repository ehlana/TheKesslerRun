using System;
using TheKesslerRun2.DTOs;

namespace TheKesslerRun2.Services.Messages;

public static class RecyclingCentre
{
    public record DepositCargoMessage(Guid DroneId, string ResourceId, double Amount);
    public record CargoReceivedMessage(Guid DroneId, RecyclingResourceDto Resource, double AmountDelivered, double AmountStored, double Overflow);
    public record InventorySnapshotMessage(RecyclingCentreSnapshotDto Snapshot);
    public record RequestInventorySnapshotMessage();
    public record SellBinRequestMessage(Guid BinId);
    public record BinSoldMessage(Guid BinId, string? ResourceId, string? ResourceName, double AmountSold, double SaleValue, double CreditsAfterSale);
}
