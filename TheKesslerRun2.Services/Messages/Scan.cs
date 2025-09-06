using TheKesslerRun2.DTOs;

namespace TheKesslerRun2.Services.Messages;
public static class Scan
{
    public record BeginScanMessage;
    public record CompletedMessage(IEnumerable<ResourceFieldDto> FieldsInRange);
    public record RechargeCompletedMessage;
}
