using Microsoft.EntityFrameworkCore;
using ProfitProphet.Data;
using ProfitProphet.DTOs;
using ProfitProphet.Models.Backtesting;
using ProfitProphet.Models.Strategies;
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
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ProfitProphet.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly IStockApiClient _apiClient;
        private readonly IAppSettingsService _settingsService;
        private readonly IChartSettingsService _chartSettingsService;
        private readonly IIndicatorRegistry _indicatorRegistry;
        private readonly IStrategySettingsService _strategyService;
        private readonly DataService _dataService;
        private readonly ChartBuilder _chartBuilder;
        private AppSettings _settings;
        private readonly OptimizerService _optimizerService;

        private string _selectedSymbol;
        private string _selectedInterval;

        private CancellationTokenSource _ctsRefresh;
        private Task _autoTask;
        private bool _isRefreshing;
        private bool _autoRefreshEnabled;
        private int _refreshIntervalMinutes = 5;
        private StrategyProfile profile;

        //private Dictionary<string, List<TradeRecord>> _tradeCache = new Dictionary<string, List<TradeRecord>>();
        private readonly BacktestService _backtestService;

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
                    //_ = Task.Run(async () =>
                    //{
                    //    // Várunk egy picit, hogy a ChartVM biztosan elinduljon a Reload-dal
                    //    await Task.Delay(100);
                    //    // Megvárjuk, amíg a ChartVM végez (feltételezve, hogy az InitializeAsync-et hívja)
                    //    await ChartVM.InitializeAsync();
                    //    // Ha kész a chart, jöhetnek a nyilak
                    //    Application.Current.Dispatcher.Invoke(() => RestoreSavedMarkers());
                    //});
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
                    //_ = Task.Run(async () =>
                    //{
                    //    // Várunk egy picit, hogy a ChartVM biztosan elinduljon a Reload-dal
                    //    await Task.Delay(100);
                    //    // Megvárjuk, amíg a ChartVM végez (feltételezve, hogy az InitializeAsync-et hívja)
                    //    await ChartVM.InitializeAsync();
                    //    // Ha kész a chart, jöhetnek a nyilak
                    //    Application.Current.Dispatcher.Invoke(() => RestoreSavedMarkers());
                    //});
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
            ChartBuilder chartBuilder,
            IStrategySettingsService strategyService,
            BacktestService backtestService,
            OptimizerService optimizerService
            )
        {
            _apiClient = apiClient;
            _dataService = dataService;
            _settingsService = settingsService;
            _indicatorRegistry = indicatorRegistry;
            _chartSettingsService = chartSettingsService;
            _strategyService = strategyService;
            _backtestService = backtestService;
            _optimizerService = optimizerService;

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
            ChartVM.ChartUpdated += () => RestoreSavedMarkers();
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
                await UpdateAllWatchlistSignalsAsync();
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
            RestoreSavedMarkers();
            await ChartVM.InitializeAsync();
            await UpdateLiveSignalsAsync();
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

            // 1. LÉPÉS: Megkeressük a kiválasztott szimbólumhoz tartozó stratégiát
            var allProfiles = _strategyService.LoadProfiles();
            var currentProfile = allProfiles.FirstOrDefault(p => p.Symbol == SelectedSymbol);

            // Ha nincs hozzá stratégia
            if (currentProfile == null)
            {
                currentProfile = new ProfitProphet.Models.Strategies.StrategyProfile
                {
                    Symbol = SelectedSymbol,
                    Name = "Új Stratégia",
                    TradeAmount = 10,
                    AmountType = ProfitProphet.Models.Strategies.TradeAmountType.FixedShareCount,
                    EntryGroups = new System.Collections.Generic.List<ProfitProphet.Models.Strategies.StrategyGroup>(),
                    ExitGroups = new System.Collections.Generic.List<ProfitProphet.Models.Strategies.StrategyGroup>()
                };

                // El is mentjük rögtön, hogy legközelebb már meglegyen
                _strategyService.SaveProfile(currentProfile);
            }

            // 2. Adatok lekérése a teszthez (lokális DB-ből)
            var candles = await _dataService.GetLocalDataAsync(SelectedSymbol, SelectedInterval);

            if (candles == null || candles.Count == 0)
            {
                MessageBox.Show("Nincs adat a teszteléshez! Kérlek frissítsd az adatokat.", "Hiányzó Adat", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 3. ViewModel létrehozása a megtalált profillal
            var vm = new StrategyTestViewModel(
                _backtestService,
                _strategyService,
                candles,
                currentProfile, // <--- MOST MÁR EZT ADJUK ÁT (nem a null-t)
                _optimizerService);

            // Amikor a teszt lefut a másik ablakban, megkapjuk az eredményt és kirajzoljuk a nyilakat a főablakon is
            vm.OnTestFinished += (result) =>
            {
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


        private void RestoreSavedMarkers()
        {
            if (string.IsNullOrEmpty(SelectedSymbol)) return;

            Application.Current.Dispatcher.Invoke(() =>
            {
                // Megkeressük a mentett stratégiát ehhez a részvényhez
                var profiles = _strategyService.LoadProfiles();
                var currentProfile = profiles.FirstOrDefault(p => p.Symbol == SelectedSymbol);

                // Ha van benne mentett kötés (nyíl), kirajzoljuk
                if (currentProfile != null && currentProfile.LastTestTrades != null && currentProfile.LastTestTrades.Count > 0)
                {
                    _chartBuilder.ShowTradeMarkers(currentProfile.LastTestTrades);
                }
            });
        }


        protected bool Set<T>(ref T field, T value, [CallerMemberName] string name = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(name);
            return true;
        }

        private async Task UpdateLiveSignalsAsync()
        {
            if (string.IsNullOrEmpty(SelectedSymbol)) return;

            // 1. Lekérjük a legfrissebb gyertyákat a helyi adatbázisból
            var candles = await _dataService.GetLocalDataAsync(SelectedSymbol, SelectedInterval);
            if (candles == null || candles.Count == 0) return;

            // 2. Betöltjük a hozzá tartozó stratégiát
            var profile = _strategyService.LoadProfiles().FirstOrDefault(p => p.Symbol == SelectedSymbol);
            if (profile == null) return;

            // 3. Futtatunk egy "csendes" tesztet (ablaknyitás nélkül)
            // Itt a kezdő tőkét a beállításokból vagy fixen is vehetjük
            var result = _backtestService.RunBacktest(candles, profile, 10000);

            // 4. Frissítjük a profilban a kötéseket és elmentjük
            profile.LastTestTrades = result.Trades;
            _strategyService.SaveProfile(profile);

            // 5. Kirajzoljuk az új (frissített) nyilakat
            RestoreSavedMarkers();
        }
        private async Task UpdateAllWatchlistSignalsAsync()
        {
            // 1. Lekérjük az összes mentett stratégiai profilt
            var allProfiles = _strategyService.LoadProfiles();

            // 2. Végigmegyünk a Watchlist összes elemén
            foreach (var symbol in Watchlist)
            {
                // Megkeressük a hozzá tartozó profilt
                var profile = allProfiles.FirstOrDefault(p => p.Symbol == symbol);
                if (profile == null) continue;

                // Lekérjük a legfrissebb gyertyákat a helyi DB-ből
                var candles = await _dataService.GetLocalDataAsync(symbol, SelectedInterval);
                if (candles == null || candles.Count == 0) continue;

                // 3. Lefuttatjuk a "csendes" háttér-tesztet
                // (A 10000-es tőke itt csak példa, használhatod a beállításokból is)
                var result = _backtestService.RunBacktest(candles, profile, 10000);

                // 4. Elmentjük az eredményt (nyilakat) a profilba és a fájlba
                profile.LastTestTrades = result.Trades;
                _strategyService.SaveProfile(profile);
            }

            // 5. Frissítjük a kijelzőt az aktuálisan nézett részvényre
            RestoreSavedMarkers();
        }
    }

    public record IntervalItem(string Display, string Tag);
}
