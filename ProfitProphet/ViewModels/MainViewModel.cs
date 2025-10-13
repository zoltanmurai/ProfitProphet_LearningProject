using Microsoft.EntityFrameworkCore;
using OxyPlot;
using ProfitProphet.Data;
using ProfitProphet.DTOs;
using ProfitProphet.Entities;
using ProfitProphet.Services;
using ProfitProphet.Views;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ProfitProphet.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly IAppSettingsService _settingsService;
        private readonly DataService _dataService;
        //private readonly ChartBuilder _chartBuilder;
        private readonly ChartBuilder _chartBuilder = new();
        private readonly ChartController _chartController = new();

        private AppSettings _settings;
        private string _selectedSymbol;
        private string _selectedInterval;
        private PlotModel _chartModel;
        private readonly StockContext _context = new();


        public ObservableCollection<string> Watchlist { get; }
        public ObservableCollection<IntervalItem> Intervals { get; }

        public PlotController ChartPlotController => _chartController.GetController();

        //public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

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
            set => Set(ref _chartModel, value);
        }

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
                // 1️⃣ Először próbáljunk lokális adatot betölteni
                var candles = await _dataService.GetLocalDataAsync(_selectedSymbol, _selectedInterval);

                // 2️⃣ Ha nincs elég adat, vagy üres az adatbázis, API-ból töltjük
                if (candles == null || candles.Count < 10)
                {
                    candles = await _dataService.GetDataAsync(_selectedSymbol, _selectedInterval);
                    await _dataService.SaveCandlesAsync(_selectedInterval, candles);
                }

                if (candles == null || candles.Count == 0)
                    return;

                // 3️⃣ Chart felépítése
                ChartModel = _chartBuilder.BuildModel(candles.Cast<CandleBase>(), _selectedSymbol, _selectedInterval);

                // 4️⃣ Lazy loader: automatikus régi adatok betöltése scroll esetén
                _chartController.ConfigureLazyLoader(async () =>
                {
                    var first = candles.Min(c => c.TimestampUtc);
                    var tf = _dataService.GetTimeframeFromInterval(_selectedInterval);

                    // Kérünk régebbi 90 napnyi adatot (ha van)
                    var older = await _context.Candles
                        .AsNoTracking()
                        .Where(c => c.Symbol == _selectedSymbol &&
                                    c.Timeframe == tf &&
                                    c.TimestampUtc < first)
                        .OrderByDescending(c => c.TimestampUtc)
                        .Take(90)
                        .ToListAsync();

                    if (older.Count == 0)
                        return;

                    older.Reverse();
                    candles.InsertRange(0, older);

                    ChartModel = _chartBuilder.BuildModel(candles.Cast<CandleBase>(), _selectedSymbol, _selectedInterval);
                    OnPropertyChanged(nameof(ChartModel));
                });

                OnPropertyChanged(nameof(ChartModel));
                OnPropertyChanged(nameof(ChartPlotController));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Hiba a chart betöltésekor: {ex.Message}");
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
