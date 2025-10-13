using System.Windows;
using ProfitProphet.ViewModels;

namespace ProfitProphet
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }
    }
}
