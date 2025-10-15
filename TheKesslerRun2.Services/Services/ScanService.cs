using TheKesslerRun2.Services.Messages;

namespace TheKesslerRun2.Services.Services;
internal class ScanService : BaseService
{
    private double _secTilCharge = 0.0;
    private double _chargeInterval = 5.0;
    private double _scanRange = 1000.0;

    protected override void SubscribeToMessages()
    {
        MessageBus.Instance.Subscribe<Scan.BeginScanMessage>(Receive);
        MessageBus.Instance.Subscribe<Scan.RequestKnownFieldsMessage>(Receive);
    }

    protected override void OnTick()
    {
        if (_secTilCharge > 0)
        {
            _secTilCharge -= Threshold;
            if (_secTilCharge <= 0)
            {
                // Scan charge is complete
                MessageBus.Instance.Publish(new Scan.RechargeCompletedMessage());
                _secTilCharge = 0;
            }
        }
    }

    public void Receive(Scan.BeginScanMessage message)
    {
        _secTilCharge = _chargeInterval;

        var fieldsInRange = ResourceFieldService.Instance.FindNewFieldsInRange(_scanRange)
            .Select(ResourceFieldMapper.ToDto)
            .ToList();

        MessageBus.Instance.Publish(new Scan.CompletedMessage(fieldsInRange));
        BroadcastKnownFields();
    }

    public void Receive(Scan.RequestKnownFieldsMessage message)
    {
        BroadcastKnownFields();
    }

    private void BroadcastKnownFields()
    {
        var allFields = ResourceFieldService.Instance.GetAllFields()
            .Select(ResourceFieldMapper.ToDto)
            .ToList();

        MessageBus.Instance.Publish(new Scan.FieldsKnownMessage(allFields));
    }

}
