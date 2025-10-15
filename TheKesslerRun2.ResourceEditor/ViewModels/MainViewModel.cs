using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows.Data;
using TheKesslerRun2.ResourceEditor.Base;
using TheKesslerRun2.ResourceEditor.Models;
using TheKesslerRun2.ResourceEditor.Services;

namespace TheKesslerRun2.ResourceEditor.ViewModels;

internal sealed class MainViewModel : ObservableObject
{
    private readonly ResourceDataService _dataService = new();
    private readonly ObservableCollection<ResourceViewModel> _resources = new();
    private readonly ObservableCollection<FieldViewModel> _fields = new();
    private readonly ListCollectionView _referencingFieldsView;
    private bool _isLoading;
    private string _filePath = string.Empty;
    private bool _hasUnsavedChanges;
    private string _statusMessage = "Ready.";
    private ResourceViewModel? _selectedResource;
    private FieldViewModel? _selectedField;

    public MainViewModel()
    {
        Fields = new ReadOnlyObservableCollection<FieldViewModel>(_fields);
        Resources = new ReadOnlyObservableCollection<ResourceViewModel>(_resources);

        _referencingFieldsView = new ListCollectionView(_fields);
        _referencingFieldsView.Filter = item => FilterFieldForSelectedResource(item as FieldViewModel);

        _fields.CollectionChanged += OnFieldsCollectionChanged;
        _resources.CollectionChanged += OnResourcesCollectionChanged;

        NewCommand = new RelayCommand(NewFile);
        LoadCommand = new RelayCommand(LoadFromCurrentPath, () => File.Exists(FilePath));
        SaveCommand = new RelayCommand(SaveToCurrentPath, () => _fields.Any() || _resources.Any());
        AddResourceCommand = new RelayCommand(AddResource);
        RemoveResourceCommand = new RelayCommand(RemoveSelectedResource, () => SelectedResource is not null);
        AddFieldCommand = new RelayCommand(AddField);
        RemoveFieldCommand = new RelayCommand(RemoveSelectedField, () => SelectedField is not null);
        AddOutputCommand = new RelayCommand(AddFieldOutput, () => SelectedField is not null);
        RemoveOutputCommand = new RelayCommand(RemoveSelectedOutput, () => SelectedField?.SelectedOutput is not null);

        FilePath = GetDefaultFilePath();

        // Attempt to load the existing data file on startup.
        if (File.Exists(FilePath))
        {
            LoadFromFile(FilePath);
        }
        else
        {
            NewFile();
        }
    }

    public ReadOnlyObservableCollection<ResourceViewModel> Resources { get; }

    public ReadOnlyObservableCollection<FieldViewModel> Fields { get; }

    public ICollectionView ReferencingFieldsView => _referencingFieldsView;

    public RelayCommand NewCommand { get; }

    public RelayCommand LoadCommand { get; }

    public RelayCommand SaveCommand { get; }

    public RelayCommand AddResourceCommand { get; }

    public RelayCommand RemoveResourceCommand { get; }

    public RelayCommand AddFieldCommand { get; }

    public RelayCommand RemoveFieldCommand { get; }

    public RelayCommand AddOutputCommand { get; }

    public RelayCommand RemoveOutputCommand { get; }

    public string FilePath
    {
        get => _filePath;
        set
        {
            if (SetProperty(ref _filePath, value))
            {
                LoadCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasUnsavedChanges
    {
        get => _hasUnsavedChanges;
        private set => SetProperty(ref _hasUnsavedChanges, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public ResourceViewModel? SelectedResource
    {
        get => _selectedResource;
        set
        {
            if (SetProperty(ref _selectedResource, value))
            {
                _referencingFieldsView.Refresh();
                RemoveResourceCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public FieldViewModel? SelectedField
    {
        get => _selectedField;
        set
        {
            if (SetProperty(ref _selectedField, value))
            {
                RemoveFieldCommand.RaiseCanExecuteChanged();
                AddOutputCommand.RaiseCanExecuteChanged();
                RemoveOutputCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public void NewFile()
    {
        _isLoading = true;
        try
        {
            _resources.Clear();
            _fields.Clear();
            SelectedResource = null;
            SelectedField = null;
            HasUnsavedChanges = false;
            StatusMessage = "New resource set created.";
        }
        finally
        {
            _isLoading = false;
            SaveCommand.RaiseCanExecuteChanged();
        }
    }

    public bool LoadFromFile(string path)
    {
        try
        {
            _isLoading = true;
            var model = _dataService.Load(path);
            _resources.Clear();
            _fields.Clear();

            foreach (var resource in model.Resources)
            {
                var viewModel = new ResourceViewModel
                {
                    Id = resource.Id,
                    DisplayName = resource.DisplayName,
                    BaseMiningDifficulty = resource.BaseMiningDifficulty,
                    BaseValue = resource.BaseValue,
                    Colour = resource.Colour
                };
                AttachResource(viewModel);
                _resources.Add(viewModel);
            }

            foreach (var field in model.Fields)
            {
                var fieldVm = new FieldViewModel
                {
                    Id = field.Id,
                    DisplayName = field.DisplayName,
                    Colour = field.Colour,
                    SpawnWeight = field.SpawnWeight
                };

                fieldVm.Amount.Min = field.Amount.Min;
                fieldVm.Amount.Max = field.Amount.Max;
                fieldVm.Difficulty.Min = field.Difficulty.Min;
                fieldVm.Difficulty.Max = field.Difficulty.Max;

                foreach (var output in field.Outputs)
                {
                    var outputVm = new FieldOutputViewModel
                    {
                        ResourceId = output.ResourceId,
                        Ratio = output.Ratio
                    };
                    fieldVm.Outputs.Add(outputVm);
                }

                AttachField(fieldVm);
                _fields.Add(fieldVm);
            }

            FilePath = path;
            HasUnsavedChanges = false;
            StatusMessage = $"Loaded data from '{Path.GetFileName(path)}'.";
            _referencingFieldsView.Refresh();
            SaveCommand.RaiseCanExecuteChanged();
            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load data: {ex.Message}";
            return false;
        }
        finally
        {
            _isLoading = false;
        }
    }

    public bool SaveToFile(string path)
    {
        try
        {
            var model = new ResourceFileModel();
            model.Resources.AddRange(_resources.Select(r => new ResourceDefinition
            {
                Id = r.Id,
                DisplayName = r.DisplayName,
                BaseMiningDifficulty = r.BaseMiningDifficulty,
                BaseValue = r.BaseValue,
                Colour = r.Colour
            }));

            model.Fields.AddRange(_fields.Select(f => new FieldDefinition
            {
                Id = f.Id,
                DisplayName = f.DisplayName,
                Colour = f.Colour,
                SpawnWeight = f.SpawnWeight,
                Amount = new RangeDefinition { Min = f.Amount.Min, Max = f.Amount.Max },
                Difficulty = new RangeDefinition { Min = f.Difficulty.Min, Max = f.Difficulty.Max },
                Outputs = f.Outputs.Select(o => new FieldOutputDefinition
                {
                    ResourceId = o.ResourceId ?? string.Empty,
                    Ratio = o.Ratio
                }).ToList()
            }));

            _dataService.Save(path, model);
            FilePath = path;
            HasUnsavedChanges = false;
            StatusMessage = $"Saved data to '{Path.GetFileName(path)}'.";
            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to save data: {ex.Message}";
            return false;
        }
    }

    public void LoadFromCurrentPath() => LoadFromFile(FilePath);

    public void SaveToCurrentPath() => SaveToFile(FilePath);

    private static string GetDefaultFilePath()
    {
        var baseDirectory = AppContext.BaseDirectory;
        var path = Path.Combine(baseDirectory, "..", "..", "..", "..", "TheKesslerRun2.Services", "Data", "resources.json");
        return Path.GetFullPath(path);
    }

    private void AddResource()
    {
        var resource = new ResourceViewModel
        {
            Id = GenerateUniqueId("resource", _resources.Select(r => r.Id)),
            DisplayName = "New Resource",
            BaseMiningDifficulty = 1.0,
            BaseValue = 1.0,
            Colour = "#FFFFFF"
        };

        AttachResource(resource);
        _resources.Add(resource);
        SelectedResource = resource;
        MarkDirty();
        SaveCommand.RaiseCanExecuteChanged();
    }

    private void RemoveSelectedResource()
    {
        if (SelectedResource is null)
        {
            return;
        }

        var resource = SelectedResource;
        DetachResource(resource);
        _resources.Remove(resource);

        foreach (var field in _fields)
        {
            var outputsToRemove = field.Outputs.Where(o => string.Equals(o.ResourceId, resource.Id, StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var output in outputsToRemove)
            {
                field.Outputs.Remove(output);
            }
        }

        SelectedResource = _resources.FirstOrDefault();
        MarkDirty();
        SaveCommand.RaiseCanExecuteChanged();
        _referencingFieldsView.Refresh();
    }

    private void AddField()
    {
        var field = new FieldViewModel
        {
            Id = GenerateUniqueId("field", _fields.Select(f => f.Id)),
            DisplayName = "New Field",
            Colour = "#FFFFFF",
            SpawnWeight = 1.0
        };

        field.Amount.Min = 0;
        field.Amount.Max = 100;
        field.Difficulty.Min = 0;
        field.Difficulty.Max = 1;

        AttachField(field);
        _fields.Add(field);
        SelectedField = field;
        MarkDirty();
        SaveCommand.RaiseCanExecuteChanged();
    }

    private void RemoveSelectedField()
    {
        if (SelectedField is null)
        {
            return;
        }

        var field = SelectedField;
        DetachField(field);
        _fields.Remove(field);
        SelectedField = _fields.FirstOrDefault();
        MarkDirty();
        SaveCommand.RaiseCanExecuteChanged();
        _referencingFieldsView.Refresh();
    }

    private void AddFieldOutput()
    {
        if (SelectedField is null)
        {
            return;
        }

        var output = new FieldOutputViewModel
        {
            Ratio = 0.1
        };

        SelectedField.Outputs.Add(output);
        SelectedField.SelectedOutput = output;
        MarkDirty();
        _referencingFieldsView.Refresh();
    }

    private void RemoveSelectedOutput()
    {
        if (SelectedField?.SelectedOutput is null)
        {
            return;
        }

        var output = SelectedField.SelectedOutput;
        SelectedField.Outputs.Remove(output);
        SelectedField.SelectedOutput = null;
        MarkDirty();
        _referencingFieldsView.Refresh();
    }

    private void AttachResource(ResourceViewModel resource)
    {
        resource.PropertyChanged += OnChildPropertyChanged;
    }

    private void DetachResource(ResourceViewModel resource)
    {
        resource.PropertyChanged -= OnChildPropertyChanged;
    }

    private void AttachField(FieldViewModel field)
    {
        field.PropertyChanged += OnChildPropertyChanged;
        field.Amount.PropertyChanged += OnChildPropertyChanged;
        field.Difficulty.PropertyChanged += OnChildPropertyChanged;
        field.Outputs.CollectionChanged += OnFieldOutputsChanged;
        foreach (var output in field.Outputs)
        {
            output.PropertyChanged += OnChildPropertyChanged;
        }
    }

    private void DetachField(FieldViewModel field)
    {
        field.PropertyChanged -= OnChildPropertyChanged;
        field.Amount.PropertyChanged -= OnChildPropertyChanged;
        field.Difficulty.PropertyChanged -= OnChildPropertyChanged;
        field.Outputs.CollectionChanged -= OnFieldOutputsChanged;
        foreach (var output in field.Outputs)
        {
            output.PropertyChanged -= OnChildPropertyChanged;
        }
    }

    private void OnFieldOutputsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            foreach (var item in e.NewItems.OfType<FieldOutputViewModel>())
            {
                item.PropertyChanged += OnChildPropertyChanged;
            }
        }

        if (e.OldItems is not null)
        {
            foreach (var item in e.OldItems.OfType<FieldOutputViewModel>())
            {
                item.PropertyChanged -= OnChildPropertyChanged;
            }
        }

        MarkDirty();
        _referencingFieldsView.Refresh();
    }

    private void OnChildPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FieldViewModel.SelectedOutput))
        {
            RemoveOutputCommand.RaiseCanExecuteChanged();
        }

        MarkDirty();
        if (sender is FieldViewModel || sender is FieldOutputViewModel)
        {
            _referencingFieldsView.Refresh();
        }
    }

    private void OnFieldsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            foreach (var field in e.NewItems.OfType<FieldViewModel>())
            {
                AttachField(field);
            }
        }

        if (e.OldItems is not null)
        {
            foreach (var field in e.OldItems.OfType<FieldViewModel>())
            {
                DetachField(field);
            }
        }

        if (!_isLoading)
        {
            MarkDirty();
        }

        _referencingFieldsView.Refresh();
        SaveCommand.RaiseCanExecuteChanged();
    }

    private void OnResourcesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            foreach (var resource in e.NewItems.OfType<ResourceViewModel>())
            {
                AttachResource(resource);
            }
        }

        if (e.OldItems is not null)
        {
            foreach (var resource in e.OldItems.OfType<ResourceViewModel>())
            {
                DetachResource(resource);
            }
        }

        if (!_isLoading)
        {
            MarkDirty();
        }

        SaveCommand.RaiseCanExecuteChanged();
    }

    private bool FilterFieldForSelectedResource(FieldViewModel? field)
    {
        if (field is null || SelectedResource is null)
        {
            return false;
        }

        return field.Outputs.Any(o => string.Equals(o.ResourceId, SelectedResource.Id, StringComparison.OrdinalIgnoreCase));
    }

    private void MarkDirty()
    {
        if (_isLoading)
        {
            return;
        }

        HasUnsavedChanges = true;
        StatusMessage = "Unsaved changes.";
    }

    private static string GenerateUniqueId(string baseId, IEnumerable<string> existingIds)
    {
        var suffix = 1;
        var candidate = $"{baseId}_{suffix}";
        while (existingIds.Any(id => string.Equals(id, candidate, StringComparison.OrdinalIgnoreCase)))
        {
            suffix++;
            candidate = $"{baseId}_{suffix}";
        }

        return candidate;
    }
}
