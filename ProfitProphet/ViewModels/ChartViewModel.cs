using OxyPlot;
using ProfitProphet.DTOs;
using ProfitProphet.Services;
using ProfitProphet.ViewModels.Commands;
using ProfitProphet.ViewModels.Dialogs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using ProfitProphet.Settings;

namespace ProfitProphet.ViewModels
{
    public sealed class ChartViewModel : INotifyPropertyChanged
    {
        private readonly ChartBuilder _chartBuilder;
        private readonly Func<string, string, Task<List<ChartBuilder.CandleData>>> _loadCandlesAsync;

        private readonly IAppSettingsService _settingsService;
        private AppSettings _appSettings;

        public PlotModel ChartModel => _chartBuilder.Model;

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

                if (value is not null &&
                    HasChartData &&
                    AddIndicatorWithDialogCommand.CanExecute(null))
                {
                    AddIndicatorWithDialogCommand.Execute(null);
                }
            }
        }

        public ObservableCollection<IndicatorConfigDto> Indicators { get; } = new();

        private List<ChartBuilder.CandleData> _candles = new();

        public ICommand AddIndicatorWithDialogCommand { get; }
        public ICommand EditIndicatorCommand { get; }
        public ICommand DeleteIndicatorCommand { get; }

        public ChartViewModel(
            IAppSettingsService settingsService,
            AppSettings appSettings,
            Func<string, string, Task<List<ChartBuilder.CandleData>>> loadCandlesAsync)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _appSettings = appSettings ?? throw new ArgumentNullException(nameof(appSettings));

            _chartBuilder = new ChartBuilder();
            _loadCandlesAsync = loadCandlesAsync ?? throw new ArgumentNullException(nameof(loadCandlesAsync));

            AddIndicatorWithDialogCommand = new RelayCommand(_ => AddIndicatorWithDialog(), _ => HasChartData);
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

        private void AddIndicatorWithDialog()
        {
            if (SelectedIndicatorType is not IndicatorType t) return;

            var cfg = DefaultFor(t);
            if (!IndicatorSettingsDialog.Show(ref cfg))
            {
                SelectedIndicatorType = null;
                return;
            }

            Indicators.Add(cfg);
            ApplyIndicatorsFromList();

            _ = SaveIndicatorsForCurrentContextAsync();
            SelectedIndicatorType = null;
        }

        private void EditIndicator(IndicatorConfigDto cfg)
        {
            if (cfg is null) return;

            var copy = Clone(cfg);
            if (!IndicatorSettingsDialog.Show(ref copy)) return;

            var ix = Indicators.IndexOf(cfg);
            if (ix >= 0) Indicators[ix] = copy;

            ApplyIndicatorsFromList();
            _ = SaveIndicatorsForCurrentContextAsync();
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
                        p["Period"] = ParseOrDefault(cfg.Parameters, "period", 20);
                        p["Source"] = "Close";
                    });
                    break;

                case IndicatorType.SMA:
                    _chartBuilder.AddIndicatorToSymbol(CurrentSymbol, "sma", p =>
                    {
                        p["Period"] = ParseOrDefault(cfg.Parameters, "period", 20);
                        p["Source"] = "Close";
                    });
                    break;

                case IndicatorType.Stochastic:
                    _chartBuilder.AddIndicatorToSymbol(CurrentSymbol, "stoch", p =>
                    {
                        p["kPeriod"] = ParseOrDefault(cfg.Parameters, "kPeriod", 14);
                        p["dPeriod"] = ParseOrDefault(cfg.Parameters, "dPeriod", 3);
                        p["outputD"] = cfg.Parameters.TryGetValue("outputD", out var v)
                                        ? v
                                        : "true";
                    });
                    break;

                case IndicatorType.CMF:
                    // későbbre: cmf indikátor
                    break;
            }
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

        private static IndicatorConfigDto DefaultFor(IndicatorType t) => t switch
        {
            IndicatorType.EMA => new() { Type = t, IsEnabled = true, Parameters = new() { ["period"] = "20" } },
            IndicatorType.SMA => new() { Type = t, IsEnabled = true, Parameters = new() { ["period"] = "20" } },
            IndicatorType.Stochastic => new() { Type = t, IsEnabled = true, Parameters = new() { ["kPeriod"] = "14", ["dPeriod"] = "3" } },
            IndicatorType.CMF => new() { Type = t, IsEnabled = true, Parameters = new() { ["period"] = "20" } },
            _ => new() { Type = t, IsEnabled = true }
        };

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
