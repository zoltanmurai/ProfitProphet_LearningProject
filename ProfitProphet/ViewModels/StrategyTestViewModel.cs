using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using ProfitProphet.Entities;
using ProfitProphet.Models.Backtesting;
using ProfitProphet.Models.Strategies;
using ProfitProphet.Services;
using ProfitProphet.ViewModels.Commands;
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
        private readonly OptimizerService _optimizerService;
        private readonly List<Candle> _candles;
        private readonly IStrategySettingsService _strategyService;
        public event Action<BacktestResult> OnTestFinished;
        public IEnumerable<TradeAmountType> TradeAmountTypes => Enum.GetValues(typeof(TradeAmountType)).Cast<TradeAmountType>();

        private StrategyProfile _currentProfile;
        public StrategyProfile CurrentProfile
        {
            get => _currentProfile;
            set { _currentProfile = value; OnPropertyChanged(); }
        }

        // Átnevezve RunTestCommand-ról RunCommand-ra, hogy egyezzen a XAML-el!
        public ICommand RunCommand { get; }
        public ICommand EditStrategyCommand { get; }
        public ICommand OpenOptimizerCommand { get; }

        public string Symbol { get; set; }

        private double _initialCash = 10000;
        public double InitialCash
        {
            get => _initialCash;
            set { _initialCash = value; OnPropertyChanged(); }
        }

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

        private PlotModel _equityModel;
        public PlotModel EquityModel
        {
            get => _equityModel;
            set { _equityModel = value; OnPropertyChanged(); }
        }

        private BacktestResult _result;
        public BacktestResult Result
        {
            get => _result;
            set { _result = value; OnPropertyChanged(); }
        }

        // KONSTRUKTOR JAVÍTVA: BacktestService-t is kap paraméterként a DI miatt!
        public StrategyTestViewModel(
            List<Candle> candles,
            string symbol,
            IStrategySettingsService strategyService,
            OptimizerService optimizerService,
            BacktestService backtestService)
        {
            _candles = candles ?? new List<Candle>();
            _strategyService = strategyService;
            _optimizerService = optimizerService;
            _backtestService = backtestService; // Injektálva, nem 'new'-val példányosítva!
            Symbol = symbol;

            var allProfiles = _strategyService.LoadProfiles();
            var savedProfile = allProfiles.FirstOrDefault(p => p.Symbol == symbol);

            if (savedProfile != null)
            {
                CurrentProfile = savedProfile;
            }
            else
            {
                CurrentProfile = new StrategyProfile
                {
                    Symbol = symbol,
                    Name = "Alap Stratégia",
                    TradeAmount = 2000,
                    CommissionPercent = 0.45,
                    MinCommission = 7.0,
                    AmountType = TradeAmountType.FixedCash
                };
                var defaultGroup = new StrategyGroup { Name = "Alap Setup" };
                defaultGroup.Rules.Add(new StrategyRule { LeftIndicatorName = "CMF", LeftPeriod = 20, Operator = ComparisonOperator.GreaterThan, RightValue = 0 });
                CurrentProfile.EntryGroups.Add(defaultGroup);
            }

            if (_candles.Any())
            {
                StartDate = _candles.First().TimestampUtc;
                EndDate = _candles.Last().TimestampUtc;
            }
            else
            {
                StartDate = DateTime.Now.AddYears(-1);
                EndDate = DateTime.Now;
            }

            // Kötések
            RunCommand = new RelayCommand(_ => RunTest());
            EditStrategyCommand = new RelayCommand(OpenStrategyEditor);
            OpenOptimizerCommand = new RelayCommand(_ => {
                var optVm = new OptimizationViewModel(CurrentProfile, _candles, _optimizerService);
                var win = new Views.OptimizationWindow { DataContext = optVm };
                if (win.ShowDialog() == true)
                {
                    OnPropertyChanged(nameof(CurrentProfile));
                    RunTest();
                }
            });
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
            if (!_candles.Any()) return;
            var filtered = _candles.Where(c => c.TimestampUtc.Date >= StartDate.Date && c.TimestampUtc.Date <= EndDate.Date).ToList();
            if (filtered.Count < 10) return;

            var res = _backtestService.RunBacktest(filtered, CurrentProfile, InitialCash);
            Result = res;

            // Itt hívd meg a chart frissítést, ha kész a metódus
            CreateEquityChart(res.EquityCurve);

            OnTestFinished?.Invoke(res);
        }

        private void CreateEquityChart(List<EquityPoint> points)
        {
            if (points == null || !points.Any()) return;

            // Létrehozunk egy új PlotModel-t a sötét témához igazítva
            var model = new PlotModel
            {
                Title = "Tőke Alakulása",
                TextColor = OxyColors.White,
                PlotAreaBorderColor = OxyColors.Gray,
                Background = OxyColors.Transparent
            };

            // Idő tengely (X)
            var dateAxis = new DateTimeAxis
            {
                Position = AxisPosition.Bottom,
                StringFormat = "yyyy-MM-dd",
                TextColor = OxyColors.LightGray,
                TicklineColor = OxyColors.Gray
            };
            model.Axes.Add(dateAxis);

            // Érték tengely (Y)
            var valueAxis = new LinearAxis
            {
                Position = AxisPosition.Left,
                TextColor = OxyColors.LightGray,
                TicklineColor = OxyColors.Gray,
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColor.FromAColor(30, OxyColors.White)
            };
            model.Axes.Add(valueAxis);

            // A görbe maga
            var series = new LineSeries
            {
                Title = "Equity",
                Color = OxyColors.DodgerBlue, // Szép kék görbe
                StrokeThickness = 2,
                MarkerType = MarkerType.None
            };

            foreach (var pt in points)
            {
                series.Points.Add(DateTimeAxis.CreateDataPoint(pt.Time, pt.Equity));
            }

            model.Series.Add(series);

            // Frissítjük a tulajdonságot, ami értesíti a UI-t
            EquityModel = model;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}