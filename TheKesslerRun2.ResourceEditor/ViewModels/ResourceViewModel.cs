using TheKesslerRun2.ResourceEditor.Base;

namespace TheKesslerRun2.ResourceEditor.ViewModels;

internal sealed class ResourceViewModel : ObservableObject
{
    private string _id = string.Empty;
    private string _displayName = string.Empty;
    private double _baseMiningDifficulty;
    private double _baseValue;
    private string _colour = "#FFFFFF";

    public string Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }

    public double BaseMiningDifficulty
    {
        get => _baseMiningDifficulty;
        set => SetProperty(ref _baseMiningDifficulty, value);
    }

    public double BaseValue
    {
        get => _baseValue;
        set => SetProperty(ref _baseValue, value);
    }

    public string Colour
    {
        get => _colour;
        set => SetProperty(ref _colour, value);
    }

    public override string ToString() => string.IsNullOrWhiteSpace(DisplayName) ? Id : $"{DisplayName} ({Id})";
}
