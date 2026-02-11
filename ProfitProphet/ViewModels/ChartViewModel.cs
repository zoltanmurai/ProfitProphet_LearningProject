using OxyPlot;
using OxyPlot.Annotations;
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
        //public LineAnnotation CursorLineX { get; private set; }
        //public LineAnnotation CursorLineY { get; private set; }

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

                // HA KIVÁLASZTOTTUNK VALAMIT, INDÍTJUK A FOLYAMATOT:
                if (value.HasValue)
                {
                    // Itt volt a hiba: át kell adni a "value.Value"-t paraméterként!
                    AddIndicatorWithDialog(value.Value);

                    // Visszaállítjuk null-ra, hogy újra kiválasztható legyen ugyanaz
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
            //_indicatorRegistry = indicatorRegistry;
            _indicatorRegistry = indicatorRegistry ?? throw new ArgumentNullException(nameof(indicatorRegistry));

            AddIndicatorWithDialogCommand = new RelayCommand(param =>
            {
                // Ellenőrizzük, hogy a kapott paraméter tényleg IndicatorType-e
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

            // GAP DETEKTÁLÁS – hétvége / szünnap jelölése a CandleData-ban
            MarkGaps();

            OnPropertyChanged(nameof(HasChartData)); //Chart may be empty, then message appears in UI (XAML)
            RebuildChart();
            LoadIndicatorsForCurrentContext();

            System.Diagnostics.Debug.WriteLine($"[ChartVM] candles: {_candles?.Count ?? 0} for {CurrentSymbol} {CurrentInterval}");
            ChartUpdated?.Invoke();
        }

        // ---- GAP DETEKTÁLÁS SEGÉDFÜGGVÉNY ----
        private void MarkGaps()
        {
            if (_candles == null || _candles.Count == 0)
                return;

            // csak napi idősornál értelmezzük (D1)
            //bool isDaily = string.Equals(CurrentInterval, "1D", StringComparison.OrdinalIgnoreCase);
            var iv = CurrentInterval ?? string.Empty;
            bool isDaily =
                string.Equals(iv, "1d", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(iv, "d1", StringComparison.OrdinalIgnoreCase);

            if (!isDaily)
            {
                // minden más idősornál töröljük a jelölést
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

            // első gyertyának sosem lesz gap előtte
            _candles[0].HasGapBefore = false;

            for (int i = 1; i < _candles.Count; i++)
            {
                var prev = _candles[i - 1];
                var cur = _candles[i];

                var prevDate = prev.Timestamp.Date;
                var curDate = cur.Timestamp.Date;

                var diffDays = (curDate - prevDate).TotalDays;

                // ha több mint 1 nap telt el a két gyertya között -> hétvége / szünnap
                cur.HasGapBefore = diffDays > 1.0;
                //_candles[i].HasGapBefore = (cur - prev).TotalDays > 1.0;
            }
        }

        private void RebuildChart()
        {
            _chartBuilder.ShowGapMarkers = _appSettings?.ShowGapMarkers ?? true;
            _chartBuilder.BuildInteractiveChart(_candles, CurrentSymbol, CurrentInterval);
            //SetupCursorAnnotations();
            OnPropertyChanged(nameof(HasChartData));
            OnPropertyChanged(nameof(ChartModel));
            (AddIndicatorWithDialogCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (EditIndicatorCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (DeleteIndicatorCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        public async Task ClearDataAsync()
        {
            _candles = new List<ChartBuilder.CandleData>();
            OnPropertyChanged(nameof(HasChartData));
            _chartBuilder.Model?.Series.Clear();
            _chartBuilder.Model?.InvalidatePlot(true);
            OnPropertyChanged(nameof(ChartModel));
        }

        // ---- PERSISTENCE HELPER ----
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

            // Ha betöltöttük, rajzoljuk újra az indikátorokat a charton
            ApplyIndicatorsFromList();
        }

        // opcionális, ha SettingsWindow után új AppSettings-et töltesz:
        public void ReloadSettings(AppSettings settings)
        {
            _appSettings = settings ?? throw new ArgumentNullException(nameof(settings));
            // aktuális symbol/interval-ra újratöltjük az indikátorokat
            LoadIndicatorsForCurrentContext();
        }

        //private void AddIndicatorWithDialog()
        //{
        //    if (SelectedIndicatorType is not IndicatorType t) return;

        //    var cfg = DefaultFor(t);
        //    if (!IndicatorSettingsDialog.Show(ref cfg))
        //    {
        //        SelectedIndicatorType = null;
        //        return;
        //    }

        //    Indicators.Add(cfg);
        //    ApplyIndicatorsFromList();

        //    _ = SaveIndicatorsForCurrentContextAsync();
        //    SelectedIndicatorType = null;
        //}
        private void AddIndicatorWithDialog(IndicatorType targetType)
        {
            // 1. Megkérdezzük a Registry-t: "Mi ez az indikátor és mik a paraméterei?"
            // (Ez a kulcs! Ha ez nincs, az ablak nem tudja mit rajzoljon ki)
            var indicatorDef = _indicatorRegistry.Resolve(targetType);

            // 2. Létrehozunk egy alap konfigurációt (üres paraméterekkel)
            var newConfig = new IndicatorConfigDto
            {
                Type = targetType,
                IsEnabled = true,
                Parameters = new Dictionary<string, string>(),
                Color = "#FFFFFF" // Alapértelmezett szín (majd a ChartBuilder felülírja ha kell)
            };

            // 3. MEGNYITJUK AZ ÚJ ABLAKOT (IndicatorSettingsWindow)
            // Átadjuk neki a configot ÉS a definíciót is!
            var win = new Views.IndicatorSettingsWindow(newConfig, indicatorDef);

            // Szülő ablak beállítása (hogy középen legyen)
            if (Application.Current?.MainWindow != null)
                win.Owner = Application.Current.MainWindow;

            // 4. Ha a felhasználó a "Mentés" gombra kattintott...
            if (win.ShowDialog() == true)
            {
                // ...akkor a newConfig.Parameters már fel van töltve a beírt adatokkal!
                Indicators.Add(newConfig);

                // Chart frissítése
                ApplyIndicatorsFromList();
                _ = SaveIndicatorsForCurrentContextAsync();
            }
        }

        private void EditIndicator(IndicatorConfigDto cfg)
        {
            if (cfg is null) return;

            // 1. Lépés: Megkeressük a definíciót (hogy tudjuk, milyen mezőket kell kirajzolni)
            var indicatorDef = _indicatorRegistry.Resolve(cfg.Type);

            // 2. Készítünk egy másolatot a konfigról (hogy ne az élőt szerkesszük, ha a Mégse gombra nyom)
            var copy = Clone(cfg);

            // 3. MEGNYITJUK AZ ÚJ ABLAKOT (IndicatorSettingsWindow) a régi helyett
            var win = new Views.IndicatorSettingsWindow(copy, indicatorDef);

            if (Application.Current?.MainWindow != null)
                win.Owner = Application.Current.MainWindow;

            // 4. Ha a felhasználó mentett...
            if (win.ShowDialog() == true)
            {
                // ...megkeressük az eredetit a listában és kicseréljük az újra (amiben már az új paraméterek vannak)
                var ix = Indicators.IndexOf(cfg);
                if (ix >= 0)
                {
                    Indicators[ix] = copy;
                }

                // Chart frissítése
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
                        p["Period"] = ParseInt(cfg.Parameters, "Period", 20); // Int
                        p["Source"] = "Close";
                        p["Color"] = cfg.Color; // Szín átadása
                    });
                    break;

                case IndicatorType.SMA:
                    _chartBuilder.AddIndicatorToSymbol(CurrentSymbol, "sma", p =>
                    {
                        p["Period"] = ParseInt(cfg.Parameters, "Period", 20); // Int
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

                // --- AZ ÚJ INDIKÁTOROK (Ezek hiányoztak!) ---

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
                return (int)d; // Biztonságosabb double-ként olvasni majd int-re castolni
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
                Indicators.Select(Clone).ToList();   // klónozunk, ne élő referenciát ments

            await _settingsService.SaveSettingsAsync(_appSettings);
        }

        private static string ParseOrDefault(Dictionary<string, string> dict, string key, int def)
        {
            if (dict.TryGetValue(key, out var s) && int.TryParse(s, out var v))
                return v.ToString();
            return def.ToString();
        }

        //private static IndicatorConfigDto DefaultFor(IndicatorType t) => t switch
        //{
        //    IndicatorType.EMA => new() { Type = t, IsEnabled = true, Parameters = new() { ["period"] = "20" } },
        //    IndicatorType.SMA => new() { Type = t, IsEnabled = true, Parameters = new() { ["period"] = "20" } },
        //    IndicatorType.Stochastic => new() { Type = t, IsEnabled = true, Parameters = new() { ["kPeriod"] = "14", ["dPeriod"] = "3" } },
        //    IndicatorType.CMF => new() { Type = t, IsEnabled = true, Parameters = new() { ["period"] = "20" } },
        //    _ => new() { Type = t, IsEnabled = true }
        //};

        private static IndicatorConfigDto DefaultFor(IndicatorType t) => t switch
        {
            IndicatorType.EMA => new() { Type = t, IsEnabled = true, Parameters = new() { ["period"] = "20" } },
            IndicatorType.SMA => new() { Type = t, IsEnabled = true, Parameters = new() { ["period"] = "20" } },
            IndicatorType.Stochastic => new() { Type = t, IsEnabled = true, Parameters = new() { ["kPeriod"] = "14", ["dPeriod"] = "3" } },

            // UPDATE: Added maPeriod with default value 10
            IndicatorType.CMF => new()
            {
                Type = t,
                IsEnabled = true,
                Parameters = new()
                {
                    ["period"] = "20",
                    ["maPeriod"] = "10"
                }
            },

            _ => new() { Type = t, IsEnabled = true }
        };

        //private void SetupCursorAnnotations()
        //{
        //    if (_chartBuilder.Model == null) return;

        //    // Ha már léteznek, nem hozzuk létre újra, csak visszaadjuk a modellhez
        //    if (CursorLineX == null)
        //    {
        //        CursorLineX = new LineAnnotation
        //        {
        //            Type = LineAnnotationType.Vertical,
        //            Color = OxyColors.White,
        //            StrokeThickness = 1,
        //            LineStyle = LineStyle.Dash,
        //            Text = "",
        //            TextColor = OxyColors.White,
        //            TextVerticalAlignment = OxyPlot.VerticalAlignment.Bottom,
        //            Layer = AnnotationLayer.AboveSeries
        //        };
        //    }

        //    if (CursorLineY == null)
        //    {
        //        CursorLineY = new LineAnnotation
        //        {
        //            Type = LineAnnotationType.Horizontal,
        //            Color = OxyColors.White,
        //            StrokeThickness = 1,
        //            LineStyle = LineStyle.Dash,
        //            Text = "",
        //            TextColor = OxyColors.White,
        //            TextHorizontalAlignment = OxyPlot.HorizontalAlignment.Right,
        //            Layer = AnnotationLayer.AboveSeries
        //        };
        //    }

        //    // Biztosítjuk, hogy benne legyenek a modellben
        //    if (!_chartBuilder.Model.Annotations.Contains(CursorLineX))
        //    {
        //        _chartBuilder.Model.Annotations.Add(CursorLineX);
        //    }

        //    if (!_chartBuilder.Model.Annotations.Contains(CursorLineY))
        //    {
        //        _chartBuilder.Model.Annotations.Add(CursorLineY);
        //    }
        //}

        private static IndicatorConfigDto Clone(IndicatorConfigDto s) =>
            new()
            {
                Type = s.Type,
                IsEnabled = s.IsEnabled,
                Parameters = s.Parameters.ToDictionary(k => k.Key, v => v.Value)
            };

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? n = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
