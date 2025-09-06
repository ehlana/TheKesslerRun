using TheKesslerRun2.Services.Interfaces;
using TheKesslerRun2.Services.Messages;

namespace TheKesslerRun2.Services.Services;
internal class ScanService : BaseService, IMessageReceiver<Scan.BeginScanMessage>
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
        // Begin the scan process
        _secTilCharge = _chargeInterval;
        MessageBus.Publish(new Scan.ScanCompletedMessage());
    }
}
