using System.Windows.Controls;
using TheKesslerRun2.ViewModels;

namespace TheKesslerRun2.Views;

public partial class RecyclingCentreView : UserControl
{
    public RecyclingCentreView(RecyclingCentreViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
