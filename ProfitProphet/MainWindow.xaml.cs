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
        // Jobb klikkn�l jel�lje ki az �ppen c�lzott sort
        private void WatchlistListBox_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var item = ItemsControl.ContainerFromElement(
                           (ItemsControl)sender,
                           e.OriginalSource as DependencyObject) as ListBoxItem;
            if (item != null)
                item.IsSelected = true;
        }

        // Kontext men� "Remove" kattint�s
        private async void RemoveMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (WatchlistListBox?.SelectedItem is string symbol)
            {
                var result = MessageBox.Show(
                    $"Biztosan el szeretn�d t�vol�tani a(z) {symbol} szimb�lumot �s a hozz� tartoz� m�ltb�li adatokat?",
                    "Meger�s�t�s",
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
