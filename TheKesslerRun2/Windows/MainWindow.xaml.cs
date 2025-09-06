using System.Windows;
using TheKesslerRun2.ViewModels;

namespace TheKesslerRun2
{
    public partial class MainWindow : Window
    {
        public MainWindow(MainViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
        }
    }
}