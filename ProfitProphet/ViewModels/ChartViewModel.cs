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

namespace ProfitProphet.ViewModels
{
    public sealed class ChartViewModel : INotifyPropertyChanged
    {
        private readonly ChartBuilder _chartBuilder;
        private readonly Func<string, string, Task<List<ChartBuilder.CandleData>>> _loadCandlesAsync;

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
                    _currentSymbol = value;
                    OnPropertyChanged();
                    _ = ReloadAsync();
                }
            }
        }

        public ObservableCollection<IndicatorType> AvailableIndicatorTypes { get; } =
            new(Enum.GetValues<IndicatorType>());

        private IndicatorType _selectedIndicatorType = IndicatorType.EMA;
        public IndicatorType SelectedIndicatorType
        {
            get => _selectedIndicatorType;
            set { _selectedIndicatorType = value; OnPropertyChanged(); }
        }

        public ObservableCollection<IndicatorConfigDto> Indicators { get; } = new();

        private List<ChartBuilder.CandleData> _candles = new();

        public ICommand AddIndicatorWithDialogCommand { get; }
        public ICommand EditIndicatorCommand { get; }
        public ICommand DeleteIndicatorCommand { get; }

        public ChartViewModel(Func<string, string, Task<List<ChartBuilder.CandleData>>> loadCandlesAsync)
        {
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
            _candles = await _loadCandlesAsync(CurrentSymbol, CurrentInterval);
            OnPropertyChanged(nameof(HasChartData)); //Chart may be empty, then message appears in UI (XAML)
            RebuildChart();

            // If you want the UI list to mirror persisted settings, you can populate it here later.
            System.Diagnostics.Debug.WriteLine($"[ChartVM] candles: {_candles?.Count ?? 0} for {CurrentSymbol} {CurrentInterval}");
        }

        private void RebuildChart()
        {
            _chartBuilder.BuildInteractiveChart(_candles, CurrentSymbol, CurrentInterval);
            OnPropertyChanged(nameof(ChartModel));
            (AddIndicatorWithDialogCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (EditIndicatorCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (DeleteIndicatorCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        private void AddIndicatorWithDialog()
        {
            var cfg = DefaultFor(SelectedIndicatorType);
            if (!IndicatorSettingsDialog.Show(ref cfg)) return;

            Indicators.Add(cfg);
            ApplyIndicatorsFromList(); // persists via ChartBuilder's ChartSettings and redraws
        }

        private void EditIndicator(IndicatorConfigDto cfg)
        {
            var copy = Clone(cfg);
            if (!IndicatorSettingsDialog.Show(ref copy)) return;

            var ix = Indicators.IndexOf(cfg);
            if (ix >= 0) Indicators[ix] = copy;

            ApplyIndicatorsFromList();
        }

        private void DeleteIndicator(IndicatorConfigDto cfg)
        {
            Indicators.Remove(cfg);
            ApplyIndicatorsFromList();
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
            // The IDs must match your Indicator implementations.
            // EMA is present in your project with Id = "ema".
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
                    // Implement SmaIndicator (Id "sma") and register it; until then this is disabled.
                    // _chartBuilder.AddIndicatorToSymbol(CurrentSymbol, "sma", p => { ... });
                    break;

                case IndicatorType.Stochastic:
                    // Implement StochasticIndicator (Id "stoch") and register it; until then this is disabled.
                    // _chartBuilder.AddIndicatorToSymbol(CurrentSymbol, "stoch", p => { ... });
                    break;

                case IndicatorType.CMF:
                    // Implement CmfIndicator (Id "cmf") and ensure Volume is available; until then this is disabled.
                    // _chartBuilder.AddIndicatorToSymbol(CurrentSymbol, "cmf", p => { ... });
                    break;
            }
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