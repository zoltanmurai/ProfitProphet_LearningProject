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
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Threading;

namespace ProfitProphet.ViewModels
{
    public class StrategyTestViewModel : INotifyPropertyChanged
    {
        private readonly BacktestService _backtestService;
        private readonly IStrategySettingsService _strategyService;
        private readonly OptimizerService _optimizerService;
        private CancellationTokenSource _cts;
        private List<OptimizationParameterUI> _savedOptimizerState;

        private readonly List<Candle> _candles;
        public event Action<BacktestResult> OnTestFinished;

        public IEnumerable<TradeAmountType> TradeAmountTypes => Enum.GetValues(typeof(TradeAmountType)).Cast<TradeAmountType>();

        private StrategyProfile _currentProfile;
        public StrategyProfile CurrentProfile
        {
            get => _currentProfile;
            set { _currentProfile = value; OnPropertyChanged(); }
        }

        private bool _isRunning;
        public bool IsRunning
        {
            get => _isRunning;
            set { _isRunning = value; OnPropertyChanged(); }
        }

        private string _optimizationStatusMessage;
        public string OptimizationStatusMessage
        {
            get => _optimizationStatusMessage;
            set { _optimizationStatusMessage = value; OnPropertyChanged(); }
        }

        private System.Windows.Media.Brush _optimizationStatusColor;
        public System.Windows.Media.Brush OptimizationStatusColor
        {
            get => _optimizationStatusColor;
            set { _optimizationStatusColor = value; OnPropertyChanged(); }
        }

        public ICommand RunCommand { get; }
        public ICommand EditStrategyCommand { get; }
        public ICommand OpenOptimizerCommand { get; }

        private bool _useOptimization;
        //public bool UseOptimization
        //{
        //    get => _useOptimization;
        //    set { _useOptimization = value; OnPropertyChanged(); }
        //}
        public bool UseOptimization
        {
            get => _useOptimization;
            set
            {
                _useOptimization = value;
                OnPropertyChanged();
                UpdateOptimizationStatus(); // ITT HÍVJUK MEG!
            }
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

        private int _progressValue;
        public int ProgressValue
        {
            get => _progressValue;
            set { _progressValue = value; OnPropertyChanged(); }
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

        //private async void RunTest()
        //{
        //    if (CurrentProfile == null || _candles == null) return;

        //    if (UseOptimization)
        //    {
        //        if (_currentOptimizationParams == null || !_currentOptimizationParams.Any())
        //        {
        //            MessageBox.Show("Nincsenek beállítva optimalizációs paraméterek!");
        //            return;
        //        }

        //        OptimizationResults.Clear();
        //        var results = await _optimizerService.OptimizeAsync(_candles, CurrentProfile, _currentOptimizationParams);

        //        foreach (var res in results.OrderByDescending(r => r.Score))
        //        {
        //            OptimizationResults.Add(res);
        //        }

        //        if (OptimizationResults.Any()) SelectedResult = OptimizationResults.First();
        //    }
        //    else
        //    {
        //        // JAVÍTÁS: Átadjuk az InitialCash-t
        //        var res = _backtestService.RunBacktest(_candles, CurrentProfile, InitialCash);

        //        Result = res;

        //        // JAVÍTÁS: A teljes 'res' objektumot adjuk át az UpdateChart-nak!
        //        UpdateChart(res);

        //        IndicatorTestValues = "Egyedi futtatás a jelenlegi beállításokkal.";
        //    }
        //}
        private async void RunTest()
        {
            // A. HA MÁR FUT -> LEÁLLÍTJUK
            if (IsRunning)
            {
                _cts?.Cancel(); // Jelezzük a leállást
                IndicatorTestValues = "Leállítás folyamatban...";
                return; // Kilépünk, a feladat a catch ágban fog megállni
            }

            // B. HA NEM FUT -> INDÍTJUK
            if (CurrentProfile == null || _candles == null) return;

            IsRunning = true;
            ProgressValue = 0;

            // Új "leállító gomb" létrehozása
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            IProgress<int> progressIndicator = new Progress<int>(value => ProgressValue = value);

            try
            {
                await Task.Run(async () =>
                {
                    if (UseOptimization)
                    {
                        if (_currentOptimizationParams == null || !_currentOptimizationParams.Any())
                        {
                            Application.Current.Dispatcher.Invoke(() => MessageBox.Show("Nincsenek beállítva optimalizációs paraméterek!"));
                            return;
                        }

                        Application.Current.Dispatcher.Invoke(() => OptimizationResults.Clear());

                        // Átadjuk a tokent is!
                        var results = await _optimizerService.OptimizeAsync(_candles, CurrentProfile, _currentOptimizationParams, progressIndicator, token);

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            OptimizationResults.Clear(); // Biztonság kedvéért törlünk, ha duplikálódna
                            foreach (var res in results.OrderByDescending(r => r.Score)) OptimizationResults.Add(res);

                            if (OptimizationResults.Any()) SelectedResult = OptimizationResults.First();
                        });
                    }
                    else
                    {
                        // Sima futtatás
                        progressIndicator.Report(10);
                        if (token.IsCancellationRequested) return; // Gyors ellenőrzés

                        var res = _backtestService.RunBacktest(_candles, CurrentProfile, InitialCash);

                        progressIndicator.Report(100);
                        if (token.IsCancellationRequested) return;

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            Result = res;
                            UpdateChart(res);
                            IndicatorTestValues = $"Egyedi futtatás kész. Profit Faktor: {res.ProfitFactor:N2}";
                        });
                    }
                }, token); // A Task.Run-nak is átadjuk
            }
            catch (OperationCanceledException)
            {
                IndicatorTestValues = "A műveletet a felhasználó megszakította.";
                ProgressValue = 0;
            }
            finally
            {
                // Mindenképp visszaállítjuk a gombot, akár lefutott, akár leállították
                IsRunning = false;
                _cts?.Dispose();
                _cts = null;
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
            // --- MEMÓRIA VISSZATÖLTÉSE (ÚJ RÉSZ) ---
            // Ha van elmentett állapotunk, akkor felülírjuk az alapértelmezett értékeket
            if (_savedOptimizerState != null)
            {
                foreach (var savedParam in _savedOptimizerState)
                {
                    // Megkeressük a megfelelő paramétert a mostani listában a név alapján
                    var targetParam = vm.AvailableParameters.FirstOrDefault(p => p.Name == savedParam.Name);
                    if (targetParam != null)
                    {
                        // Visszatöltjük a beállításokat
                        targetParam.IsSelected = savedParam.IsSelected;
                        targetParam.MinValue = savedParam.MinValue;
                        targetParam.MaxValue = savedParam.MaxValue;
                        targetParam.Step = savedParam.Step; // A lépésközt is!
                    }
                }
            }

            //var win = new OptimizationWindow { DataContext = vm };
            var win = new OptimizationWindow { DataContext = vm, Owner = Application.Current.MainWindow };

            vm.OnRequestClose += (result) =>
            {
                if (result)
                {
                    // --- ÁLLAPOT MENTÉSE ---
                    // elmentjük a jelenlegi beállításokat a memóriába
                    _savedOptimizerState = vm.AvailableParameters.Select(p => new OptimizationParameterUI
                    {
                        Name = p.Name, // név az azonosításhoz
                        IsSelected = p.IsSelected,
                        MinValue = p.MinValue,
                        MaxValue = p.MaxValue,
                        Step = p.Step,
                    }).ToList();
                    // ---------------------------------

                    _currentOptimizationParams = vm.AvailableParameters
                        .Where(p => p.IsSelected)
                        .Select(p => new OptimizationParameter
                        {
                            Rule = p.Rule,
                            IsEntrySide = p.IsEntrySide,
                            ParameterName = p.ParameterName,
                            MinValue = p.MinValue,
                            MaxValue = p.MaxValue,
                            Step = p.Step
                        })
                        .ToList();
                    UseOptimization = true;
                }
                win.Close();
                UpdateOptimizationStatus();
            };
            win.ShowDialog();
        }

        // a teljes BacktestResult-ot várjuk, nem csak a pontokat!
        private void UpdateChart(BacktestResult res)
        {
            if (res == null || res.EquityCurve == null || !res.EquityCurve.Any()) return;

            var equityPoints = res.EquityCurve;
            var balancePoints = res.BalanceCurve;

            var model = new PlotModel
            {
                Title = UseOptimization ? "Tőke Görbe (Optimalizált)" : "Tőke Görbe (Alap)",
                TextColor = OxyColors.White,
                PlotAreaBorderColor = OxyColors.Gray,
                Background = OxyColors.Transparent,
            };

            // Jelmagyarázat
            model.Legends.Add(new Legend
            {
                LegendPosition = LegendPosition.TopLeft,
                LegendTextColor = OxyColors.White,
                LegendBackground = OxyColor.FromAColor(200, OxyColors.Black),
                LegendBorder = OxyColors.Gray
            });

            // 1. IDŐ TENGELY (X)
            double minDate = DateTimeAxis.ToDouble(StartDate);
            double maxDate = DateTimeAxis.ToDouble(EndDate);

            var viewPoints = new List<DataPoint>();
            foreach (var pt in equityPoints) viewPoints.Add(DateTimeAxis.CreateDataPoint(pt.Time, pt.Equity));

            // Kiegészítés a végéig
            if (equityPoints.Last().Time < EndDate) viewPoints.Add(DateTimeAxis.CreateDataPoint(EndDate, equityPoints.Last().Equity));

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

            // 2. PÉNZ TENGELY (Y)
            double minVal = Math.Min(equityPoints.Min(p => p.Equity), balancePoints?.Any() == true ? balancePoints.Min(p => p.Equity) : double.MaxValue);
            double maxVal = Math.Max(equityPoints.Max(p => p.Equity), balancePoints?.Any() == true ? balancePoints.Max(p => p.Equity) : double.MinValue);

            if (Math.Abs(maxVal - minVal) < 0.1) { minVal -= 100; maxVal += 100; }
            else { double margin = (maxVal - minVal) * 0.1; minVal -= margin; maxVal += margin; }

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

            // 3. BALANCE GÖRBE (Kék)
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

            // 4. EQUITY GÖRBE (Zöld)
            var equitySeries = new LineSeries
            {
                Title = "Equity (Lebegő)",
                Color = OxyColors.LimeGreen,
                StrokeThickness = 2,
                MarkerType = MarkerType.None,
            };
            equitySeries.Points.AddRange(viewPoints);
            model.Series.Add(equitySeries);

            // 5. KÖTÉSEK (VÉTEL - Fehér Pötty)
            if (res.Trades != null && res.Trades.Any())
            {
                var buySeries = new ScatterSeries
                {
                    MarkerType = MarkerType.Circle,
                    MarkerSize = 4,
                    MarkerFill = OxyColors.White, // FEHÉR = VÉTEL
                    MarkerStroke = OxyColors.Black,
                    MarkerStrokeThickness = 1,
                    Title = "Vétel"
                };

                // 6. KÖTÉSEK (ELADÁS - Piros Pötty) - EZ AZ ÚJ!
                var sellSeries = new ScatterSeries
                {
                    MarkerType = MarkerType.Circle, // Vagy MarkerType.Square ha más alakot akarsz
                    MarkerSize = 4,
                    MarkerFill = OxyColors.Red,   // PIROS = ELADÁS
                    MarkerStroke = OxyColors.White,
                    MarkerStrokeThickness = 1,
                    Title = "Eladás"
                };

                foreach (var trade in res.Trades)
                {
                    // VÉTEL PONT (EntryDate)
                    var entryPoint = equityPoints.FirstOrDefault(p => p.Time == trade.EntryDate);
                    if (entryPoint != null)
                    {
                        buySeries.Points.Add(new ScatterPoint(DateTimeAxis.ToDouble(trade.EntryDate), entryPoint.Equity));
                    }

                    // ELADÁS PONT (ExitDate) - Csak ha már lezártuk
                    if (trade.ExitDate != DateTime.MinValue)
                    {
                        var exitPoint = equityPoints.FirstOrDefault(p => p.Time == trade.ExitDate);

                        // Ha pontosan arra az időpontra nincs pont (ritka), keressük a legközelebbit, 
                        // de itt a BacktestService ugyanazt az időbélyeget használja, szóval egyeznie kell.
                        if (exitPoint != null)
                        {
                            sellSeries.Points.Add(new ScatterPoint(DateTimeAxis.ToDouble(trade.ExitDate), exitPoint.Equity));
                        }
                    }
                }

                model.Series.Add(buySeries);
                model.Series.Add(sellSeries);
            }

            EquityModel = model;
        }

        // 5. & 6. KÖTÉSEK JELÖLÉSE (ANNOTÁCIÓKKAL) - ÚJ!
        //    if (res.Trades != null && res.Trades.Any())
        //    {
        //        foreach (var trade in res.Trades)
        //        {
        //            // VÉTEL (Entry) - Zöld Nyíl felfelé
        //            var entryPoint = equityPoints.FirstOrDefault(p => p.Time == trade.EntryDate);
        //            if (entryPoint != null)
        //            {
        //                var buyAnnotation = new OxyPlot.Annotations.TextAnnotation
        //                {
        //                    TextPosition = new DataPoint(DateTimeAxis.ToDouble(trade.EntryDate), entryPoint.Equity),
        //                    Text = "▲", // Vagy Wingdings esetén pl: "é"
        //                    // FontFamily = "Wingdings", // Ha Wingdings-et akarsz, vedd ki a kommentet
        //                    FontSize = 20,
        //                    Stroke = OxyColors.Transparent,
        //                    TextColor = OxyColors.LimeGreen, // Zöld szín a vételnek
        //                    FontWeight = 700, // Félkövér
        //                    TextVerticalAlignment = OxyPlot.VerticalAlignment.Top // A pont ALÁ rajzolja, hogy felfelé mutasson
        //                };
        //                model.Annotations.Add(buyAnnotation);
        //            }

        //            // ELADÁS (Exit) - Piros Nyíl lefelé
        //            if (trade.ExitDate != DateTime.MinValue)
        //            {
        //                var exitPoint = equityPoints.FirstOrDefault(p => p.Time == trade.ExitDate);
        //                if (exitPoint != null)
        //                {
        //                    var sellAnnotation = new OxyPlot.Annotations.TextAnnotation
        //                    {
        //                        TextPosition = new DataPoint(DateTimeAxis.ToDouble(trade.ExitDate), exitPoint.Equity),
        //                        Text = "▼", // Vagy Wingdings esetén pl: "ê"
        //                        // FontFamily = "Wingdings",
        //                        FontSize = 20,
        //                        Stroke = OxyColors.Transparent,
        //                        TextColor = OxyColors.Red, // Piros szín az eladásnak
        //                        FontWeight = 700,
        //                        TextVerticalAlignment = OxyPlot.VerticalAlignment.Bottom // A pont FÖLÉ rajzolja
        //                    };
        //                    model.Annotations.Add(sellAnnotation);
        //                }
        //            }
        //        }
        //    }

        //    EquityModel = model;
        //}

        private void UpdateOptimizationStatus()
        {
            if (!UseOptimization)
            {
                OptimizationStatusMessage = ""; // Ha nincs pipa, nincs üzenet
                return;
            }

            if (_currentOptimizationParams != null && _currentOptimizationParams.Any())
            {
                OptimizationStatusMessage = "✅ Paraméterek beállítva. Indíthatod a tesztet!";
                OptimizationStatusColor = System.Windows.Media.Brushes.LimeGreen;
            }
            else
            {
                OptimizationStatusMessage = "⚠️ Nincsenek beállítva intervallumok!\nKattints a 'Beállítások' gombra.";
                OptimizationStatusColor = System.Windows.Media.Brushes.OrangeRed; // Vagy Red
            }
        }

        public ICommand ApplyResultCommand => new RelayCommand(obj =>
        {
            // Ellenőrizzük, hogy érvényes eredményre kattintott-e és vannak-e ismert paramétereink
            if (obj is OptimizationResult res && _currentOptimizationParams != null)
            {
                // Végigmegyünk a paramétereken és alkalmazzuk őket a Jelenlegi Profilra
                for (int i = 0; i < _currentOptimizationParams.Count; i++)
                {
                    if (i < res.Values.Length)
                    {
                        // A Service-ben lévő segédfüggvényt használjuk az érték beállítására
                        _optimizerService.ApplyValue(CurrentProfile, _currentOptimizationParams[i], res.Values[i]);
                    }
                }

                // Értesítés a felhasználónak
                // Opcionális: Automatikusan újra is futtathatjuk a tesztet az új beállításokkal
                // RunTest(); 
        
                MessageBox.Show($"Beállítások sikeresen alkalmazva!\n\nÚj Score: {res.Score:N2}\nProfit: {res.Profit:N0}$", 
                                "Sikeres betöltés", MessageBoxButton.OK, MessageBoxImage.Information);

                // Frissítjük a nézetet, hogy a UI-n is látszódjanak az új számok (ha vannak kötve textboxok)
                OnPropertyChanged(nameof(CurrentProfile));
            }
        });

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}