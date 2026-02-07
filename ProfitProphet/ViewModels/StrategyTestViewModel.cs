using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Legends;
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
                    MessageBox.Show("Nincsenek beállítva optimalizációs paraméterek!");
                    return;
                }

                OptimizationResults.Clear();
                var results = await _optimizerService.OptimizeAsync(_candles, CurrentProfile, _currentOptimizationParams);

                foreach (var res in results.OrderByDescending(r => r.Score))
                {
                    OptimizationResults.Add(res);
                }

                if (OptimizationResults.Any()) SelectedResult = OptimizationResults.First();
            }
            else
            {
                // JAVÍTÁS: Átadjuk az InitialCash-t
                var res = _backtestService.RunBacktest(_candles, CurrentProfile, InitialCash);

                Result = res;

                // JAVÍTÁS: A teljes 'res' objektumot adjuk át az UpdateChart-nak!
                UpdateChart(res);

                IndicatorTestValues = "Egyedi futtatás a jelenlegi beállításokkal.";
            }
        }

        private void ShowResultOnChart(OptimizationResult optRes)
        {
            if (optRes == null || _currentOptimizationParams == null) return;

            var tempProfile = _optimizerService.DeepCopyProfile(CurrentProfile);

            for (int i = 0; i < _currentOptimizationParams.Count; i++)
            {
                if (i < optRes.Values.Length)
                {
                    _optimizerService.ApplyValue(tempProfile, _currentOptimizationParams[i], optRes.Values[i]);
                }
            }

            // JAVÍTÁS: InitialCash átadása
            var res = _backtestService.RunBacktest(_candles, tempProfile, InitialCash);

            // JAVÍTÁS: Teljes 'res' átadása a grafikonnak
            UpdateChart(res);

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

        // a teljes BacktestResult-ot várjuk, nem csak a pontokat!
        private void UpdateChart(BacktestResult res)
        {
            if (res == null || res.EquityCurve == null || !res.EquityCurve.Any()) return;

            // 1. JAVÍTÁS: Egységes változónevek (equityPoints és balancePoints)
            var equityPoints = res.EquityCurve;
            var balancePoints = res.BalanceCurve;

            var model = new PlotModel
            {
                Title = UseOptimization ? "Tőke Görbe (Optimalizált)" : "Tőke Görbe (Alap)",
                TextColor = OxyColors.White,
                PlotAreaBorderColor = OxyColors.Gray,
                Background = OxyColors.Transparent,
                // 2. JAVÍTÁS: A LegendPosition már nem tulajdonság, hanem külön objektum kell
                // LegendPosition = LegendPosition.TopLeft (EZT TÖRÖLTÜK)
            };

            // Így kell hozzáadni a jelmagyarázatot az új OxyPlot-ban:
            model.Legends.Add(new Legend
            {
                LegendPosition = LegendPosition.TopLeft,
                LegendTextColor = OxyColors.White,
                LegendBackground = OxyColor.FromAColor(200, OxyColors.Black), // Opcionális: félig átlátszó háttér
                LegendBorder = OxyColors.Gray
            });

            // --- 1. IDŐ TENGELY (X) ---
            double minDate = DateTimeAxis.ToDouble(StartDate);
            double maxDate = DateTimeAxis.ToDouble(EndDate);

            var viewPoints = new List<DataPoint>();
            foreach (var pt in equityPoints)
            {
                viewPoints.Add(DateTimeAxis.CreateDataPoint(pt.Time, pt.Equity));
            }
            if (equityPoints.Last().Time < EndDate)
            {
                viewPoints.Add(DateTimeAxis.CreateDataPoint(EndDate, equityPoints.Last().Equity));
            }

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

            // --- 2. PÉNZ TENGELY (Y) ---
            // Kiszámoljuk a minimumot és maximumot mindkét görbére
            double minVal = Math.Min(equityPoints.Min(p => p.Equity), balancePoints?.Any() == true ? balancePoints.Min(p => p.Equity) : double.MaxValue);
            double maxVal = Math.Max(equityPoints.Max(p => p.Equity), balancePoints?.Any() == true ? balancePoints.Max(p => p.Equity) : double.MinValue);

            // 3. JAVÍTÁS: A minVal és maxVal változókat használjuk a számításhoz
            if (Math.Abs(maxVal - minVal) < 0.1)
            {
                minVal -= 100; maxVal += 100;
            }
            else
            {
                double margin = (maxVal - minVal) * 0.1;
                minVal -= margin; maxVal += margin;
            }

            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                TextColor = OxyColors.LightGray,
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColor.FromAColor(40, OxyColors.Gray),
                Minimum = minVal,
                Maximum = maxVal,
                StringFormat = "N0"
            });

            // --- 3. RÉTEG: BALANCE (KÉK VONAL) ---
            if (balancePoints != null && balancePoints.Any())
            {
                var balanceSeries = new LineSeries
                {
                    Title = "Balance (Realizált)",
                    Color = OxyColors.DodgerBlue,
                    StrokeThickness = 2,
                    MarkerType = MarkerType.None
                };

                foreach (var pt in balancePoints) balanceSeries.Points.Add(DateTimeAxis.CreateDataPoint(pt.Time, pt.Equity));
                if (balancePoints.Last().Time < EndDate) balanceSeries.Points.Add(DateTimeAxis.CreateDataPoint(EndDate, balancePoints.Last().Equity));

                model.Series.Add(balanceSeries);
            }

            // --- 4. RÉTEG: EQUITY (ZÖLD VONAL) ---
            var equitySeries = new LineSeries
            {
                Title = "Equity (Lebegő)",
                Color = OxyColors.LimeGreen,
                StrokeThickness = 2,
                MarkerType = MarkerType.None,
            };
            equitySeries.Points.AddRange(viewPoints);
            model.Series.Add(equitySeries);

            // --- 5. RÉTEG: KÖTÉSEK (PÖTTYÖK) ---
            if (res.Trades != null && res.Trades.Any())
            {
                var tradeSeries = new ScatterSeries
                {
                    MarkerType = MarkerType.Circle,
                    MarkerSize = 4,
                    MarkerFill = OxyColors.White,
                    MarkerStroke = OxyColors.Black,
                    MarkerStrokeThickness = 1,
                    Title = "Kötések"
                };

                foreach (var trade in res.Trades)
                {
                    var matchingPoint = equityPoints.FirstOrDefault(p => p.Time == trade.EntryDate);
                    if (matchingPoint != null)
                    {
                        tradeSeries.Points.Add(new ScatterPoint(DateTimeAxis.ToDouble(trade.EntryDate), matchingPoint.Equity));
                    }
                }
                model.Series.Add(tradeSeries);
            }

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