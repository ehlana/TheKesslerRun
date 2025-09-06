using System.Windows.Controls;
using TheKesslerRun2.ViewModels;

namespace TheKesslerRun2.Views;

public partial class DronesView : UserControl
{
    public DronesView(DronesViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
