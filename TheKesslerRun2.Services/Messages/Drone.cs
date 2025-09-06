namespace TheKesslerRun2.Services.Messages;
public static class Drone
{
    public record LaunchMessage(Guid DroneId, Guid DestinationId);
    public record LaunchedMessage(Guid DroneId, Guid DestinationId);
    public record ArrivedAtDestinationMessage(Guid DroneId, Guid DestinationId);
    public record MiningCompletedMessage(Guid DroneId, Guid DestinationId);
    public record ArrivedAtCentreMessage(Guid DroneId);
    public record LostMessage(Guid DroneId);
    public record OutOfChargeMessage(Guid DroneId);
    public record RechargedMessage(Guid DroneId);
}
