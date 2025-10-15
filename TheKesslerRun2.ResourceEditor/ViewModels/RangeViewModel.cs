using TheKesslerRun2.ResourceEditor.Base;

namespace TheKesslerRun2.ResourceEditor.ViewModels;

internal sealed class RangeViewModel : ObservableObject
{
    private double _min;
    private double _max;

    public double Min
    {
        get => _min;
        set => SetProperty(ref _min, value);
    }

    public double Max
    {
        get => _max;
        set => SetProperty(ref _max, value);
    }
}
