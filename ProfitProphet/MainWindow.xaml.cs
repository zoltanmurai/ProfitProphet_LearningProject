using OxyPlot.Wpf;
using ProfitProphet.Entities;
using ProfitProphet.Services;
using ProfitProphet.ViewModels;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ProfitProphet
{
    public partial class MainWindow : Window
    {
        private MainViewModel Vm => (MainViewModel)DataContext;

        //private readonly ChartBuilder _chartBuilder = new();
        //private List<ChartBuilder.CandleData> _candles;
        //private string _symbol;
        //private string _interval;

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

        //private void AddEma_Click(object sender, RoutedEventArgs e)
        //{
        //    if (_candles == null || _candles.Count == 0) return;

        //    _chartBuilder.AddIndicatorToSymbol(_symbol, "ema", p =>
        //    {
        //        p["Period"] = 20;
        //        p["Source"] = "Close";
        //    });

        //    PlotView.Model = _chartBuilder.BuildInteractiveChart(_candles, _symbol, _interval);
        //}

        //private void RemoveAll_Click(object sender, RoutedEventArgs e)
        //{
        //    if (_candles == null || _candles.Count == 0) return;

        //    _chartBuilder.ClearIndicatorsForSymbol(_symbol);
        //    PlotView.Model = _chartBuilder.BuildInteractiveChart(_candles, _symbol, _interval);
        //}

    }
}
