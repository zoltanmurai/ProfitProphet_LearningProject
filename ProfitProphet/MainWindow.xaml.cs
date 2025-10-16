using ProfitProphet.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ProfitProphet
{
    public partial class MainWindow : Window
    {
        private MainViewModel Vm => (MainViewModel)DataContext;
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }
        // Jobb klikknél jelölje ki az éppen célzott sort
        private void WatchlistListBox_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var item = ItemsControl.ContainerFromElement(
                           (ItemsControl)sender,
                           e.OriginalSource as DependencyObject) as ListBoxItem;
            if (item != null)
                item.IsSelected = true;
        }

        // Kontext menü "Remove" kattintás
        private async void RemoveMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (WatchlistListBox?.SelectedItem is string symbol)
            {
                var result = MessageBox.Show(
                    $"Biztosan el szeretnéd távolítani a(z) {symbol} szimbólumot és a hozzá tartozó múltbéli adatokat?",
                    "Megerõsítés",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    await Vm.RemoveSymbolAsync(symbol);
                }
            }
        }
    }
}
