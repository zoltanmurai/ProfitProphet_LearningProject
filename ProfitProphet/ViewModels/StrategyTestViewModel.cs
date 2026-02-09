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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
            // A. HA MÁR FUT -> LEÁLLÍTJUK
            if (IsRunning)
            {
                _cts?.Cancel();
                IndicatorTestValues = "Leállítás folyamatban...";
                return;
            }

            // B. HA NEM FUT -> INDÍTJUK
            if (CurrentProfile == null || _candles == null) return;

            IsRunning = true;
            ProgressValue = 0;

            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            IProgress<int> progressIndicator = new Progress<int>(value => ProgressValue = value);

            // --- 1. PUFFERELÉS ELŐKÉSZÍTÉSE ---
            // Ide gyűjtjük az adatokat a háttérből, hogy ne terheljük a UI-t egyesével
            var pendingResults = new ConcurrentQueue<OptimizationResult>();

            // UI Időzítő: 300ms-enként frissít (így a táblázat nem villog és nem fagy)
            var uiTimer = new DispatcherTimer();
            uiTimer.Interval = TimeSpan.FromMilliseconds(300);

            // Globális legjobb érték követése
            double globalBestScore = double.MinValue;

            // Lista törlése
            OptimizationResults.Clear();

            // --- 2. AZ IDŐZÍTŐ LOGIKÁJA (Ez fut a UI Szálon 300ms-enként) ---
            uiTimer.Tick += (s, args) =>
            {
                // Kivesszük az összeset, ami összegyűlt az elmúlt 300ms-ben
                var batch = new List<OptimizationResult>();
                while (pendingResults.TryDequeue(out var res))
                {
                    batch.Add(res);
                }

                if (batch.Count == 0) return;

                // Csak a legjobbakat dolgozzuk fel a Chartnak, de mindet a listának
                bool chartUpdated = false;

                foreach (var res in batch)
                {
                    // A) Lista frissítése (Okos beszúrás nélkül, csak a végére, majd a végén rendezünk
                    // VAGY egyszerűsített beszúrás, ha nem túl nagy a batch).
                    // A fagyás elkerülése érdekében most csak hozzáadjuk, a rendezést a DataGridre bízzuk,
                    // vagy csak a legjobb 100-at tartjuk meg.

                    OptimizationResults.Add(res);

                    // B) Chart Frissítés (Csak ha rekord)
                    if (res.Score > globalBestScore)
                    {
                        globalBestScore = res.Score;

                        // Csak a legutolsó rekordot rajzoljuk ki a batch-ből
                        if (!chartUpdated && res.EquityCurve != null && res.EquityCurve.Count > 0)
                        {
                            // 1. KIVÁLASZTJUK A LISTÁBAN (Így látszik a kijelölés!)
                            // Mivel levédtük a settert (IsRunning check), ez nem okoz lassulást.
                            SelectedResult = res;

                            // 2. KIRAJZOLJUK A CHARTOT (Kötésekkel együtt!)
                            var tempResult = new BacktestResult
                            {
                                EquityCurve = res.EquityCurve,
                                BalanceCurve = res.BalanceCurve ?? new List<EquityPoint>(),

                                // ITT A LÉNYEG: Átadjuk a kötéseket is, így megjelennek a pöttyök!
                                Trades = res.Trades ?? new List<TradeRecord>()
                            };

                            UpdateChart(tempResult);
                            chartUpdated = true;
                        }
                    }
                }

                // RAM VÉDELEM: Ha túl sok az elem, a régieket/rosszakat kidobjuk
                // Ez kritikus, ha 100.000 iteráció fut!
                if (OptimizationResults.Count > 500)
                {
                    // Ez egy lassú művelet lehet, ritkábban kéne, de a stabilitás miatt:
                    // Inkább csak a lista végét vágjuk le, ha nem fér el.
                    // A leggyorsabb, ha nem rendezzük itt, hanem csak a View-ban.
                    // De ha nagyon kell a rendezés:
                    var sorted = OptimizationResults.OrderByDescending(x => x.Score).Take(200).ToList();
                    OptimizationResults.Clear();
                    foreach (var item in sorted) OptimizationResults.Add(item);
                }
            };

            // --- 3. A HANDLER MÓDOSÍTÁSA ---
            // Mostantól a handler NEM nyúl a UI-hoz, csak bedobja a közösbe.
            var realTimeHandler = new Progress<OptimizationResult>(res =>
            {
                pendingResults.Enqueue(res);
            });

            uiTimer.Start(); // Indul a frissítés

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

                        // Indítás
                        var results = await _optimizerService.OptimizeAsync(
                            _candles,
                            CurrentProfile,
                            _currentOptimizationParams,
                            progressIndicator,
                            token,
                            IsVisualMode,
                            realTimeHandler    // <--- Ez most már csak a Queue-ba ír
                        );

                        // VÉGSŐ FRISSÍTÉS UI SZÁLON
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            uiTimer.Stop(); // Leállítjuk a köztes frissítést

                            // A maradékot még feldolgozzuk
                            while (pendingResults.TryDequeue(out var res)) results.Add(res);

                            OptimizationResults.Clear();
                            // Most már rendezhetjük az egészet nyugodtan
                            foreach (var res in results.OrderByDescending(r => r.Score)) OptimizationResults.Add(res);

                            _cachedOptimizationResults = OptimizationResults.ToList();
                            if (OptimizationResults.Any()) SelectedResult = OptimizationResults.First();
                        });
                    }
                    else
                    {
                        // ... Sima futtatás ág (Változatlan) ...
                        progressIndicator.Report(10);
                        if (token.IsCancellationRequested) return;
                        var res = _backtestService.RunBacktest(_candles, CurrentProfile, InitialCash);
                        progressIndicator.Report(100);

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            uiTimer.Stop(); // Biztos ami biztos
                            Result = res;
                            UpdateChart(res);
                            IndicatorTestValues = $"Egyedi futtatás kész. Profit Faktor: {res.ProfitFactor:N2}";
                            _cachedSingleResult = res;
                            _cachedLogText = IndicatorTestValues;
                        });
                    }

                }, token);
            }
            catch (OperationCanceledException)
            {
                IndicatorTestValues = "A műveletet a felhasználó megszakította.";
                ProgressValue = 0;
            }
            finally
            {
                uiTimer.Stop(); // Mindenképp megállítjuk
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
                OptimizationStatusMessage = "✅ Paraméterek beállítva. Indíthatod a tesztet!";
                OptimizationStatusColor = System.Windows.Media.Brushes.LimeGreen;
            }
            else
            {
                OptimizationStatusMessage = "⚠️ Nincsenek beállítva intervallumok!\nKattints a 'Beállítások' gombra.";
                OptimizationStatusColor = System.Windows.Media.Brushes.OrangeRed;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}