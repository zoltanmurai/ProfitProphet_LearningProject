using Microsoft.EntityFrameworkCore;
using OxyPlot;
using ProfitProphet.Data;
using ProfitProphet.DTOs;
using ProfitProphet.Services;
using ProfitProphet.Settings;
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
using ProfitProphet.ViewModels.Commands;

namespace ProfitProphet.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly IAppSettingsService _settingsService;
        private readonly DataService _dataService;
        private readonly ChartBuilder _chartBuilder = new();

        private AppSettings _settings;
        private string _selectedSymbol;
        private string _selectedInterval;
        private PlotModel _chartModel;

        private CancellationTokenSource _ctsRefresh;
        private Task _autoTask;
        private bool _isRefreshing;
        private bool _autoRefreshEnabled;
        private int _refreshIntervalMinutes = 5;

        private List<ChartBuilder.CandleData> _candles = new();

        public ObservableCollection<string> Watchlist { get; }
        public ObservableCollection<IntervalItem> Intervals { get; }

        public ICommand AddEma20Command { get; }
        public ICommand ClearIndicatorsCommand { get; }

        public bool HasChartData => _candles?.Count > 0;


        public string SelectedSymbol
        {
            get => _selectedSymbol;
            set
            {
                if (Set(ref _selectedSymbol, value))
                {
                    _ = LoadChartAsync();
                    (AddEma20Command as RelayCommand)?.RaiseCanExecuteChanged();
                    (ClearIndicatorsCommand as RelayCommand)?.RaiseCanExecuteChanged();
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
                    _ = LoadChartAsync();
                }
            }
        }

        private static List<ChartBuilder.CandleData> MapToCandleData(IEnumerable<ProfitProphet.Entities.Candle> src)
        {
            return src
                .OrderBy(c => c.TimestampUtc)                // ha csak Timestamp van
                .Select(c => new ChartBuilder.CandleData
                {
                    Timestamp = c.TimestampUtc,
                    Open = (double)c.Open,
                    High = (double)c.High,
                    Low = (double)c.Low,
                    Close = (double)c.Close
                })
                .ToList();
        }

        public MainViewModel()
        {
            var cfgPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ProfitProphet", "settings.json");

            _settingsService = new AppSettingsService(cfgPath);
            _settings = _settingsService.LoadSettings();

            _dataService = new DataService(new StockContext());
            _chartBuilder = new ChartBuilder();

            Intervals = new ObservableCollection<IntervalItem>
            {
                new IntervalItem("1H", "1h"),
                new IntervalItem("1D", "1d"),
                new IntervalItem("1W", "1wk")
            };

            Watchlist = new ObservableCollection<string>(_settings.Watchlist ?? new());
            if (Watchlist.Count == 0)
                Watchlist.Add("MSFT");

            _selectedSymbol = Watchlist.FirstOrDefault();
            _selectedInterval = _settings.DefaultInterval ?? "1d";

            AddSymbolCommand = new RelayCommand(AddSymbol);
            OpenSettingsCommand = new RelayCommand(OpenSettings);

            // Auto-refresh beállítások
            var s = _settingsService.LoadSettings();
            AutoRefreshEnabled = s?.AutoRefreshEnabled ?? false;
            RefreshIntervalMinutes = Math.Max(1, s?.RefreshIntervalMinutes ?? 5);

            RefreshNowCommand = new RelayCommand(async _ => await RefreshNowAsync(), _ => !IsRefreshing);
            ToggleAutoRefreshCommand = new RelayCommand(_ => AutoRefreshEnabled = !AutoRefreshEnabled);

            AddEma20Command = new RelayCommand(_ => AddEma20(), _ => HasChartData && !string.IsNullOrWhiteSpace(SelectedSymbol));
            ClearIndicatorsCommand = new RelayCommand(_ => ClearIndicators(), _ => HasChartData && !string.IsNullOrWhiteSpace(SelectedSymbol));

            _ = LoadChartAsync();
        }

        private void AddEma20()
        {
            if (_candles == null || _candles.Count == 0) return; // vagy üzenet a usernek

            _chartBuilder.AddIndicatorToSymbol(SelectedSymbol, "ema", p =>
            {
                p["Period"] = 20;
                p["Source"] = "Close";
            });

            //ChartModel = _chartBuilder.BuildInteractiveChart(_candles, _symbol, _interval);
            //OnPropertyChanged(nameof(ChartModel));
            RebuildChart();
        }

        private void ClearIndicators()
        {
            if (_candles == null || _candles.Count == 0) return;

            _chartBuilder.ClearIndicatorsForSymbol(SelectedSymbol);
            //ChartModel = _chartBuilder.BuildInteractiveChart(_candles, _symbol, _interval);
            //OnPropertyChanged(nameof(ChartModel));
            RebuildChart();
        }

        #region Parancsok
        public ICommand AddSymbolCommand { get; }
        public ICommand OpenSettingsCommand { get; }
        public ICommand RefreshNowCommand { get; }
        public ICommand ToggleAutoRefreshCommand { get; }
        #endregion

        #region Állapot

        public PlotModel ChartModel
        {
            get => _chartModel;
            set
            {
                if (Set(ref _chartModel, value))
                    OnPropertyChanged(nameof(HasChartData));
            }
        }

        public bool IsRefreshing
        {
            get => _isRefreshing;
            private set
            {
                if (Set(ref _isRefreshing, value))
                {
                    // Frissítjük a Refresh gomb CanExecute-ját
                    (RefreshNowCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
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
        #endregion

        #region Műveletek
        private async Task LoadChartAsync()
        {
            if (string.IsNullOrWhiteSpace(_selectedSymbol))
                return;

            try
            {
                // 1) csak lokális
                var candles = await _dataService.GetLocalDataAsync(_selectedSymbol, _selectedInterval);

                // 2) ha nincs lokális és be van kapcsolva az auto-import → egyszeri lookback letöltés
                if ((candles == null || candles.Count == 0) && _settings.AutoDataImport)
                {
                    bool hasLocal = await _dataService.HasLocalDataAsync(_selectedSymbol, _selectedInterval);
                    if (!hasLocal)
                    {
                        await _dataService.DownloadLookbackAsync(
                            _selectedSymbol,
                            _selectedInterval,
                            _settings.LookbackPeriodDays > 0 ? _settings.LookbackPeriodDays : 200
                        );

                        // mentés után újra DB-ből
                        candles = await _dataService.GetLocalDataAsync(_selectedSymbol, _selectedInterval);
                    }
                }

                // 3) nincs adat → jelzés és kilépés (nem kérünk API-t)
                if (candles == null || candles.Count == 0)
                {
                    MessageBox.Show(
                        $"Nincs lokális adat a(z) {_selectedSymbol} szimbólumhoz.\n" +
                        $"Nyomd meg a Data Import gombot, vagy kapcsold be az automatikus letöltést.",
                        "Chart",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    ChartModel = null;
                    return;
                }

                // 4) rendezés és chart
                _candles = MapToCandleData(candles);


                _chartBuilder.ConfigureLazyLoader(async (start, end) =>
                {
                    var olderData = await _dataService.GetLocalDataAsync(SelectedSymbol, SelectedInterval);
                    return MapToCandleData(
                        olderData.Where(c => c.TimestampUtc >= start && c.TimestampUtc < end)
                    );
                });

                ChartModel = _chartBuilder.BuildInteractiveChart(_candles, SelectedSymbol, SelectedInterval);
            }

            catch (Exception ex)
            {
                MessageBox.Show($"Hiba a chart betöltése közben:\n{ex.Message}",
                    "Chart Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddSymbol(object obj)
        {
            var input = Microsoft.VisualBasic.Interaction.InputBox("Add meg a részvény szimbólumát:", "Új szimbólum");
            if (string.IsNullOrWhiteSpace(SelectedSymbol)) return;

            var symbol = input.Trim().ToUpperInvariant();
            if (!Watchlist.Contains(symbol))
            {
                Watchlist.Add(symbol);
                _settings.Watchlist = Watchlist.ToList();
                _ = _settingsService.SaveSettingsAsync(_settings);
            }
        }

        private void RebuildChart()
        {
            ChartModel = _chartBuilder.BuildInteractiveChart(_candles, SelectedSymbol, SelectedInterval);
            OnPropertyChanged(nameof(HasChartData));
            (AddEma20Command as RelayCommand)?.RaiseCanExecuteChanged();
            (ClearIndicatorsCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        // (Ha MVVM-tisztaság kell, ezt a ListBox code-behindba tedd.)
        private void WatchlistListBox_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var item = ItemsControl.ContainerFromElement((ItemsControl)sender, e.OriginalSource as DependencyObject) as ListBoxItem;
            if (item != null) item.IsSelected = true; // jobb klikk is kijelöl
        }

        public async Task RemoveSymbolAsync(string symbol)
        {
            if (string.IsNullOrWhiteSpace(symbol)) return;

            try
            {
                // Nézet ürítése, ha épp ezt nézzük
                if (SelectedSymbol == symbol)
                {
                    SelectedSymbol = null;   // setter nem tölt be semmit üresre
                    ChartModel = null;       // HasChartData frissül
                }

                await _dataService.RemoveSymbolAndCandlesAsync(symbol);

                if (Watchlist.Contains(symbol))
                    Watchlist.Remove(symbol);

                _settings.Watchlist = Watchlist.ToList();
                _ = _settingsService.SaveSettingsAsync(_settings);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Törlés sikertelen:\n{ex.Message}",
                    "Hiba", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenSettings(object obj)
        {
            var win = new SettingsWindow(_dataService, _settingsService);
            win.ShowDialog();
            _settings = _settingsService.LoadSettings();
        }
        #endregion

        #region Frissítés + auto loop
        public async Task RefreshNowAsync()
        {
            if (IsRefreshing) return;

            _ctsRefresh?.Cancel();
            _ctsRefresh = new CancellationTokenSource();
            var token = _ctsRefresh.Token;

            try
            {
                IsRefreshing = true;

                // await _dataService.RefreshAllVisibleAsync(token);
                //await _dataService.RefreshSymbolAsync(SelectedSymbol, SelectedInterval, token);
                await _dataService.RefreshAllVisibleAsync(Watchlist.ToList(), SelectedInterval, token);

                await LoadCandlesIntoChartAsync(token);
            }
            catch (OperationCanceledException) { /* megszakítva */ }
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
            if (enabled)
                await RestartAutoLoopAsync();
            else
                await StopAutoLoopAsync();
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
            catch { /* lenyeljük leállításkor */ }
            finally
            {
                _autoTask = null;
            }
        }

        private async Task AutoLoopAsync()
        {
            using var timer = new System.Threading.PeriodicTimer(TimeSpan.FromMinutes(RefreshIntervalMinutes));

            // induláskor azonnali frissítés
            await RefreshNowAsync();

            while (AutoRefreshEnabled)
            {
                try
                {
                    var tick = await timer.WaitForNextTickAsync();
                    if (!tick || !AutoRefreshEnabled) break;

                    await RefreshNowAsync();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    // logolható, majd mehet tovább
                }
            }
        }

        private async Task PersistSettingsAsync()
        {
            var s = _settingsService.LoadSettings() ?? new AppSettings();
            s.AutoRefreshEnabled = AutoRefreshEnabled;
            s.RefreshIntervalMinutes = RefreshIntervalMinutes;
            await _settingsService.SaveSettingsAsync(s);
        }

        private Task LoadCandlesIntoChartAsync(CancellationToken token)
        {
            // Itt töltsd újra a chart adatforrását/PlotModelt, ha kell külön útvonalon
            return LoadChartAsync();
        }
        #endregion

        #region INotifyPropertyChanged

        private void OnCandlesLoaded(IEnumerable<CandleDto> candles)
        {
            //_candles = candles ?? new List<ChartBuilder.CandleData>();
            _candles = candles
                .OrderBy(c => c.TimestampUtc)
                .Select(c => new ChartBuilder.CandleData
                {
                    Timestamp = c.TimestampUtc,
                    Open = (double)c.Open,
                    High = (double)c.High,
                    Low = (double)c.Low,
                    Close = (double)c.Close
                }).ToList();

            RebuildChart();
        }

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
        #endregion
    }

    public record IntervalItem(string Display, string Tag);

}
