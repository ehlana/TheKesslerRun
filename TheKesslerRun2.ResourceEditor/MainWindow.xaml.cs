using System.IO;
using System.Windows;
using Microsoft.Win32;
using TheKesslerRun2.ResourceEditor.ViewModels;

namespace TheKesslerRun2.ResourceEditor;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        DataContext = _viewModel;
    }

    private void BrowseFilePath_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
            FileName = _viewModel.FilePath
        };

        if (!string.IsNullOrWhiteSpace(_viewModel.FilePath))
        {
            var directory = Path.GetDirectoryName(_viewModel.FilePath);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                dialog.InitialDirectory = directory;
            }
        }

        if (dialog.ShowDialog(this) == true)
        {
            _viewModel.FilePath = dialog.FileName;
            _viewModel.LoadFromFile(dialog.FileName);
        }
    }

    private void SaveAs_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
            FileName = Path.GetFileName(_viewModel.FilePath)
        };

        if (!string.IsNullOrWhiteSpace(_viewModel.FilePath))
        {
            var directory = Path.GetDirectoryName(_viewModel.FilePath);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                dialog.InitialDirectory = directory;
            }
        }

        if (dialog.ShowDialog(this) == true)
        {
            _viewModel.FilePath = dialog.FileName;
            _viewModel.SaveToFile(dialog.FileName);
        }
    }
}
