using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using ProfitProphet.Entities;
using ProfitProphet.Models.Backtesting;
using ProfitProphet.Models.Strategies;
using ProfitProphet.Services;
using ProfitProphet.ViewModels.Commands;
using ProfitProphet.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        private readonly OptimizerService _optimizerService;

        private readonly List<Candle> _candles;
        public event Action<BacktestResult> OnTestFinished;

        public IEnumerable<TradeAmountType> TradeAmountTypes => Enum.GetValues(typeof(TradeAmountType)).Cast<TradeAmountType>();

        private StrategyProfile _currentProfile;
        public StrategyProfile CurrentProfile
        {
            get => _currentProfile;
            set { _currentProfile = value; OnPropertyChanged(); }
        }

        public ICommand RunCommand { get; }
        public ICommand EditStrategyCommand { get; }
        public ICommand OpenOptimizerCommand { get; }

        private bool _useOptimization;
        public bool UseOptimization
        {
            get => _useOptimization;
            set { _useOptimization = value; OnPropertyChanged(); }
        }

        public ObservableCollection<OptimizationResult> OptimizationResults { get; } = new();

        private OptimizationResult _selectedResult;
        public OptimizationResult SelectedResult
        {
            get => _selectedResult;
            set
            {
                if (_selectedResult != value)
                {
                    _selectedResult = value;
                    OnPropertyChanged();
                    if (value != null)
                    {
                        // KATTINTÁS: Újraszámolás és rajzolás
                        ShowResultOnChart(value);
                    }
                }
            }
        }

        private string _indicatorTestValues;
        public string IndicatorTestValues
        {
            get => _indicatorTestValues;
            set { _indicatorTestValues = value; OnPropertyChanged(); }
        }

        private List<OptimizationParameter> _currentOptimizationParams;

        public string Symbol { get; private set; }

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

            if (_candles.Any())
            {
                Symbol = _candles.First().Symbol;
                StartDate = _candles.Min(c => c.TimestampUtc);
                EndDate = _candles.Max(c => c.TimestampUtc);
            }

            RunCommand = new RelayCommand(o => RunTest());
            EditStrategyCommand = new RelayCommand(OpenStrategyEditor);
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

        private async void RunTest()
        {
            if (CurrentProfile == null || _candles == null) return;

            if (UseOptimization)
            {
                if (_currentOptimizationParams == null || !_currentOptimizationParams.Any())
                {
                    MessageBox.Show("Nincsenek beállítva optimalizációs paraméterek! Kattints a ceruza ikonra.");
                    return;
                }

                OptimizationResults.Clear();

                // Most már Listát kapunk vissza!
                var results = await _optimizerService.OptimizeAsync(_candles, CurrentProfile, _currentOptimizationParams);

                foreach (var res in results.OrderByDescending(r => r.Score))
                {
                    OptimizationResults.Add(res);
                }

                if (OptimizationResults.Any()) SelectedResult = OptimizationResults.First();
            }
            else
            {
                //var res = _backtestService.RunBacktest(_candles, CurrentProfile);
                var res = _backtestService.RunBacktest(_candles, CurrentProfile, InitialCash);
                Result = res;
                UpdateChart(res.EquityCurve);
                IndicatorTestValues = "Egyedi futtatás a jelenlegi beállításokkal.";
            }
        }

        private void ShowResultOnChart(OptimizationResult optRes)
        {
            if (optRes == null || _currentOptimizationParams == null) return;

            var tempProfile = _optimizerService.DeepCopyProfile(CurrentProfile);

            // 2. Értékek alkalmazása 
            for (int i = 0; i < _currentOptimizationParams.Count; i++)
            {
                if (i < optRes.Values.Length)
                {
                    _optimizerService.ApplyValue(tempProfile, _currentOptimizationParams[i], optRes.Values[i]);
                }
            }

            //var res = _backtestService.RunBacktest(_candles, tempProfile);
            var res = _backtestService.RunBacktest(_candles, tempProfile, InitialCash);
            UpdateChart(res.EquityCurve);
            Result = res;

            IndicatorTestValues = $"KIVÁLASZTVA:\n{optRes.ParameterSummary}\n" +
                                  $"Profit: {optRes.Profit:N0}$ | Score: {optRes.Score:N2}";
        }

        private void OpenOptimizer()
        {
            var vm = new OptimizationViewModel(CurrentProfile);
            var win = new OptimizationWindow { DataContext = vm, Owner = Application.Current.MainWindow };

            vm.OnRequestClose += (result) =>
            {
                if (result)
                {
                    _currentOptimizationParams = vm.AvailableParameters
                        .Where(p => p.IsSelected)
                        .Select(p => new OptimizationParameter
                        {
                            Rule = p.Rule,
                            IsEntrySide = p.IsEntrySide,
                            ParameterName = p.ParameterName,
                            MinValue = p.MinValue,
                            MaxValue = p.MaxValue
                        })
                        .ToList();
                    UseOptimization = true;
                }
                win.Close();
            };
            win.ShowDialog();
        }

        private void UpdateChart(List<EquityPoint> points)
        {
            if (points == null || !points.Any()) return;

            var model = new PlotModel
            {
                Title = UseOptimization ? "Tőke Görbe (Optimalizált)" : "Tőke Görbe (Alap)",
                TextColor = OxyColors.White,
                PlotAreaBorderColor = OxyColors.Gray,
                Background = OxyColors.Transparent
            };

            // 1. IDŐ TENGELY (X) SZÁMÍTÁSA
            double minDate = DateTimeAxis.ToDouble(StartDate);
            double maxDate = DateTimeAxis.ToDouble(EndDate);

            var viewPoints = new List<DataPoint>();
            foreach (var pt in points)
            {
                viewPoints.Add(DateTimeAxis.CreateDataPoint(pt.Time, pt.Equity));
            }

            // Záró pont hozzáadása a végéhez (hogy végigérjen a vonal)
            if (points.Last().Time < EndDate)
            {
                viewPoints.Add(DateTimeAxis.CreateDataPoint(EndDate, points.Last().Equity));
            }

            // 2. PÉNZ TENGELY (Y) SZÁMÍTÁSA (Dinamikus Zoom!)
            double minEquity = points.Min(p => p.Equity);
            double maxEquity = points.Max(p => p.Equity);

            // Ha a görbe teljesen lapos (nincs kötés), csinálunk egy kis mesterséges margót
            if (Math.Abs(maxEquity - minEquity) < 0.1)
            {
                minEquity -= 100;
                maxEquity += 100;
            }
            else
            {
                // Ha van mozgás, adunk hozzá 10% margót alul-felül, hogy szép legyen
                double margin = (maxEquity - minEquity) * 0.1;
                minEquity -= margin;
                maxEquity += margin;
            }

            // X Tengely
            model.Axes.Add(new DateTimeAxis
            {
                Position = AxisPosition.Bottom,
                StringFormat = "yyyy.MM.dd",
                TextColor = OxyColors.LightGray,
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColor.FromAColor(40, OxyColors.Gray),
                Minimum = minDate,
                Maximum = maxDate
            });

            // Y Tengely (Pénz) - ITT A LÉNYEG!
            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                TextColor = OxyColors.LightGray,
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColor.FromAColor(40, OxyColors.Gray),
                // Kényszerítjük a skálát a számított értékekre:
                Minimum = minEquity,
                Maximum = maxEquity,
                // Opcionális: formátum (pl. 10k helyett 10,000)
                StringFormat = "N0"
            });

            // Vonal
            var series = new LineSeries
            {
                Title = "Equity",
                Color = OxyColors.LimeGreen,
                StrokeThickness = 2,
                MarkerType = MarkerType.Circle,
                MarkerSize = 3,
                MarkerFill = OxyColors.White
            };

            series.Points.AddRange(viewPoints);
            model.Series.Add(series);

            EquityModel = model;
        }

        public ICommand ApplyResultCommand => new RelayCommand(obj =>
        {
            if (obj is OptimizationResult res)
            {
                MessageBox.Show("Ez a funkció véglegesítené a beállításokat. (Még nincs implementálva)");
            }
        });

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}