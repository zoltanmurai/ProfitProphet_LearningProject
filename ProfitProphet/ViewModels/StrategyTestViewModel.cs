using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using ProfitProphet.Entities;
using ProfitProphet.Models.Backtesting;
using ProfitProphet.Models.Strategies;
using ProfitProphet.Services;
using ProfitProphet.ViewModels.Commands;
using ProfitProphet.Views; // Kell az ablak megnyitásához
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace ProfitProphet.ViewModels
{
    public class StrategyTestViewModel : INotifyPropertyChanged
    {
        private readonly BacktestService _backtestService;
        private readonly IStrategySettingsService _strategyService;
        private readonly OptimizerService _optimizerService; // <--- Új Service

        private readonly List<Candle> _candles;
        public event Action<BacktestResult> OnTestFinished;

        // Nem használjuk az Enum.GetValues-t közvetlenül a bindinghoz, ha nem muszáj, de itt maradhat
        public IEnumerable<TradeAmountType> TradeAmountTypes => Enum.GetValues(typeof(TradeAmountType)).Cast<TradeAmountType>();

        private StrategyProfile _currentProfile;
        public StrategyProfile CurrentProfile
        {
            get => _currentProfile;
            set { _currentProfile = value; OnPropertyChanged(); }
        }

        public ICommand RunCommand { get; }
        public ICommand EditStrategyCommand { get; }
        public ICommand OpenOptimizerCommand { get; } // <--- Új Parancs

        public string Symbol { get; private set; }

        private DateTime _startDate;
        public DateTime StartDate
        {
            get => _startDate;
            set { _startDate = value; OnPropertyChanged(); }
        }

        private DateTime _endDate;
        public DateTime EndDate
        {
            get => _endDate;
            set { _endDate = value; OnPropertyChanged(); }
        }

        private BacktestResult _result;
        public BacktestResult Result
        {
            get => _result;
            set { _result = value; OnPropertyChanged(); OnPropertyChanged(nameof(EquityModel)); }
        }

        private PlotModel _equityModel;
        public PlotModel EquityModel
        {
            get => _equityModel;
            set { _equityModel = value; OnPropertyChanged(); }
        }

        // Konstruktor: Most már 5 paramétert vár!
        public StrategyTestViewModel(
            BacktestService backtestService,
            IStrategySettingsService strategyService,
            List<Candle> candles,
            StrategyProfile profile,
            OptimizerService optimizerService)
        {
            _backtestService = backtestService ?? throw new ArgumentNullException(nameof(backtestService));
            _strategyService = strategyService ?? throw new ArgumentNullException(nameof(strategyService));
            _optimizerService = optimizerService ?? throw new ArgumentNullException(nameof(optimizerService));
            _candles = candles ?? throw new ArgumentNullException(nameof(candles));
            CurrentProfile = profile ?? throw new ArgumentNullException(nameof(profile));

            // Candle property javítások:
            if (_candles.Any())
            {
                Symbol = _candles.First().Symbol; // TickerSymbol helyett Symbol
                StartDate = _candles.Min(c => c.TimestampUtc); // Time helyett TimestampUtc
                EndDate = _candles.Max(c => c.TimestampUtc);   // Time helyett TimestampUtc
            }

            RunCommand = new RelayCommand(o => RunTest());

            // Az "Edit" parancs üres volt az eredetiben, de ha kell, ide írhatod a logikát
            //EditStrategyCommand = new RelayCommand(o => MessageBox.Show("Szerkesztés..."));
            EditStrategyCommand = new RelayCommand(OpenStrategyEditor);


            // Optimizer gomb bekötése
            OpenOptimizerCommand = new RelayCommand(o => OpenOptimizer());
        }

        private void OpenStrategyEditor(object obj)
        {
            var editorVm = new StrategyEditorViewModel(CurrentProfile);
            var editorWin = new Views.StrategyEditorWindow();
            editorWin.DataContext = editorVm;
            editorVm.OnRequestClose += () => { editorWin.DialogResult = true; editorWin.Close(); };

            if (editorWin.ShowDialog() == true)
            {
                CurrentProfile = editorVm.Profile;
                _strategyService.SaveProfile(CurrentProfile);
                OnPropertyChanged(nameof(CurrentProfile));
            }
        }


        private void RunTest()
        {
            if (CurrentProfile == null || _candles == null || !_candles.Any()) return;

            // Szűrés a dátumokra (TimestampUtc használatával)
            var filteredCandles = _candles.Where(c => c.TimestampUtc >= StartDate && c.TimestampUtc <= filteredEndDate()).ToList();

            // Segédfüggvény a dátumhoz, mert az EndDate általában 00:00:00, a gyertyák meg napközbeniek lehetnek
            DateTime filteredEndDate() => EndDate.Date.AddDays(1).AddTicks(-1);

            if (!filteredCandles.Any())
            {
                MessageBox.Show("Nincs adat a kiválasztott időszakban!");
                return;
            }

            // Futtatás
            Result = _backtestService.RunBacktest(filteredCandles, CurrentProfile);

            // Grafikon frissítése
            UpdateChart(Result.EquityCurve);

            OnTestFinished?.Invoke(Result);
        }

        private void OpenOptimizer()
        {
            // Ellenőrzés
            if (CurrentProfile == null || _candles == null || !_candles.Any())
            {
                MessageBox.Show("Nincs betöltött stratégia vagy adat!", "Hiba", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 1. ViewModel létrehozása
            // Fontos: Itt a teljes gyertya listát átadjuk, az Optimizer majd szűri ha kell, 
            // vagy használhatjuk a szűrt listát is. Most a teljeset adjuk át a robusztusság miatt.
            var vm = new OptimizationViewModel(CurrentProfile, _candles, _optimizerService);

            // 2. Ablak példányosítás
            var window = new OptimizationWindow
            {
                DataContext = vm,
                Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
            };

            // 3. Megjelenítés
            if (window.ShowDialog() == true)
            {
                // Ha sikeres (DialogResult = true), akkor a profil már frissült a háttérben.
                // Futtassuk újra a tesztet, hogy lássuk az eredményt!
                RunTest();
            }
        }

        private void UpdateChart(List<EquityPoint> points)
        {
            if (points == null || !points.Any()) return;

            var model = new PlotModel
            {
                Title = "Tőke Görbe (Equity)",
                TextColor = OxyColors.White,
                PlotAreaBorderColor = OxyColors.Gray,
                Background = OxyColors.Transparent
            };

            // X Tengely (Idő)
            model.Axes.Add(new DateTimeAxis
            {
                Position = AxisPosition.Bottom,
                StringFormat = "yyyy.MM.dd",
                TextColor = OxyColors.LightGray,
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColor.FromAColor(40, OxyColors.Gray)
            });

            // Y Tengely (Pénz)
            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                TextColor = OxyColors.LightGray,
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColor.FromAColor(40, OxyColors.Gray)
            });

            // Vonal
            var series = new LineSeries
            {
                Title = "Equity",
                Color = OxyColors.LimeGreen,
                StrokeThickness = 2,
                MarkerType = MarkerType.None
            };

            foreach (var pt in points)
            {
                series.Points.Add(DateTimeAxis.CreateDataPoint(pt.Time, pt.Equity));
            }

            model.Series.Add(series);
            EquityModel = model;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}