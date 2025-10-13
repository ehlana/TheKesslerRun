using TheKesslerRun2.Services.Interfaces;
using TheKesslerRun2.Services.Messages;

namespace TheKesslerRun2.Services.Services;
internal class ScanService : BaseService,
    IMessageReceiver<Scan.BeginScanMessage>,
    IMessageReceiver<Scan.RequestKnownFieldsMessage>
{
    private double _secTilCharge = 0.0;
    private double _chargeInterval = 5.0;
    private double _scanRange = 1000.0;

    protected override void OnTick()
    {
        if (_secTilCharge > 0)
        {
            _secTilCharge -= Threshold;
            if (_secTilCharge <= 0)
            {
                // Scan charge is complete
                MessageBus.Publish(new Scan.RechargeCompletedMessage());
                _secTilCharge = 0;
            }
        }
    }

    public void Receive(Scan.BeginScanMessage message)
    {
        _secTilCharge = _chargeInterval;

        var fieldsInRange = ResourceFieldService.Instance.FindNewFieldsInRange(_scanRange)
            .Select(f => new DTOs.ResourceFieldDto(f.Id, f.ResourceAmount, f.ResourceType, f.MiningDifficulty, f.DistanceFromCentre))
            .ToList();

        MessageBus.Publish(new Scan.CompletedMessage(fieldsInRange));
        BroadcastKnownFields();
    }

    public void Receive(Scan.RequestKnownFieldsMessage message)
    {
        BroadcastKnownFields();
    }

    private void BroadcastKnownFields()
    {
        var allFields = ResourceFieldService.Instance.GetAllFields()
            .Select(f => new DTOs.ResourceFieldDto(f.Id, f.ResourceAmount, f.ResourceType, f.MiningDifficulty, f.DistanceFromCentre))
            .ToList();

        MessageBus.Publish(new Scan.FieldsKnownMessage(allFields));
    }
}
