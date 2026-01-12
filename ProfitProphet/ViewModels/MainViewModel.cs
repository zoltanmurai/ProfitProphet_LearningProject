using Microsoft.EntityFrameworkCore;
using ProfitProphet.Data;
using ProfitProphet.DTOs;
using ProfitProphet.Services;
using ProfitProphet.Services.Charting;
using ProfitProphet.Services.Indicators;
using ProfitProphet.Settings;
using ProfitProphet.ViewModels.Commands;
using ProfitProphet.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ProfitProphet.Models.Backtesting;

namespace ProfitProphet.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly IStockApiClient _apiClient;
        private readonly IAppSettingsService _settingsService; 
        private readonly IChartSettingsService _chartSettingsService;
        private readonly IIndicatorRegistry _indicatorRegistry;
        private readonly DataService _dataService;
        private readonly ChartBuilder _chartBuilder;
        private AppSettings _settings;

        private string _selectedSymbol;
        private string _selectedInterval;

        private CancellationTokenSource _ctsRefresh;
        private Task _autoTask;
        private bool _isRefreshing;
        private bool _autoRefreshEnabled;
        private int _refreshIntervalMinutes = 5;

        public ObservableCollection<string> Watchlist { get; }
        public ObservableCollection<IntervalItem> Intervals { get; }

        public ChartViewModel ChartVM { get; }

        public ICommand AddSymbolCommand { get; }
        public ICommand OpenSettingsCommand { get; }
        public ICommand RefreshNowCommand { get; }
        public ICommand ToggleAutoRefreshCommand { get; }
        public ICommand OpenStrategyTestCommand { get; }

        public string SelectedSymbol
        {
            get => _selectedSymbol;
            set
            {
                if (Set(ref _selectedSymbol, value))
                {
                    // Forward selection to ChartVM (it will reload itself)
                    ChartVM.CurrentSymbol = value;
                }
            }
        }

        public string SelectedInterval
        {
            get => _selectedInterval;
            set
            {
                if (Set(ref _selectedInterval, value))
                {
                    _settings.DefaultInterval = value;
                    _ = _settingsService.SaveSettingsAsync(_settings);
                    // Forward interval change to ChartVM
                    ChartVM.CurrentInterval = value;
                }
            }
        }

        public bool IsRefreshing
        {
            get => _isRefreshing;
            private set
            {
                if (Set(ref _isRefreshing, value))
                    (RefreshNowCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        public bool AutoRefreshEnabled
        {
            get => _autoRefreshEnabled;
            set
            {
                if (Set(ref _autoRefreshEnabled, value))
                    _ = HandleAutoRefreshToggleAsync(value);
            }
        }

        public int RefreshIntervalMinutes
        {
            get => _refreshIntervalMinutes;
            set
            {
                int v = Math.Max(1, value);
                if (Set(ref _refreshIntervalMinutes, v))
                {
                    _ = PersistSettingsAsync();
                    if (AutoRefreshEnabled) _ = RestartAutoLoopAsync();
                }
            }
        }

        //public MainViewModel()
        //{
        public MainViewModel(
            IStockApiClient apiClient,
            DataService dataService,
            IAppSettingsService settingsService,
            IIndicatorRegistry indicatorRegistry,
            IChartSettingsService chartSettingsService,
            // ÚJ PARAMÉTER ITT:
            ChartBuilder chartBuilder
            )
        {
            _apiClient = apiClient;
            _dataService = dataService;
            _settingsService = settingsService;
            _indicatorRegistry = indicatorRegistry;
            _chartSettingsService = chartSettingsService;

            // ITT MENTJÜK EL A KÖZÖS PÉLDÁNYT:
            _chartBuilder = chartBuilder;
            var cfgPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ProfitProphet", "settings.json");

            _settingsService = new AppSettingsService(cfgPath);
            _settings = _settingsService.LoadSettings();

            _dataService = new DataService(new StockContext());

            // ChartVM uses DataService via mapping to ChartBuilder.CandleData
            //ChartVM = new ChartViewModel(async (symbol, interval) =>
            //{
            //    var list = await _dataService.GetLocalDataAsync(symbol, interval);
            //    return MapToCandleData(list);
            //});
            ChartVM = new ChartViewModel(_settingsService, _settings, async (symbol, interval) =>
            {
                var list = await _dataService.GetLocalDataAsync(symbol, interval);

                // auto-import fallback
                if ((list == null || list.Count == 0) && _settings.AutoDataImport)
                {
                    bool hasLocal = await _dataService.HasLocalDataAsync(symbol, interval);
                    if (!hasLocal)
                    {
                        await _dataService.DownloadLookbackAsync(
                            symbol,
                            interval,
                            _settings.LookbackPeriodDays > 0 ? _settings.LookbackPeriodDays : 200
                        );

                        list = await _dataService.GetLocalDataAsync(symbol, interval);
                    }
                }

                return MapToCandleData(list);
            },
            _chartBuilder);

            Intervals = new ObservableCollection<IntervalItem>
            {
                new IntervalItem("1H", "1h"),
                new IntervalItem("1D", "1d"),
                new IntervalItem("1W", "1wk")
            };

            Watchlist = new ObservableCollection<string>(_settings.Watchlist ?? new());
            if (Watchlist.Count == 0) Watchlist.Add("MSFT");

            _selectedSymbol = Watchlist.FirstOrDefault();
            _selectedInterval = _settings.DefaultInterval ?? "1d";

            // Initialize ChartVM with current selection
            ChartVM.CurrentSymbol = _selectedSymbol;
            ChartVM.CurrentInterval = _selectedInterval;
            _ = ChartVM.InitializeAsync();

            AddSymbolCommand = new RelayCommand(AddSymbol);
            OpenSettingsCommand = new RelayCommand(OpenSettings);
            OpenStrategyTestCommand = new RelayCommand(OpenStrategyTest);

            AutoRefreshEnabled = _settings?.AutoRefreshEnabled ?? false;
            RefreshIntervalMinutes = Math.Max(1, _settings?.RefreshIntervalMinutes ?? 5);

            RefreshNowCommand = new RelayCommand(async _ => await RefreshNowAsync(), _ => !IsRefreshing);
            ToggleAutoRefreshCommand = new RelayCommand(_ => AutoRefreshEnabled = !AutoRefreshEnabled);
        }

        // Maps your entity DTO to ChartBuilder.CandleData for the ChartViewModel loader
        private static List<ChartBuilder.CandleData> MapToCandleData(IEnumerable<ProfitProphet.Entities.Candle> src)
        {
            return src
                .OrderBy(c => c.TimestampUtc)
                .Select(c => new ChartBuilder.CandleData
                {
                    Timestamp = c.TimestampUtc,
                    Open = (double)c.Open,
                    High = (double)c.High,
                    Low = (double)c.Low,
                    Close = (double)c.Close,
                    Volume = (double)(c.Volume ?? 0)
                })
                .ToList();
        }

        private void AddSymbol(object _)
        {
            var input = Microsoft.VisualBasic.Interaction.InputBox("Add meg a részvény szimbólumát:", "Új szimbólum");
            if (string.IsNullOrWhiteSpace(input)) return;

            var symbol = input.Trim().ToUpperInvariant();
            if (!Watchlist.Contains(symbol))
            {
                Watchlist.Add(symbol);
                _settings.Watchlist = Watchlist.ToList();
                _ = _settingsService.SaveSettingsAsync(_settings);
            }

            SelectedSymbol = symbol; // will forward to ChartVM
        }

        private void OpenSettings(object _)
        {
            var win = new SettingsWindow(_dataService, _settingsService);
            win.ShowDialog();
            _settings = _settingsService.LoadSettings();
        }

        public async Task RefreshNowAsync()
        {
            if (IsRefreshing) return;

            _ctsRefresh?.Cancel();
            _ctsRefresh = new CancellationTokenSource();
            var token = _ctsRefresh.Token;

            try
            {
                IsRefreshing = true;

                // Refresh visible symbols (your existing service API)
                await _dataService.RefreshAllVisibleAsync(Watchlist.ToList(), SelectedInterval, token);

                // After data refresh, just tell ChartVM to reload
                await ChartVM.InitializeAsync();
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                MessageBox.Show($"Frissítési hiba:\n{ex.Message}", "Refresh", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsRefreshing = false;
            }
        }

        private async Task HandleAutoRefreshToggleAsync(bool enabled)
        {
            await PersistSettingsAsync();
            if (enabled) await RestartAutoLoopAsync();
            else await StopAutoLoopAsync();
        }

        private async Task RestartAutoLoopAsync()
        {
            await StopAutoLoopAsync();
            _autoTask = AutoLoopAsync();
        }

        private async Task StopAutoLoopAsync()
        {
            try
            {
                _ctsRefresh?.Cancel();
                if (_autoTask != null) await _autoTask;
            }
            catch { }
            finally
            {
                _autoTask = null;
            }
        }

        private async Task AutoLoopAsync()
        {
            using var timer = new System.Threading.PeriodicTimer(TimeSpan.FromMinutes(RefreshIntervalMinutes));

            await RefreshNowAsync();

            while (AutoRefreshEnabled)
            {
                try
                {
                    var tick = await timer.WaitForNextTickAsync();
                    if (!tick || !AutoRefreshEnabled) break;

                    await RefreshNowAsync();
                }
                catch (OperationCanceledException) { break; }
                catch { /* optional logging */ }
            }
        }

        private async Task PersistSettingsAsync()
        {
            var s = _settingsService.LoadSettings() ?? new AppSettings();
            s.AutoRefreshEnabled = AutoRefreshEnabled;
            s.RefreshIntervalMinutes = RefreshIntervalMinutes;
            await _settingsService.SaveSettingsAsync(s);
        }

        // Optional helper for right-click behavior on the watchlist
        private void WatchlistListBox_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var item = ItemsControl.ContainerFromElement((ItemsControl)sender, e.OriginalSource as DependencyObject) as ListBoxItem;
            if (item != null) item.IsSelected = true;
        }

        public async Task RemoveSymbolAsync(string symbol)
        {
            if (string.IsNullOrWhiteSpace(symbol)) return;

            try
            {
                var wasSelected = SelectedSymbol == symbol;

                if (SelectedSymbol == symbol)
                {
                    SelectedSymbol = null;
                }

                await _dataService.RemoveSymbolAndCandlesAsync(symbol);

                if (Watchlist.Contains(symbol))
                    Watchlist.Remove(symbol);

                _settings.Watchlist = Watchlist.ToList();
                _ = _settingsService.SaveSettingsAsync(_settings);

                //if (SelectedSymbol == removed)
                //SelectedSymbol = Watchlist.FirstOrDefault();

                if (wasSelected)
                {
                    SelectedSymbol = null;
                    await ChartVM.ClearDataAsync();
                }
                // Vagy „szomszédra” lép:
                // else if (Watchlist.Count > 0) SelectedSymbol = Watchlist[0];
            }


            catch (Exception ex)
            {
                MessageBox.Show($"Törlés sikertelen:\n{ex.Message}", "Hiba", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void OpenStrategyTest(object _)
        {
            if (string.IsNullOrWhiteSpace(SelectedSymbol)) return;

            // Adatok lekérése a teszthez (lokális DB-ből)
            var candles = await _dataService.GetLocalDataAsync(SelectedSymbol, SelectedInterval);

            if (candles == null || candles.Count == 0)
            {
                MessageBox.Show("Nincs adat a teszteléshez!", "Hiba");
                return;
            }

            var vm = new StrategyTestViewModel(candles, SelectedSymbol);

            // Amikor a teszt lefut a másik ablakban, megkapjuk az eredményt és kirajzoljuk a nyilakat
            vm.OnTestFinished += (result) =>
            {
                // MainViewModel-ben lévő chartBuilder példány
                _chartBuilder.ShowTradeMarkers(result.Trades);
            };

            var win = new Views.StrategyTestWindow(vm);
            win.Show();
        }

        //public async Task RemoveSymbolAsync(string symbol)
        //{
        //    if (string.IsNullOrWhiteSpace(symbol)) return;

        //    try
        //    {
        //        // Ha a törölt szimbólum volt kiválasztva, nulláz
        //        if (SelectedSymbol == symbol)
        //        {
        //            SelectedSymbol = null;

        //            await ChartVM.ClearDataAsync();
        //        }

        //        await _dataService.RemoveSymbolAndCandlesAsync(symbol);

        //        if (Watchlist.Contains(symbol))
        //            Watchlist.Remove(symbol);

        //        _settings.Watchlist = Watchlist.ToList();
        //        _ = _settingsService.SaveSettingsAsync(_settings);
        //    }
        //    catch (Exception ex)
        //    {
        //        MessageBox.Show($"Törlés sikertelen:\n{ex.Message}", "Hiba", MessageBoxButton.OK, MessageBoxImage.Error);
        //    }
        //}

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        protected bool Set<T>(ref T field, T value, [CallerMemberName] string name = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(name);
            return true;
        }
    }

    public record IntervalItem(string Display, string Tag);
}
