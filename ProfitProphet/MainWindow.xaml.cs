using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Wpf;
using ProfitProphet.Entities;
using ProfitProphet.Services;
using ProfitProphet.ViewModels;
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

        //private readonly ChartBuilder _chartBuilder = new();
        //private List<ChartBuilder.CandleData> _candles;
        //private string _symbol;
        //private string _interval;

        //public MainWindow()
        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            //DataContext = new MainViewModel();
            DataContext = viewModel;
            //SourceInitialized += (s, e) => ApplyDarkMode();
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

        //<oxy:PlotView x:Name="MainPlotView"
        //      Model="{Binding ChartModel}" 
        //      MouseMove="PlotView_MouseMove" 
        //      MouseEnter="PlotView_MouseEnter"
        //      MouseLeave="PlotView_MouseLeave"
        //      Background="Transparent" />
        //private void PlotView_MouseMove(object sender, MouseEventArgs e)
        //{
        //    // Lekérjük a ViewModel-t (ellenõrizd, hogy a DataContext tényleg ChartViewModel-e!)
        //    if (DataContext is not ChartViewModel vm || vm.ChartModel == null || vm.CursorLineX == null) return;

        //    var plotModel = vm.ChartModel;
        //    var plotView = sender as IInputElement;
        //    var point = e.GetPosition(plotView); // Egér pozíció pixelben
        //    var screenPoint = new ScreenPoint(point.X, point.Y);

        //    // Visszaszámoljuk az adat koordinátákra
        //    var xAxis = plotModel.DefaultXAxis;
        //    var yAxis = plotModel.DefaultYAxis;

        //    if (xAxis == null || yAxis == null) return;

        //    var dataPoint = Axis.InverseTransform(screenPoint, xAxis, yAxis);

        //    // FRISSÍTJÜK A VIEWMODEL VONALAIT

        //    // 1. Függõleges vonal (Idõ)
        //    vm.CursorLineX.X = dataPoint.X;

        //    // Dátum formázása az idõtávtól függõen
        //    DateTime date = DateTimeAxis.ToDateTime(dataPoint.X);
        //    // Ha napos (D1), elég a dátum, ha perces, kell az óra:perc is
        //    string dateFormat = vm.CurrentInterval == "D1" ? "yyyy.MM.dd" : "yyyy.MM.dd HH:mm";
        //    vm.CursorLineX.Text = date.ToString(dateFormat);

        //    // 2. Vízszintes vonal (Ár)
        //    vm.CursorLineY.Y = dataPoint.Y;
        //    vm.CursorLineY.Text = dataPoint.Y.ToString("N2");

        //    // 3. Újrarajzolás (csak a változások)
        //    plotModel.InvalidatePlot(false);
        //}

        //private void PlotView_MouseEnter(object sender, MouseEventArgs e)
        //{
        //    if (DataContext is ChartViewModel vm && vm.CursorLineX != null)
        //    {
        //        // Megjelenítjük a vonalakat
        //        vm.CursorLineX.StrokeThickness = 1;
        //        vm.CursorLineY.StrokeThickness = 1;
        //        vm.ChartModel.InvalidatePlot(false);
        //    }
        //}

        //private void PlotView_MouseLeave(object sender, MouseEventArgs e)
        //{
        //    if (DataContext is ChartViewModel vm && vm.CursorLineX != null)
        //    {
        //        // Elrejtjük a vonalakat
        //        vm.CursorLineX.StrokeThickness = 0;
        //        vm.CursorLineY.StrokeThickness = 0;
        //        vm.ChartModel.InvalidatePlot(false);
        //    }
        //}

        //private void ApplyDarkMode()
        //{
        //    var windowHandle = new WindowInteropHelper(this).Handle;

        //    // A "20" a DWMWA_USE_IMMERSIVE_DARK_MODE attribútum kódja
        //    // Windows 10 2004 (20H1) verziótól felfelé mûködik
        //    int useImmersiveDarkMode = 1;
        //    DwmSetWindowAttribute(windowHandle, 20, ref useImmersiveDarkMode, sizeof(int));
        //}
        //[DllImport("dwmapi.dll", PreserveSig = true)]
        //private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
    }
}
