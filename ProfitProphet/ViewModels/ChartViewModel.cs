using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using ProfitProphet.DTOs;
using ProfitProphet.Services;
using ProfitProphet.Services.Indicators;
using ProfitProphet.Settings;
using ProfitProphet.ViewModels.Commands;
using ProfitProphet.ViewModels.Dialogs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ProfitProphet.ViewModels
{
    public sealed class ChartViewModel : INotifyPropertyChanged
    {
        private readonly ChartBuilder _chartBuilder;
        private readonly Func<string, string, Task<List<ChartBuilder.CandleData>>> _loadCandlesAsync;
        private readonly IAppSettingsService _settingsService;
        private AppSettings _appSettings;

        public event Action ChartUpdated;
        public PlotModel ChartModel => _chartBuilder.Model;

        // ===== CROSSHAIR VONALAK =====
        public LineAnnotation CrosshairX { get; private set; }
        public LineAnnotation CrosshairY { get; private set; }
        public TextAnnotation CrosshairTextX { get; private set; }
        public TextAnnotation CrosshairTextY { get; private set; }

        public ObservableCollection<string> Intervals { get; } =
            new(new[] { "M1", "M5", "M15", "H1", "H4", "D1" });

        private string _currentInterval = "D1";
        public string CurrentInterval
        {
            get => _currentInterval;
            set
            {
                if (_currentInterval != value)
                {
                    _ = SaveIndicatorsForCurrentContextAsync();
                    _currentInterval = value;
                    OnPropertyChanged();
                    _ = ReloadAsync();
                }
            }
        }

        private string _currentSymbol = "AMD";
        public string CurrentSymbol
        {
            get => _currentSymbol;
            set
            {
                if (_currentSymbol != value)
                {
                    _ = SaveIndicatorsForCurrentContextAsync();
                    _currentSymbol = value;
                    OnPropertyChanged();
                    _ = ReloadAsync();
                }
            }
        }

        public ObservableCollection<IndicatorType> AvailableIndicatorTypes { get; } =
            new(Enum.GetValues<IndicatorType>());

        private IndicatorType? _selectedIndicatorType;
        public IndicatorType? SelectedIndicatorType
        {
            get => _selectedIndicatorType;
            set
            {
                if (_selectedIndicatorType == value) return;
                _selectedIndicatorType = value;
                OnPropertyChanged();
                if (value.HasValue)
                {
                    AddIndicatorWithDialog(value.Value);
                    _selectedIndicatorType = null;
                    OnPropertyChanged();
                }
            }
        }

        public ObservableCollection<IndicatorConfigDto> Indicators { get; } = new();
        private List<ChartBuilder.CandleData> _candles = new();

        public ICommand AddIndicatorWithDialogCommand { get; }
        public ICommand EditIndicatorCommand { get; }
        public ICommand DeleteIndicatorCommand { get; }

        private readonly IIndicatorRegistry _indicatorRegistry;

        public ChartViewModel(
            IAppSettingsService settingsService,
            AppSettings appSettings,
            Func<string, string, Task<List<ChartBuilder.CandleData>>> loadCandlesAsync,
            ChartBuilder chartBuilder,
            IIndicatorRegistry indicatorRegistry)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _appSettings = appSettings ?? throw new ArgumentNullException(nameof(appSettings));
            _chartBuilder = chartBuilder;
            _loadCandlesAsync = loadCandlesAsync ?? throw new ArgumentNullException(nameof(loadCandlesAsync));
            _indicatorRegistry = indicatorRegistry ?? throw new ArgumentNullException(nameof(indicatorRegistry));

            AddIndicatorWithDialogCommand = new RelayCommand(param =>
            {
                if (param is IndicatorType type)
                {
                    AddIndicatorWithDialog(type);
                }
            }, _ => HasChartData);

            EditIndicatorCommand = new RelayCommand(p => { if (p is IndicatorConfigDto c) EditIndicator(c); }, _ => HasChartData);
            DeleteIndicatorCommand = new RelayCommand(p => { if (p is IndicatorConfigDto c) DeleteIndicator(c); }, _ => HasChartData);
        }

        public bool HasChartData => _candles?.Count > 0;

        public async Task InitializeAsync() => await ReloadAsync();

        private async Task ReloadAsync()
        {
            if (string.IsNullOrWhiteSpace(CurrentSymbol))
            {
                _candles = new();
                OnPropertyChanged(nameof(HasChartData));
                _chartBuilder.Model?.Series.Clear();
                _chartBuilder.Model?.InvalidatePlot(true);
                OnPropertyChanged(nameof(ChartModel));
                return;
            }

            _candles = await _loadCandlesAsync(CurrentSymbol, CurrentInterval);
            MarkGaps();
            OnPropertyChanged(nameof(HasChartData));
            RebuildChart();
            LoadIndicatorsForCurrentContext();
            System.Diagnostics.Debug.WriteLine($"[ChartVM] candles: {_candles?.Count ?? 0} for {CurrentSymbol} {CurrentInterval}");
            ChartUpdated?.Invoke();
        }

        private void MarkGaps()
        {
            if (_candles == null || _candles.Count == 0)
                return;

            var iv = CurrentInterval ?? string.Empty;
            bool isDaily =
                string.Equals(iv, "1d", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(iv, "d1", StringComparison.OrdinalIgnoreCase);

            if (!isDaily)
            {
                foreach (var c in _candles)
                {
                    c.HasGapBefore = false;
                }
                return;
            }

            _candles = _candles
                .OrderBy(c => c.Timestamp)
                .ToList();

            if (_candles.Count == 0)
                return;

            _candles[0].HasGapBefore = false;

            for (int i = 1; i < _candles.Count; i++)
            {
                var prev = _candles[i - 1];
                var cur = _candles[i];
                var prevDate = prev.Timestamp.Date;
                var curDate = cur.Timestamp.Date;
                var diffDays = (curDate - prevDate).TotalDays;
                cur.HasGapBefore = diffDays > 1.0;
            }
        }

        private void RebuildChart()
        {
            _chartBuilder.ShowGapMarkers = _appSettings?.ShowGapMarkers ?? true;

            _chartBuilder.Model?.Annotations.Clear();
            // ELŐSZÖR: Nullázzuk a crosshair referenciákat, mert új model jön
            CrosshairX = null;
            CrosshairY = null;
            CrosshairTextX = null;
            CrosshairTextY = null;

            _chartBuilder.BuildInteractiveChart(_candles, CurrentSymbol, CurrentInterval);

            // MÁSODSZOR: Új crosshair objektumokat hozunk létre az ÚJ modellhez
            SetupCrosshair();

            OnPropertyChanged(nameof(HasChartData));
            OnPropertyChanged(nameof(ChartModel));
            (AddIndicatorWithDialogCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (EditIndicatorCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (DeleteIndicatorCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        // ===== CROSSHAIR FUNKCIÓK - EGYSZERŰSÍTETT VERZIÓ =====
        //private void SetupCrosshair()
        //{
        //    if (_chartBuilder.Model == null) return;

        //    // Mindig új objektumokat hozunk létre (a referenciák már null-ra lettek állítva)
        //    CrosshairX = new LineAnnotation
        //    {
        //        Type = LineAnnotationType.Vertical,
        //        Color = OxyColors.Gray,
        //        StrokeThickness = 0, // Kezdetben láthatatlan
        //        LineStyle = LineStyle.Dash,
        //        Text = "",
        //        TextColor = OxyColors.White,
        //        TextVerticalAlignment = OxyPlot.VerticalAlignment.Bottom,
        //        TextHorizontalAlignment = OxyPlot.HorizontalAlignment.Center,
        //        Layer = AnnotationLayer.AboveSeries,
        //        FontSize = 11,
        //        TextPadding = 3
        //    };

        //    CrosshairY = new LineAnnotation
        //    {
        //        Type = LineAnnotationType.Horizontal,
        //        Color = OxyColors.Gray,
        //        StrokeThickness = 0, // Kezdetben láthatatlan
        //        LineStyle = LineStyle.Dash,
        //        Text = "",
        //        TextColor = OxyColors.White,
        //        TextHorizontalAlignment = OxyPlot.HorizontalAlignment.Right,
        //        TextVerticalAlignment = OxyPlot.VerticalAlignment.Middle,
        //        Layer = AnnotationLayer.AboveSeries,
        //        FontSize = 11,
        //        TextPadding = 3
        //    };

        //    // Hozzáadjuk az ÚJ modellhez
        //    _chartBuilder.Model.Annotations.Add(CrosshairX);
        //    _chartBuilder.Model.Annotations.Add(CrosshairY);
        //}
        private void SetupCrosshair()
        {
            if (_chartBuilder.Model == null) return;

            // Függőleges vonal - SZÖVEG NÉLKÜL
            CrosshairX = new LineAnnotation
            {
                Type = LineAnnotationType.Vertical,
                Color = OxyColors.Gray,
                StrokeThickness = 0,
                LineStyle = LineStyle.Dash,
                Layer = AnnotationLayer.AboveSeries
            };

            // Vízszintes vonal - SZÖVEG NÉLKÜL
            CrosshairY = new LineAnnotation
            {
                Type = LineAnnotationType.Horizontal,
                Color = OxyColors.Gray,
                StrokeThickness = 0,
                LineStyle = LineStyle.Dash,
                Layer = AnnotationLayer.AboveSeries
            };

            // KÜLÖN SZÖVEG ANNOTÁCIÓK - DÁTUMHOZ
            CrosshairTextX = new TextAnnotation
            {
                Text = "",
                TextColor = OxyColors.White,
                Background = OxyColor.FromArgb(200, 30, 30, 30),
                Padding = new OxyThickness(5, 2, 5, 2),
                FontSize = 11,
                Layer = AnnotationLayer.AboveSeries,
                TextVerticalAlignment = OxyPlot.VerticalAlignment.Bottom,
                TextHorizontalAlignment = OxyPlot.HorizontalAlignment.Center
            };

            // KÜLÖN SZÖVEG ANNOTÁCIÓK - ÁRHOZ
            CrosshairTextY = new TextAnnotation
            {
                Text = "",
                TextColor = OxyColors.White,
                Background = OxyColor.FromArgb(200, 30, 30, 30),
                Padding = new OxyThickness(5, 2, 5, 2),
                FontSize = 11,
                Layer = AnnotationLayer.AboveSeries,
                TextVerticalAlignment = OxyPlot.VerticalAlignment.Middle,
                TextHorizontalAlignment = OxyPlot.HorizontalAlignment.Right
            };

            _chartBuilder.Model.Annotations.Add(CrosshairX);
            _chartBuilder.Model.Annotations.Add(CrosshairY);
            _chartBuilder.Model.Annotations.Add(CrosshairTextX);
            _chartBuilder.Model.Annotations.Add(CrosshairTextY);
        }

        /// <summary>
        /// Frissíti a crosshair pozícióját és szövegét
        /// </summary>
        public void UpdateCrosshair(double x, double y)
        {
            if (CrosshairX == null || CrosshairY == null || _chartBuilder.Model == null) return;
            if (_candles == null || _candles.Count == 0) return;

            CrosshairX.X = x;
            CrosshairY.Y = y;

            // DÁTUM - Az X egy INDEX a CategoryAxis-ban!
            try
            {
                int candleIndex = (int)Math.Round(x);

                if (candleIndex >= 0 && candleIndex < _candles.Count)
                {
                    DateTime date = _candles[candleIndex].Timestamp;

                    string dateText = CurrentInterval switch
                    {
                        "M1" or "M5" or "M15" => date.ToString("yyyy.MM.dd HH:mm"),
                        "H1" or "H4" => date.ToString("yyyy.MM.dd HH:00"),
                        "D1" => date.ToString("yyyy.MM.dd"),
                        _ => date.ToString("yyyy.MM.dd")
                    };

                    if (CrosshairTextX != null)
                    {
                        CrosshairTextX.Text = dateText;
                        CrosshairTextX.TextPosition = new DataPoint(x, _chartBuilder.Model.DefaultYAxis.ActualMinimum);
                    }
                }
                else
                {
                    if (CrosshairTextX != null) CrosshairTextX.Text = "";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Crosshair] Hiba: {ex.Message}");
                if (CrosshairTextX != null) CrosshairTextX.Text = "";
            }

            // ÁR
            if (CrosshairTextY != null)
            {
                CrosshairTextY.Text = y.ToString("F2");
                CrosshairTextY.TextPosition = new DataPoint(_chartBuilder.Model.DefaultXAxis.ActualMaximum, y);
            }

            _chartBuilder.Model.InvalidatePlot(false);
        }

        /// <summary>
        /// Megjeleníti a crosshair vonalakat
        /// </summary>
        public void ShowCrosshair()
        {
            if (CrosshairX == null || CrosshairY == null) return;
            CrosshairX.StrokeThickness = 1;
            CrosshairY.StrokeThickness = 1;
            _chartBuilder.Model?.InvalidatePlot(false);
        }

        /// <summary>
        /// Elrejti a crosshair vonalakat
        /// </summary>
        public void HideCrosshair()
        {
            if (CrosshairX == null || CrosshairY == null) return;
            CrosshairX.StrokeThickness = 0;
            CrosshairY.StrokeThickness = 0;

            // Szövegek elrejtése
            if (CrosshairTextX != null) CrosshairTextX.Text = "";
            if (CrosshairTextY != null) CrosshairTextY.Text = "";

            _chartBuilder.Model?.InvalidatePlot(false);
        }

        public async Task ClearDataAsync()
        {
            _candles = new List<ChartBuilder.CandleData>();
            OnPropertyChanged(nameof(HasChartData));
            _chartBuilder.Model?.Series.Clear();
            _chartBuilder.Model?.InvalidatePlot(true);
            OnPropertyChanged(nameof(ChartModel));
        }

        private string GetProfileKey() =>
            $"{CurrentSymbol}|{CurrentInterval}";

        private void LoadIndicatorsForCurrentContext()
        {
            Indicators.Clear();
            if (string.IsNullOrWhiteSpace(CurrentSymbol) || string.IsNullOrWhiteSpace(CurrentInterval))
                return;

            var key = GetProfileKey();
            if (_appSettings.IndicatorProfiles != null &&
                _appSettings.IndicatorProfiles.TryGetValue(key, out var list) &&
                list != null)
            {
                foreach (var cfg in list)
                    Indicators.Add(Clone(cfg));
            }

            ApplyIndicatorsFromList();
        }

        public void ReloadSettings(AppSettings settings)
        {
            _appSettings = settings ?? throw new ArgumentNullException(nameof(settings));
            LoadIndicatorsForCurrentContext();
        }

        private void AddIndicatorWithDialog(IndicatorType targetType)
        {
            var indicatorDef = _indicatorRegistry.Resolve(targetType);
            var newConfig = new IndicatorConfigDto
            {
                Type = targetType,
                IsEnabled = true,
                Parameters = new Dictionary<string, string>(),
                Color = "#FFFFFF"
            };

            var win = new Views.IndicatorSettingsWindow(newConfig, indicatorDef);
            if (Application.Current?.MainWindow != null)
                win.Owner = Application.Current.MainWindow;

            if (win.ShowDialog() == true)
            {
                Indicators.Add(newConfig);
                ApplyIndicatorsFromList();
                _ = SaveIndicatorsForCurrentContextAsync();
            }
        }

        private void EditIndicator(IndicatorConfigDto cfg)
        {
            if (cfg is null) return;
            var indicatorDef = _indicatorRegistry.Resolve(cfg.Type);
            var copy = Clone(cfg);

            var win = new Views.IndicatorSettingsWindow(copy, indicatorDef);
            if (Application.Current?.MainWindow != null)
                win.Owner = Application.Current.MainWindow;

            if (win.ShowDialog() == true)
            {
                var ix = Indicators.IndexOf(cfg);
                if (ix >= 0)
                {
                    Indicators[ix] = copy;
                }
                ApplyIndicatorsFromList();
                _ = SaveIndicatorsForCurrentContextAsync();
            }
        }

        private void DeleteIndicator(IndicatorConfigDto cfg)
        {
            if (cfg is null) return;
            Indicators.Remove(cfg);
            ApplyIndicatorsFromList();
            _ = SaveIndicatorsForCurrentContextAsync();
        }

        private void ApplyIndicatorsFromList()
        {
            _chartBuilder.ClearIndicatorsForSymbol(CurrentSymbol);
            foreach (var it in Indicators)
                AddIndicatorToChartSettings(it);
            RebuildChart();
        }

        private void AddIndicatorToChartSettings(IndicatorConfigDto cfg)
        {
            switch (cfg.Type)
            {
                case IndicatorType.EMA:
                    _chartBuilder.AddIndicatorToSymbol(CurrentSymbol, "ema", p =>
                    {
                        p["Period"] = ParseInt(cfg.Parameters, "Period", 20);
                        p["Source"] = "Close";
                        p["Color"] = cfg.Color;
                    });
                    break;
                case IndicatorType.SMA:
                    _chartBuilder.AddIndicatorToSymbol(CurrentSymbol, "sma", p =>
                    {
                        p["Period"] = ParseInt(cfg.Parameters, "Period", 20);
                        p["Source"] = "Close";
                        p["Color"] = cfg.Color;
                    });
                    break;
                case IndicatorType.Stochastic:
                    _chartBuilder.AddIndicatorToSymbol(CurrentSymbol, "stoch", p =>
                    {
                        p["kPeriod"] = ParseInt(cfg.Parameters, "kPeriod", 14);
                        p["dPeriod"] = ParseInt(cfg.Parameters, "dPeriod", 3);
                        p["outputD"] = ParseString(cfg.Parameters, "outputD", "true");
                    });
                    break;
                case IndicatorType.CMF:
                    _chartBuilder.AddIndicatorToSymbol(CurrentSymbol, "cmf", p =>
                    {
                        p["Period"] = ParseInt(cfg.Parameters, "Period", 20);
                        p["MaPeriod"] = ParseInt(cfg.Parameters, "MaPeriod", 10);
                    });
                    break;
                case IndicatorType.RSI:
                    _chartBuilder.AddIndicatorToSymbol(CurrentSymbol, "rsi", p =>
                    {
                        p["Period"] = ParseInt(cfg.Parameters, "Period", 14);
                        p["Source"] = "Close";
                        p["Color"] = cfg.Color;
                    });
                    break;
                case IndicatorType.MACD:
                    _chartBuilder.AddIndicatorToSymbol(CurrentSymbol, "macd", p =>
                    {
                        p["FastPeriod"] = ParseInt(cfg.Parameters, "FastPeriod", 12);
                        p["SlowPeriod"] = ParseInt(cfg.Parameters, "SlowPeriod", 26);
                        p["SignalPeriod"] = ParseInt(cfg.Parameters, "SignalPeriod", 9);
                        p["Source"] = "Close";
                    });
                    break;
                case IndicatorType.Bollinger:
                    _chartBuilder.AddIndicatorToSymbol(CurrentSymbol, "bb", p =>
                    {
                        p["Period"] = ParseInt(cfg.Parameters, "Period", 20);
                        p["Multiplier"] = ParseDouble(cfg.Parameters, "Multiplier", 2.0);
                        p["Source"] = "Close";
                    });
                    break;
            }
        }

        private static string? GetValueIgnoreCase(Dictionary<string, string> dict, string key)
        {
            if (dict == null) return null;
            var match = dict.Keys.FirstOrDefault(k => k.Equals(key, StringComparison.OrdinalIgnoreCase));
            return match != null ? dict[match] : null;
        }

        private static int ParseInt(Dictionary<string, string> dict, string key, int def)
        {
            var val = GetValueIgnoreCase(dict, key);
            if (val != null && double.TryParse(val, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var d))
                return (int)d;
            return def;
        }

        private static double ParseDouble(Dictionary<string, string> dict, string key, double def)
        {
            var val = GetValueIgnoreCase(dict, key);
            if (val != null && double.TryParse(val, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var v))
                return v;
            return def;
        }

        private static string ParseString(Dictionary<string, string> dict, string key, string def)
        {
            return GetValueIgnoreCase(dict, key) ?? def;
        }

        private async Task SaveIndicatorsForCurrentContextAsync()
        {
            if (string.IsNullOrWhiteSpace(CurrentSymbol) || string.IsNullOrWhiteSpace(CurrentInterval))
                return;

            var key = GetProfileKey();
            _appSettings.IndicatorProfiles[key] =
                Indicators.Select(Clone).ToList();
            await _settingsService.SaveSettingsAsync(_appSettings);
        }

        private static IndicatorConfigDto Clone(IndicatorConfigDto s) =>
            new()
            {
                Type = s.Type,
                IsEnabled = s.IsEnabled,
                Parameters = s.Parameters.ToDictionary(k => k.Key, v => v.Value),
                Color = s.Color
            };

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? n = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
