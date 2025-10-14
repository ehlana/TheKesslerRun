using AvalonDock.Layout;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using TheKesslerRun2.Services.Interfaces;
using TheKesslerRun2.Services.Messages;
using TheKesslerRun2.ViewModels;
using TheKesslerRun2.Views;

namespace TheKesslerRun2;

[SupportedOSPlatform("windows")]
public partial class MainWindow : Window, IMessageReceiver<Scan.CompletedMessage>
{
    private bool _firstScanDone = false;
    public MainWindow(MainViewModel vm, IMessageBus messageBus)
    {
        InitializeComponent();
        DataContext = vm;
        messageBus.Subscribe(this);
        Loaded += OnLoaded;
    }

    /// <summary>
    /// Adds a UserControl as a LayoutDocument to the main DockingManager.
    /// </summary>
    /// <param name="view">The UserControl to add</param>
    /// <param name="title">Title of the tab</param>
    /// <param name="canClose">Whether the tab can be closed</param>
    /// <param name="selectThisDocument">Whether to select this document immediately</param>
public void AddDockedDocument(UserControl view, string title, bool canClose = true, bool selectThisDocument = false, bool newPane = false)
{
    if (view == null) return;

    // Wrap the view in a LayoutDocument
    var layoutDoc = new LayoutDocument
    {
        Title = title,
        Content = view,
        CanClose = canClose
    };

    if (newPane)
    {
        // Create a new LayoutDocumentPane and insert it to the right of the first pane
        var existingPane = DockManager.Layout.Descendents().OfType<LayoutDocumentPane>().FirstOrDefault();

        var newPaneInstance = new LayoutDocumentPane();
        newPaneInstance.Children.Add(layoutDoc);

        if (existingPane != null && existingPane.Parent is LayoutPanel parentPanel)
        {
            int index = parentPanel.IndexOfChild(existingPane);
            parentPanel.Children.Insert(index + 1, newPaneInstance);
        }
        else
        {
            DockManager.Layout.RootPanel.Children.Add(newPaneInstance);
        }
    }
    else
    {
        // Add to first available pane as a tab
        var docPane = DockManager.Layout.Descendents().OfType<LayoutDocumentPane>().FirstOrDefault();
        if (docPane != null)
        {
            docPane.Children.Add(layoutDoc);
        }
        else
        {
            // If none exists, create a new pane
            var newPaneInstance = new LayoutDocumentPane(layoutDoc);
            DockManager.Layout.RootPanel.Children.Add(newPaneInstance);
        }
    }

    // Select the new document immediately
    if (selectThisDocument) layoutDoc.IsSelected = true;
}

    public void Receive(Scan.CompletedMessage message)
    {
        // If this is the first ever scan, then bring up the drones view.
        if (_firstScanDone) return;

        _firstScanDone = true;

        AddDockedDocument(App.Current.GetService<DronesView>(), "Drones", false, false, true);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        AddDockedDocument(App.Current.GetService<RecyclingCentreView>(), "Recycling Centre", false);
    }
}
