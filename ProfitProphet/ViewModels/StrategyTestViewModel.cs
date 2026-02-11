using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Legends;
using OxyPlot.Series;
using ProfitProphet.Entities;
using ProfitProphet.Models.Backtesting;
using ProfitProphet.Models.Strategies;
using ProfitProphet.Services;
using ProfitProphet.Services.Indicators;
using ProfitProphet.ViewModels.Commands;
using ProfitProphet.Views;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace ProfitProphet.ViewModels
{
    public class StrategyTestViewModel : INotifyPropertyChanged
    {
        private readonly BacktestService _backtestService;
        private readonly IStrategySettingsService _strategyService;
        private readonly OptimizerService _optimizerService;
        private CancellationTokenSource _cts;

        // STATIKUS MEZŐK A MEMÓRIA MEGŐRZÉSÉHEZ
        private static List<OptimizationParameterUI> _savedOptimizerState;
        private static bool _cachedUseOptimization = false;
        private static List<OptimizationResult> _cachedOptimizationResults;
        private static BacktestResult _cachedSingleResult;
        private static string _cachedLogText;
        private static int _cachedSelectedIndex = -1;
        private readonly IIndicatorRegistry _indicatorRegistry;
        private readonly StrategySettingsService _settingsService;

        private readonly List<Candle> _candles;
        public event Action<BacktestResult> OnTestFinished;

        public IEnumerable<TradeAmountType> TradeAmountTypes => Enum.GetValues(typeof(TradeAmountType)).Cast<TradeAmountType>();

        private StrategyProfile _currentProfile;
        public StrategyProfile CurrentProfile
        {
            get => _currentProfile;
            set 
            {
                //_currentProfile = value; 
                //OnPropertyChanged(); 
                if (_currentProfile != value)
                {
                    _currentProfile = value;
                    OnPropertyChanged();

                    if (_currentProfile != null && !string.IsNullOrEmpty(_currentProfile.LastOptimizationReport))
                    {
                        IndicatorTestValues = _currentProfile.LastOptimizationReport;
                    }
                    else
                    {
                        IndicatorTestValues = GetStrategyDetails(_currentProfile);
                    }
                }
            }
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

        private bool _isVisualMode = true; // Alapértelmezetten legyen bekapcsolva a "Show" kedvéért
        public bool IsVisualMode
        {
            get => _isVisualMode;
            set { _isVisualMode = value; OnPropertyChanged(); }
        }

        public ICommand RunCommand { get; }
        public ICommand EditStrategyCommand { get; }
        public ICommand OpenOptimizerCommand { get; }
        public ICommand ApplyResultCommand { get; private set; }

        private bool _useOptimization;
        public bool UseOptimization
        {
            get => _useOptimization;
            set
            {
                if (_useOptimization != value)
                {
                    _useOptimization = value;
                    _cachedUseOptimization = value; // Statikus mentés
                    OnPropertyChanged();
                    UpdateOptimizationStatus();
                }
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

                    // --- 1. MENTJÜK A KIVÁLASZTÁST ---
                    if (OptimizationResults != null)
                    {
                        _cachedSelectedIndex = OptimizationResults.IndexOf(value);
                    }

                    if (IsRunning) return;

                    // --- 2. GRAFIKON FRISSÍTÉSE ---
                    // Csak akkor tudunk rajzolni, ha vannak paraméter-szabályaink!
                    if (value != null && _currentOptimizationParams != null && _currentOptimizationParams.Count > 0)
                    {
                        // Paraméterek alkalmazása
                        for (int i = 0; i < _currentOptimizationParams.Count; i++)
                        {
                            if (i < value.Values.Length)
                            {
                                _optimizerService.ApplyValue(CurrentProfile, _currentOptimizationParams[i], value.Values[i]);
                            }
                        }

                        // Teszt futtatása és rajzolás
                        var chartResult = _backtestService.RunBacktest(_candles, CurrentProfile, InitialCash);

                        Result = chartResult;
                        UpdateChart(chartResult);

                        _cachedSingleResult = chartResult;
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

        string stats = "";

        public StrategyTestViewModel(
            BacktestService backtestService,
            IStrategySettingsService strategyService,
            List<Candle> candles,
            StrategyProfile profile,
            OptimizerService optimizerService,
            IIndicatorRegistry indicatorRegistry)
        {
            // Visszatöltjük a checkbox állapotát
            UseOptimization = _cachedUseOptimization;

            _backtestService = backtestService ?? throw new ArgumentNullException(nameof(backtestService));
            _strategyService = strategyService ?? throw new ArgumentNullException(nameof(strategyService));
            _optimizerService = optimizerService ?? throw new ArgumentNullException(nameof(optimizerService));
            _candles = candles ?? throw new ArgumentNullException(nameof(candles));
            _indicatorRegistry = indicatorRegistry;
            _settingsService = new Services.StrategySettingsService();
            CurrentProfile = profile ?? throw new ArgumentNullException(nameof(profile));

            OpenOptimizerCommand = new RelayCommand(o => OpenOptimizer());
            ApplyResultCommand = new RelayCommand(obj => ApplyResult(obj));

            OptimizationResults = new ObservableCollection<OptimizationResult>();

            // ==============================================================================
            // 1. PARAMÉTEREK VISSZAÁLLÍTÁSA (INDEX ALAPJÁN - JAVÍTOTT!)
            // ==============================================================================
            if (_savedOptimizerState != null && _savedOptimizerState.Any())
            {
                var tempVm = new OptimizationViewModel(CurrentProfile);

                // Név helyett INDEX alapján megyünk végig
                for (int i = 0; i < tempVm.AvailableParameters.Count; i++)
                {
                    // Ha a mentett listában létezik ez az index
                    if (i < _savedOptimizerState.Count)
                    {
                        var saved = _savedOptimizerState[i];
                        var current = tempVm.AvailableParameters[i];

                        // Felülírjuk az értékeket vakon, a sorrend alapján
                        current.IsSelected = saved.IsSelected;
                        current.MinValue = saved.MinValue;
                        current.MaxValue = saved.MaxValue;
                        current.Step = saved.Step;
                    }
                }

                // Lista újraépítése a motor számára (hogy működjön a rajzolás)
                _currentOptimizationParams = tempVm.AvailableParameters
                    .Where(p => p.IsSelected)
                    .Select(p => new OptimizationParameter
                    {
                        Rule = p.Rule, // Így megvan a helyes Rule referencia!
                        ParameterName = p.ParameterName,
                        MinValue = p.MinValue,
                        MaxValue = p.MaxValue,
                        Step = p.Step
                    }).ToList();

                UseOptimization = true;
                UpdateOptimizationStatus();
            }

            // ==============================================================================
            // 2. LISTA ÉS KIVÁLASZTÁS VISSZATÖLTÉSE
            // ==============================================================================
            if (_cachedOptimizationResults != null && _cachedOptimizationResults.Any())
            {
                foreach (var res in _cachedOptimizationResults)
                {
                    OptimizationResults.Add(res);
                }

                // Visszajelöljük az utolsót
                if (_cachedSelectedIndex >= 0 && _cachedSelectedIndex < OptimizationResults.Count)
                {
                    // Mivel a _currentOptimizationParams fentebb helyreállt,
                    // a SelectedResult setter most már tudni fogja, mit kell rajzolni!
                    SelectedResult = OptimizationResults[_cachedSelectedIndex];
                }
            }
            else if (_cachedSingleResult != null)
            {
                Result = _cachedSingleResult;
                UpdateChart(_cachedSingleResult);
            }

            if (!string.IsNullOrEmpty(_cachedLogText))
            {
                IndicatorTestValues = _cachedLogText;
            }

            if (_candles.Any())
            {
                Symbol = _candles.First().Symbol;
                StartDate = _candles.Min(c => c.TimestampUtc);
                EndDate = _candles.Max(c => c.TimestampUtc);
            }

            RunCommand = new RelayCommand(o => RunTest());
            EditStrategyCommand = new RelayCommand(OpenStrategyEditor);
            OpenOptimizerCommand = new RelayCommand(o => OpenOptimizer());

            if (OptimizationResults.Any() && SelectedResult == null)
            {
                SelectedResult = OptimizationResults.First();
            }
        }

        private void OpenStrategyEditor(object obj)
        {
            var editorVm = new StrategyEditorViewModel(CurrentProfile, _indicatorRegistry);
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
            if (IsRunning)
            {
                _cts?.Cancel();
                IndicatorTestValues = "Leállítás folyamatban...";
                return;
            }

            if (CurrentProfile == null || _candles == null) return;

            IsRunning = true;
            ProgressValue = 0;

            _cts = new CancellationTokenSource();
            IndicatorTestValues = "Tesztelés...";
            var token = _cts.Token;

            IProgress<int> progressIndicator = new Progress<int>(value => ProgressValue = value);

            // BUFFER a háttérszál és UI közé
            var pendingResults = new ConcurrentQueue<OptimizationResult>();

            // Globális legjobb
            double globalBestScore = double.MinValue;
            OptimizationResult globalBestResult = null;

            OptimizationResults.Clear();

            // UI TIMER - 500ms (nem 300ms, az túl gyakori!)
            var uiTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };

            uiTimer.Tick += (s, args) =>
            {
                // 1. Batch kiszedése
                var batch = new List<OptimizationResult>();
                while (pendingResults.TryDequeue(out var res))
                {
                    batch.Add(res);
                }

                if (batch.Count == 0) return;

                // 2. BULK INSERT (SOKKAL GYORSABB!)
                // Ideiglenesen kikapcsoljuk a CollectionChanged eseményt
                var tempList = new List<OptimizationResult>(OptimizationResults);
                tempList.AddRange(batch);

                // Csak a top 500-at tartjuk meg
                if (tempList.Count > 500)
                {
                    tempList = tempList.OrderByDescending(x => x.Score).Take(500).ToList();
                }

                // EGY LÉPÉSBEN töröljük és töltjük újra (1 UI frissítés!)
                OptimizationResults.Clear();
                foreach (var item in tempList)
                {
                    OptimizationResults.Add(item);
                }

                // 3. CHART FRISSÍTÉS - CSAK 1x a batch-ben!
                var newBest = batch.OrderByDescending(x => x.Score).FirstOrDefault();
                if (newBest != null && newBest.Score > globalBestScore)
                {
                    globalBestScore = newBest.Score;
                    globalBestResult = newBest;

                    if (newBest.EquityCurve != null && newBest.EquityCurve.Count > 0)
                    {
                        // NEM állítjuk a SelectedResult-ot futás közben (lassú!)
                        // Csak a chart-ot frissítjük
                        var tempResult = new BacktestResult
                        {
                            EquityCurve = newBest.EquityCurve,
                            BalanceCurve = newBest.BalanceCurve ?? new List<EquityPoint>(),
                            Trades = newBest.Trades ?? new List<TradeRecord>()
                        };

                        UpdateChart(tempResult);
                    }
                }
            };

            // PROGRESS HANDLER - csak Enqueue, semmi más!
            var realTimeHandler = new Progress<OptimizationResult>(res =>
            {
                pendingResults.Enqueue(res);
            });

            uiTimer.Start();

            try
            {
                await Task.Run(async () =>
                {
                    if (UseOptimization)
                    {
                        if (_currentOptimizationParams == null || !_currentOptimizationParams.Any())
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                                MessageBox.Show("Nincsenek beállítva optimalizációs paraméterek!"));
                            return;
                        }

                        // OPTIMALIZÁLÁS FUTTATÁSA
                        var results = await _optimizerService.OptimizeAsync(
                            _candles,
                            CurrentProfile,
                            _currentOptimizationParams,
                            progressIndicator,
                            token,
                            IsVisualMode,
                            realTimeHandler
                        );

                        // VÉGSŐ FRISSÍTÉS (UI szálon)
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            uiTimer.Stop();

                            // Maradék batch feldolgozása
                            while (pendingResults.TryDequeue(out var res))
                            {
                                results.Add(res);
                            }

                            // RENDEZÉS ÉS VÉGLEGESÍTÉS
                            OptimizationResults.Clear();
                            var sortedResults = results.OrderByDescending(r => r.Score).Take(500).ToList();

                            foreach (var res in sortedResults)
                            {
                                OptimizationResults.Add(res);
                            }

                            _cachedOptimizationResults = OptimizationResults.ToList();

                            // Most már kiválaszthatjuk a legjobbet
                            if (OptimizationResults.Any())
                            {
                                SelectedResult = OptimizationResults.First();

                                var bestResult = SelectedResult;
                                //stats = $"OPTIMALIZÁLT EREDMÉNY:\nProfit: {bestResult.Profit:C0}\n" +
                                //        $"Trades: {bestResult.TradeCount}\nPF: {bestResult.ProfitFactor:F2}\n\n";

                                //var usCulture = CultureInfo.GetCultureInfo("en-US");

                                //stats = $"OPTIMALIZÁLT EREDMÉNY:\n" +
                                //               $"Profit: {bestResult.Profit.ToString("C0", usCulture)}\n" +
                                //               $"Trades: {bestResult.TradeCount}\n" +
                                //               $"PF: {bestResult.ProfitFactor.ToString("F2", usCulture)}\n\n";

                                //stats = $"OPTIMALIZÁLT EREDMÉNY:\nProfit: {bestResult.Profit:N0}\n" +
                                //        $"Trades: {bestResult.TradeCount}\nPF: {bestResult.ProfitFactor:F2}\n\n";
                                // 1. MENTÉS ÉS ALKALMAZÁS LOGIKA
                                // Ha az OptimizerService elmentette a profilt az eredménybe:
                                if (bestResult.OptimizedProfile != null)
                                {
                                    CurrentProfile.EntryGroups = bestResult.OptimizedProfile.EntryGroups;
                                    CurrentProfile.ExitGroups = bestResult.OptimizedProfile.ExitGroups;

                                    _settingsService.SaveProfile(CurrentProfile);
                                    stats = $"OPTIMALIZÁLT EREDMÉNY:\nProfit: {bestResult.Profit:N0}\n" +
                                           $"Trades: {bestResult.TradeCount}\nPF: {bestResult.ProfitFactor:F2}\n" +
                                           "(Paraméterek frissítve és mentve!)\n\n";

                                    if (bestResult.OptimizedProfile != null)
                                    {
                                        stats += "(Paraméterek mentve!)\n\n";
                                    }
                                    else
                                    {
                                        stats += "\n";
                                    }

                                    string paramsText = GetStrategyDetails(CurrentProfile);
                                    IndicatorTestValues = stats + paramsText;

                                    _settingsService.SaveProfile(CurrentProfile);

                                    // Frissítjük a log cache-t is
                                    _cachedLogText = IndicatorTestValues;
                                }

                                // 2. SZÖVEG GENERÁLÁS
                                //string stats = $"OPTIMALIZÁLT EREDMÉNY:\nProfit: {bestResult.Profit:N0}\n" +
                                //               $"Trades: {bestResult.TradeCount}\nPF: {bestResult.ProfitFactor:F2}\n";

                                //if (bestResult.OptimizedProfile != null)
                                //{
                                //    stats += "(Paraméterek mentve!)\n\n";
                                //}
                                //else
                                //{
                                //    stats += "\n";
                                //}

                                //string paramsText = GetStrategyDetails(CurrentProfile);
                                //IndicatorTestValues = stats + paramsText;
                                //_cachedLogText = IndicatorTestValues;
                            }
                        });
                    }
                    else
                    {
                        // SIMA FUTTATÁS
                        progressIndicator.Report(10);
                        token.ThrowIfCancellationRequested();

                        var res = _backtestService.RunBacktest(_candles, CurrentProfile, InitialCash);
                        progressIndicator.Report(100);

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            uiTimer.Stop();
                            Result = res;
                            UpdateChart(res);
                            IndicatorTestValues = $"Egyedi futtatás kész. Profit Faktor: {res.ProfitFactor:N2}";
                            _cachedSingleResult = res;
                            _cachedLogText = IndicatorTestValues;
                        });
                    }

                }, token);

                System.Diagnostics.Debug.WriteLine($"Test completed at {DateTime.Now}");
            }
            catch (OperationCanceledException)
            {
                string message = "A műveletet a felhasználó megszakította.\n(Nincs kiszámolt statisztika)\n\n";
                string paramsText = GetStrategyDetails(CurrentProfile);
                IndicatorTestValues = message + paramsText;
                ProgressValue = 0;
            }
            catch (Exception ex)
            {
                // HIBAKEZELÉS
                System.Diagnostics.Debug.WriteLine($"Error in RunTest: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack: {ex.StackTrace}");

                MessageBox.Show($"Hiba történt: {ex.Message}", "Hiba",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                uiTimer.Stop();
                IsRunning = false;
                _cts?.Dispose();
                _cts = null;
            }
        }

        private void ApplyResult(object obj)
        {
            if (obj is OptimizationResult res && _currentOptimizationParams != null)
            {
                for (int i = 0; i < _currentOptimizationParams.Count; i++)
                {
                    if (i < res.Values.Length)
                    {
                        _optimizerService.ApplyValue(CurrentProfile, _currentOptimizationParams[i], res.Values[i]);
                    }
                }

                System.Windows.MessageBox.Show(
                    $"Beállítások sikeresen alkalmazva!\n\nÚj Score: {res.Score:N2}\nProfit: {res.Profit:N0}$",
                    "Sikeres betöltés",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);

                OnPropertyChanged(nameof(CurrentProfile));
            }
        }

        private void OpenOptimizer()
        {
            var vm = new OptimizationViewModel(CurrentProfile);

            // --- MEMÓRIA VISSZATÖLTÉSE (INDEX ALAPJÁN) ---
            if (_savedOptimizerState != null)
            {
                // Név helyett INDEX alapján
                for (int i = 0; i < vm.AvailableParameters.Count; i++)
                {
                    if (i < _savedOptimizerState.Count)
                    {
                        var saved = _savedOptimizerState[i];
                        var current = vm.AvailableParameters[i];

                        current.IsSelected = saved.IsSelected;
                        current.MinValue = saved.MinValue;
                        current.MaxValue = saved.MaxValue;
                        current.Step = saved.Step;
                    }
                }
            }

            var win = new OptimizationWindow { DataContext = vm, Owner = Application.Current.MainWindow };

            vm.OnRequestClose += (result) =>
            {
                if (result)
                {
                    // --- ÁLLAPOT MENTÉSE (SORRENDHELYESEN) ---
                    _savedOptimizerState = vm.AvailableParameters.Select(p => new OptimizationParameterUI
                    {
                        Name = p.Name,
                        IsSelected = p.IsSelected,
                        MinValue = p.MinValue,
                        MaxValue = p.MaxValue,
                        Step = p.Step,
                    }).ToList();

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

            // 3. BALANCE GÖRBE
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

            // 4. EQUITY GÖRBE
            var equitySeries = new LineSeries
            {
                Title = "Equity (Lebegő)",
                Color = OxyColors.LimeGreen,
                StrokeThickness = 2,
                MarkerType = MarkerType.None,
            };
            equitySeries.Points.AddRange(viewPoints);
            model.Series.Add(equitySeries);

            // 5. KÖTÉSEK (VÉTEL)
            if (res.Trades != null && res.Trades.Any())
            {
                var buySeries = new ScatterSeries
                {
                    MarkerType = MarkerType.Circle,
                    MarkerSize = 4,
                    MarkerFill = OxyColors.White,
                    MarkerStroke = OxyColors.Black,
                    MarkerStrokeThickness = 1,
                    Title = "Vétel"
                };

                // 6. KÖTÉSEK (ELADÁS)
                var sellSeries = new ScatterSeries
                {
                    MarkerType = MarkerType.Circle,
                    MarkerSize = 4,
                    MarkerFill = OxyColors.Red,
                    MarkerStroke = OxyColors.White,
                    MarkerStrokeThickness = 1,
                    Title = "Eladás"
                };

                foreach (var trade in res.Trades)
                {
                    var entryPoint = equityPoints.FirstOrDefault(p => p.Time == trade.EntryDate);
                    if (entryPoint != null)
                    {
                        buySeries.Points.Add(new ScatterPoint(DateTimeAxis.ToDouble(trade.EntryDate), entryPoint.Equity));
                    }

                    if (trade.ExitDate != DateTime.MinValue)
                    {
                        var exitPoint = equityPoints.FirstOrDefault(p => p.Time == trade.ExitDate);
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

        private void UpdateOptimizationStatus()
        {
            if (!UseOptimization)
            {
                OptimizationStatusMessage = "";
                return;
            }

            if (_currentOptimizationParams != null && _currentOptimizationParams.Any())
            {
                OptimizationStatusMessage = " Paraméterek beállítva. Indíthatod a tesztet!";
                OptimizationStatusColor = System.Windows.Media.Brushes.LimeGreen;
            }
            else
            {
                OptimizationStatusMessage = "⚠️ Nincsenek beállítva intervallumok!\nKattints a 'Beállítások' gombra.";
                OptimizationStatusColor = System.Windows.Media.Brushes.OrangeRed;
            }
        }

        private string GetStrategyDetails(StrategyProfile profile)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"--- PARAMÉTEREK (Dátum: {DateTime.Now:yyyy.MM.dd HH:mm}) ---");

            // 1. BELÉPŐK (ENTRY)
            sb.AppendLine("BELÉPŐ SZABÁLYOK (Vétel):");
            foreach (var group in profile.EntryGroups)
            {
                foreach (var rule in group.Rules)
                {
                    string left = $"{rule.LeftIndicatorName}({rule.LeftPeriod})";
                    if (rule.LeftShift > 0) left += $" [Shift-{rule.LeftShift}]";

                    string right;
                    if (rule.RightSourceType == DataSourceType.Value)
                        right = rule.RightValue.ToString();
                    else
                    {
                        right = $"{rule.RightIndicatorName}({rule.RightPeriod})";
                        if (rule.RightShift > 0) right += $" [Shift-{rule.RightShift}]";
                    }

                    // Operátor szépítése
                    string op = rule.Operator.ToString(); // Vagy használhatod a Description attribútumot is ha van

                    sb.AppendLine($"  • {left}  {op}  {right}");
                }
            }

            // 2. KILÉPŐK (EXIT)
            if (profile.ExitGroups.Any())
            {
                sb.AppendLine("\nKILÉPŐ SZABÁLYOK (Eladás):");
                foreach (var group in profile.ExitGroups)
                {
                    foreach (var rule in group.Rules)
                    {
                        string left = $"{rule.LeftIndicatorName}({rule.LeftPeriod})";
                        if (rule.LeftShift > 0) left += $" [Shift-{rule.LeftShift}]";

                        string right = rule.RightSourceType == DataSourceType.Value
                            ? rule.RightValue.ToString()
                            : $"{rule.RightIndicatorName}({rule.RightPeriod})";

                        if (rule.RightSourceType == DataSourceType.Indicator && rule.RightShift > 0)
                            right += $" [Shift-{rule.RightShift}]";

                        sb.AppendLine($"  • {left}  {rule.Operator}  {right}");
                    }
                }
            }

            return sb.ToString();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}