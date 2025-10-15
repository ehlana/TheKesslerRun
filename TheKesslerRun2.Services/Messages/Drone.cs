using System.Collections.Generic;
using TheKesslerRun2.DTOs;

namespace TheKesslerRun2.Services.Messages;

public static class Drone
{
    public record LaunchMessage(Guid DroneId, Guid DestinationId);
    public record LaunchFailedMessage(Guid DroneId, Guid DestinationId, string Reason);
    public record LaunchedMessage(Guid DroneId, Guid DestinationId);
    public record ArrivedAtDestinationMessage(Guid DroneId, Guid DestinationId);
    public record GatheringCompletedMessage(Guid DroneId, Guid DestinationId);
    public record ArrivedAtCentreMessage(Guid DroneId);
    public record LostMessage(Guid DroneId);
    public record OutOfChargeMessage(Guid DroneId);
    public record RechargedMessage(Guid DroneId);
    public record RecallMessage(Guid DroneId);
    public record RecallAcknowledgedMessage(Guid DroneId, Guid? DestinationId);
    public record RecallFailedMessage(Guid DroneId, string Reason);
    public record FleetStatusMessage(IReadOnlyList<DroneStatusDto> Drones, int LostCount);
}
