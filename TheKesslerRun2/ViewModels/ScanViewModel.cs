using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using TheKesslerRun2.Services;
using TheKesslerRun2.Services.Interfaces;
using static TheKesslerRun2.Services.Messages.Scan;

namespace TheKesslerRun2.ViewModels;
internal partial class ScanViewModel : ObservableObject,
    IMessageReceiver<CompletedMessage>,
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

    [ObservableProperty]
    private ObservableCollection<DTOs.ResourceFieldDto> _fieldsInRange = [];

    public ScanViewModel(IMessageBus messageBus)
    {
        _messageBus = messageBus;
        Game.Instance.HeartbeatService!.AddReceiver(this);

        messageBus.Subscribe<CompletedMessage>(this);
        messageBus.Subscribe<RechargeCompletedMessage>(this);
    }

    public void Receive(CompletedMessage message)
    {
        IsCharged = false;
        CurrentRechargeProgress = 0;

        foreach (var f in message.FieldsInRange)
        {
            FieldsInRange.Add(f);
        }
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
