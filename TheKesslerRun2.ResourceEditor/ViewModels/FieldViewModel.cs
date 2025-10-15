using System.Collections.ObjectModel;
using System.Collections.Specialized;
using TheKesslerRun2.ResourceEditor.Base;

namespace TheKesslerRun2.ResourceEditor.ViewModels;

internal sealed class FieldViewModel : ObservableObject
{
    private string _id = string.Empty;
    private string _displayName = string.Empty;
    private string _colour = "#FFFFFF";
    private double _spawnWeight;
    private FieldOutputViewModel? _selectedOutput;

    public FieldViewModel()
    {
        Amount = new RangeViewModel();
        Difficulty = new RangeViewModel();
        Outputs.CollectionChanged += (_, args) =>
        {
            if (args.NewItems is not null)
            {
                foreach (var item in args.NewItems)
                {
                    if (item is FieldOutputViewModel output)
                    {
                        output.PropertyChanged += (_, _) => OnPropertyChanged(nameof(Outputs));
                    }
                }
            }

            OnPropertyChanged(nameof(Outputs));
        };
    }

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

    public string Colour
    {
        get => _colour;
        set => SetProperty(ref _colour, value);
    }

    public double SpawnWeight
    {
        get => _spawnWeight;
        set => SetProperty(ref _spawnWeight, value);
    }

    public RangeViewModel Amount { get; }

    public RangeViewModel Difficulty { get; }

    public ObservableCollection<FieldOutputViewModel> Outputs { get; } = new();

    public FieldOutputViewModel? SelectedOutput
    {
        get => _selectedOutput;
        set => SetProperty(ref _selectedOutput, value);
    }

    public override string ToString() => string.IsNullOrWhiteSpace(DisplayName) ? Id : $"{DisplayName} ({Id})";
}
