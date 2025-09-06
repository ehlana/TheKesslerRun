using System.Windows.Controls;

namespace TheKesslerRun2.Views;
public partial class ScanView : UserControl
{
    public ScanView()
    {
        InitializeComponent();
        DataContext = App.Current.ServiceProvider!.GetService(typeof(ViewModels.ScanViewModel));
    }
}
