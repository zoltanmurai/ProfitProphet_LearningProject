using Microsoft.EntityFrameworkCore;
using OxyPlot;
using ProfitProphet.Data;
using ProfitProphet.DTOs;
using ProfitProphet.Services;
using ProfitProphet.Views;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ProfitProphet.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly IAppSettingsService _settingsService;
        private readonly DataService _dataService;
        private readonly ChartBuilder _chartBuilder;

        private AppSettings _settings;
        private string _selectedSymbol;
        private string _selectedInterval;
        private PlotModel _chartModel;


        public ObservableCollection<string> Watchlist { get; }
        public ObservableCollection<IntervalItem> Intervals { get; }

        public string SelectedSymbol
        {
            get => _selectedSymbol;
            set
            {
                if (Set(ref _selectedSymbol, value))
                    _ = LoadChartAsync();
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
                    _settingsService.Save(_settings);
                    _ = LoadChartAsync();
                }
            }
        }

        public PlotModel ChartModel
        {
            get => _chartModel;
            //set => Set(ref _chartModel, value);
            set
            {
                if (Set(ref _chartModel, value))
                {
                    // Ha sikerült beállítani, frissítjük a HasChartData-t is
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasChartData)));
                }
            }
        }

        public bool HasChartData => ChartModel != null;
        public ICommand AddSymbolCommand { get; }
        public ICommand OpenSettingsCommand { get; }

        public MainViewModel()
        {
            var cfgPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ProfitProphet", "settings.json");

            _settingsService = new JsonAppSettingsService(cfgPath);
            _settings = _settingsService.Load();

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

            _ = LoadChartAsync();
        }

        private async Task LoadChartAsync()
        {
            if (string.IsNullOrWhiteSpace(_selectedSymbol))
                return;

            try
            {

                var candles = await _dataService.GetRefreshReloadAsync(_selectedSymbol, _selectedInterval);



                // Ha nincs adat, vagy 1 napnál régebbi, frissítsünk API-ról
                if (candles.Count == 0 || candles.Last().TimestampUtc < DateTime.UtcNow.AddDays(-1))
                {
                    var newData = await _dataService.GetDataAsync(_selectedSymbol, _selectedInterval);

                    if (newData?.Count > 0)
                    {
                        
                        // 🔹 új adatok mentése a DB-be
                        await _dataService.SaveCandlesAsync(_selectedSymbol, newData);

                        // 🔹 a memóriában lévő listához hozzáadjuk a frisset
                        candles.AddRange(newData);
                    }
                }

                //  Biztonsági ellenőrzés
                if (candles == null || candles.Count == 0)
                {
                    MessageBox.Show($"Nincs adat a(z) {_selectedSymbol} szimbólumhoz.", "Chart",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Rendezés időrendbe
                candles = candles.OrderBy(c => c.TimestampUtc).ToList();

                // Chart felépítése
                _chartBuilder.ConfigureLazyLoader(async (start, end) =>
                {
                    var olderData = await _dataService.GetLocalDataAsync(_selectedSymbol, _selectedInterval);
                    return olderData
                        .Where(c => c.TimestampUtc >= start && c.TimestampUtc < end)
                        .OrderBy(c => c.TimestampUtc)
                        .Select(c => new ChartBuilder.CandleData
                        {
                            Timestamp = c.TimestampUtc,
                            Open = (double)c.Open,
                            High = (double)c.High,
                            Low = (double)c.Low,
                            Close = (double)c.Close
                        })
                        .ToList();
                });

                ChartModel = _chartBuilder.BuildInteractiveChart(
                    candles.Select(c => new ChartBuilder.CandleData
                    {
                        Timestamp = c.TimestampUtc,
                        Open = (double)c.Open,
                        High = (double)c.High,
                        Low = (double)c.Low,
                        Close = (double)c.Close
                    }).ToList(),
                    _selectedSymbol,
                    _selectedInterval);
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
            if (string.IsNullOrWhiteSpace(input)) return;

            var symbol = input.Trim().ToUpperInvariant();
            if (!Watchlist.Contains(symbol))
            {
                Watchlist.Add(symbol);
                _settings.Watchlist = Watchlist.ToList();
                _settingsService.Save(_settings);
            }
        }

        private void WatchlistListBox_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var item = ItemsControl.ContainerFromElement((ItemsControl)sender, e.OriginalSource as DependencyObject) as ListBoxItem;
            if (item != null)
                item.IsSelected = true; // jobb klikk is kijelöl
        }

        public async Task RemoveSymbolAsync(string symbol)
        {
            if (string.IsNullOrWhiteSpace(symbol))
                return;

            try
            {
                // 1. Törlés az adatbázisból
                await _dataService.RemoveSymbolAndCandlesAsync(symbol);

                // 2. Eltávolítás a Watchlist-ből
                if (Watchlist.Contains(symbol))
                    Watchlist.Remove(symbol);

                // 3. Settings mentése
                _settings.Watchlist = Watchlist.ToList();
                _settingsService.Save(_settings);

                // 4. Ha a törölt volt kiválasztva, válassz másik szimbólumot
                if (SelectedSymbol == symbol)
                {
                    // Válassz az első megmaradt szimbólumot, vagy null
                    SelectedSymbol = Watchlist.Count > 0 ? Watchlist[0] : null;

                    // Ha null lett, töröld a chartot
                    if (SelectedSymbol == null)
                    {
                        ChartModel = null;
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasChartData)));
                    }
                    // Ha van még szimbólum, a SelectedSymbol setter magáról meghívja a LoadChartAsync-et
                }
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
            _settings = _settingsService.Load();
        }

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
        protected bool Set<T>(ref T field, T value, [CallerMemberName] string name = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            return true;
        }
        #endregion
    }

    public record IntervalItem(string Display, string Tag);

    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Predicate<object> _canExecute;

        public RelayCommand(Action<object> execute, Predicate<object> canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter) => _canExecute?.Invoke(parameter) ?? true;
        public void Execute(object parameter) => _execute(parameter);
        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }
}
