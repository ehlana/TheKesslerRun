using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TheKesslerRun2.Services;
using TheKesslerRun2.Services.Interfaces;
using static TheKesslerRun2.Services.Messages.Scan;

namespace TheKesslerRun2.ViewModels;
internal partial class ScanViewModel : ObservableObject, 
    IMessageReceiver<ScanCompletedMessage>, 
    IMessageReceiver<RechargeCompletedMessage>, 
    IHeartbeatReceiver
{
    public double MaxDistance { get; } = 1000;
    [ObservableProperty]
    private double _rechargeTime = 5.0;

    [ObservableProperty]
    private double _currentRechargeProgress;

    [ObservableProperty]
    private bool _isCharged = true;
    private IMessageBus _messageBus;

    public ScanViewModel(IMessageBus messageBus)
    {
        _messageBus = messageBus;
        Game.Instance.HeartbeatService!.AddReceiver(this);

        messageBus.Subscribe<ScanCompletedMessage>(this);
        messageBus.Subscribe<RechargeCompletedMessage>(this);
    }

    public void Receive(ScanCompletedMessage message)
    {
        IsCharged = false;
        CurrentRechargeProgress = 0;
    }

    public void Receive(RechargeCompletedMessage message)
    {
        IsCharged = true;
        CurrentRechargeProgress = RechargeTime;
    }

    [RelayCommand]
    public void StartScan()
    {
        _messageBus.Publish(new BeginScanMessage());
        CurrentRechargeProgress = 0;
        IsCharged = false;
    }

    public void Tick(double deltaSeconds)
    {
        if (IsCharged) return;
        CurrentRechargeProgress += deltaSeconds;
    }
}
