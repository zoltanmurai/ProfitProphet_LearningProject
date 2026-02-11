using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Wpf;
using ProfitProphet.Entities;
using ProfitProphet.Services;
using ProfitProphet.ViewModels;
using ProfitProphet.Views;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;

namespace ProfitProphet
{
    public partial class MainWindow : Window
    {
        private MainViewModel Vm => (MainViewModel)DataContext;

        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            this.ApplyDarkTitleBar();
        }

        private void WatchlistListBox_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var item = ItemsControl.ContainerFromElement(
                           (ItemsControl)sender,
                           e.OriginalSource as DependencyObject) as ListBoxItem;
            if (item != null)
                item.IsSelected = true;
        }

        //private async void RemoveMenuItem_Click(object sender, RoutedEventArgs e)
        //{
        //    // Lekérjük az objektumot, és abból a Symbol property-t
        //    if (WatchlistListBox?.SelectedItem != null)
        //    {
        //        // Ha van SelectedValuePath, akkor a SelectedValue
        //        string symbol = WatchlistListBox.SelectedValue as string;

        //        // VAGY az objektumból kinyerem. 
        //        // dynamic item = WatchlistListBox.SelectedItem;
        //        // string symbol = item.Symbol;

        //        if (!string.IsNullOrEmpty(symbol))
        //        {
        //            var result = MessageBox.Show(
        //                $"Biztosan el szeretnéd távolítani a(z) {symbol} szimbólumot és a hozzá tartozó múltbéli adatokat?",
        //                "Megerõsítés",
        //                MessageBoxButton.YesNo,
        //                MessageBoxImage.Question);

        //            if (result == MessageBoxResult.Yes)
        //            {
        //                await Vm.RemoveSymbolAsync(symbol);
        //            }
        //        }
        //    }
        //}
        private async void RemoveMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // A SelectedValue már a Symbol string lesz, mivel SelectedValuePath="Symbol"
            string symbol = WatchlistListBox?.SelectedValue as string;

            if (!string.IsNullOrEmpty(symbol))
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

        // ===== CROSSHAIR EVENT HANDLEREK =====

        private void ChartView_MouseMove(object sender, MouseEventArgs e)
        {
            if (Vm?.ChartVM == null || Vm.ChartVM.ChartModel == null) return;

            var plotView = sender as PlotView;
            if (plotView == null) return;

            var position = e.GetPosition(plotView);
            var screenPoint = new ScreenPoint(position.X, position.Y);

            var plotModel = Vm.ChartVM.ChartModel;
            var xAxis = plotModel.DefaultXAxis;
            var yAxis = plotModel.DefaultYAxis;

            if (xAxis == null || yAxis == null) return;

            // Képernyõ koordináta -> Adat koordináta konverzió
            var dataPoint = OxyPlot.Axes.Axis.InverseTransform(screenPoint, xAxis, yAxis);

            // Frissítjük a crosshair-t a ViewModel-ben
            Vm.ChartVM.UpdateCrosshair(dataPoint.X, dataPoint.Y);
        }

        private void ChartView_MouseEnter(object sender, MouseEventArgs e)
        {
            if (Vm?.ChartVM != null)
            {
                Vm.ChartVM.ShowCrosshair();
            }
        }

        private void ChartView_MouseLeave(object sender, MouseEventArgs e)
        {
            if (Vm?.ChartVM != null)
            {
                Vm.ChartVM.HideCrosshair();
            }
        }
    }
}
