using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Linq;
using TheKesslerRun2.DTOs;
using TheKesslerRun2.Extensions;
using TheKesslerRun2.Services;
using TheKesslerRun2.Services.Interfaces;
using static TheKesslerRun2.Services.Messages.Scan;

namespace TheKesslerRun2.ViewModels;
internal partial class ScanViewModel : ObservableObject, IHeartbeatReceiver
{
    public double MaxDistance { get; } = 1000;

    [ObservableProperty]
    private double _rechargeTime = 5.0;

    [ObservableProperty]
    private double _currentRechargeProgress;

    [ObservableProperty]
    private bool _isCharged = true;

    private readonly IMessageBus _messageBus;

    [ObservableProperty]
    private ObservableCollection<ResourceFieldDto> _fieldsInRange = [];

    public ScanViewModel(IMessageBus messageBus)
    {
        _messageBus = messageBus;
        Game.Instance.HeartbeatService!.AddReceiver(this);

        messageBus.SubscribeOnUI<CompletedMessage>(Receive);
        messageBus.SubscribeOnUI<RechargeCompletedMessage>(Receive);
        messageBus.SubscribeOnUI<FieldUpdatedMessage>(Receive);
        messageBus.SubscribeOnUI<FieldsKnownMessage>(Receive);
    }

    public void Receive(CompletedMessage message)
    {
        IsCharged = false;
        CurrentRechargeProgress = 0;

        foreach (var field in message.FieldsInRange)
        {
            UpsertField(field);
        }
    }

    public void Receive(FieldUpdatedMessage message)
    {
        if (message.Field.ResourceAmount <= 0)
        {
            RemoveField(message.Field.Id);
        }
        else
        {
            UpsertField(message.Field);
        }
    }

    public void Receive(FieldsKnownMessage message)
    {
        FieldsInRange = new ObservableCollection<ResourceFieldDto>(
            message.Fields.OrderBy(f => f.DistanceFromCentre));
    }

    public void Receive(RechargeCompletedMessage _)
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
        if (IsCharged)
        {
            return;
        }

        CurrentRechargeProgress = Math.Min(RechargeTime, CurrentRechargeProgress + deltaSeconds);
    }

    private void UpsertField(ResourceFieldDto field)
    {
        for (int i = 0; i < FieldsInRange.Count; i++)
        {
            if (FieldsInRange[i].Id == field.Id)
            {
                FieldsInRange[i] = field;
                return;
            }
        }

        int insertIndex = 0;
        while (insertIndex < FieldsInRange.Count && FieldsInRange[insertIndex].DistanceFromCentre <= field.DistanceFromCentre)
        {
            insertIndex++;
        }

        FieldsInRange.Insert(insertIndex, field);
    }

    private void RemoveField(Guid id)
    {
        for (int i = 0; i < FieldsInRange.Count; i++)
        {
            if (FieldsInRange[i].Id == id)
            {
                FieldsInRange.RemoveAt(i);
                return;
            }
        }
    }
}
