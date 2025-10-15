using TheKesslerRun2.ResourceEditor.Base;

namespace TheKesslerRun2.ResourceEditor.ViewModels;

internal sealed class FieldOutputViewModel : ObservableObject
{
    private string? _resourceId;
    private double _ratio;

    public string? ResourceId
    {
        get => _resourceId;
        set => SetProperty(ref _resourceId, value);
    }

    public double Ratio
    {
        get => _ratio;
        set => SetProperty(ref _ratio, value);
    }
}
