using ProfitProphet.Entities;
using ProfitProphet.Models.Strategies;
using ProfitProphet.Services;
using ProfitProphet.ViewModels.Commands;
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
    public class OptimizationViewModel : INotifyPropertyChanged
    {
        private readonly OptimizerService _optimizerService;
        private readonly StrategyProfile _profile;
        private readonly List<Candle> _candles;
        private bool _isRunning;
        private string _statusText = "Készen áll az indításra. Jelöld ki a paramétereket!";
        private double _progressValue;
        public ObservableCollection<OptimizationResult> OptimizationResults { get; } = new();

        // EZT A SORT HAGYTAM KI VÉLETLENÜL - MOST PÓTOLVA:
        public event Action<bool> OnRequestClose;

        public ObservableCollection<OptimizationParameterUI> AvailableParameters { get; } = new();

        public ICommand RunOptimizationCommand { get; }

        public bool IsRunning
        {
            get => _isRunning;
            set { _isRunning = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsNotRunning)); }
        }

        public bool IsNotRunning => !_isRunning;

        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); }
        }

        public double ProgressValue
        {
            get => _progressValue;
            set { _progressValue = value; OnPropertyChanged(); }
        }

        public OptimizationViewModel(StrategyProfile profile, List<Candle> candles, OptimizerService optimizerService)
        {
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
            _candles = candles ?? throw new ArgumentNullException(nameof(candles));
            _optimizerService = optimizerService ?? throw new ArgumentNullException(nameof(optimizerService));

            LoadParametersFromProfile();

            RunOptimizationCommand = new RelayCommand(async (o) => await RunOptimization(), (o) => !IsRunning);
        }

        private void LoadParametersFromProfile()
        {
            AvailableParameters.Clear();

            // 1. ENTRY (Vételi) csoportok feldolgozása
            if (_profile.EntryGroups != null)
            {
                foreach (var group in _profile.EntryGroups)
                {
                    foreach (var rule in group.Rules)
                    {
                        AddRulesSpecificParameters(rule, true);
                    }
                }
            }

            // 2. EXIT (Eladási) csoportok feldolgozása
            if (_profile.ExitGroups != null)
            {
                foreach (var group in _profile.ExitGroups)
                {
                    foreach (var rule in group.Rules)
                    {
                        AddRulesSpecificParameters(rule, false);
                    }
                }
            }
        }

        private void AddRulesSpecificParameters(StrategyRule rule, bool isEntry)
        {
            string prefix = isEntry ? "ENTRY" : "EXIT";

            if (rule.LeftPeriod > 0)
            {
                AddParameterUI(rule, isEntry, "LeftPeriod", rule.LeftPeriod,
                    $"{prefix} - {rule.LeftIndicatorName} (Bal): Period");
            }

            if (rule.RightSourceType == DataSourceType.Indicator && rule.RightPeriod > 0)
            {
                AddParameterUI(rule, isEntry, "RightPeriod", rule.RightPeriod,
                    $"{prefix} - {rule.RightIndicatorName} (Jobb): Period");
            }

            if (rule.RightSourceType == DataSourceType.Value)
            {
                AddParameterUI(rule, isEntry, "RightValue", rule.RightValue,
                    $"{prefix} - {rule.LeftIndicatorName} vs Fix Érték");
            }
        }

        private void AddParameterUI(StrategyRule rule, bool isEntry, string paramName, double currentValue, string displayName)
        {
            int defaultMin = (int)Math.Max(1, Math.Floor(currentValue * 0.5));
            int defaultMax = (int)Math.Ceiling(currentValue * 1.5);

            if (defaultMax <= defaultMin) defaultMax = defaultMin + 5;

            AvailableParameters.Add(new OptimizationParameterUI
            {
                Name = displayName,
                Rule = rule,
                ParameterName = paramName,
                IsEntrySide = isEntry,
                IsSelected = false,
                CurrentValue = currentValue,
                MinValue = defaultMin,
                MaxValue = defaultMax
            });
        }

        private async Task RunOptimization()
        {
            var selectedParams = AvailableParameters.Where(p => p.IsSelected).ToList();

            if (!selectedParams.Any())
            {
                StatusText = "Hiba: Nincs kiválasztva paraméter!";
                return;
            }

            IsRunning = true;
            //StatusText = $"Optimalizálás futtatása {selectedParams.Count} paraméteren...";
            StatusText = "Számítás folyamatban...";
            OptimizationResults.Clear();
            //ProgressValue = 0;

            try
            {
                var optParams = selectedParams.Select(p => new OptimizationParameter
                {
                    Rule = p.Rule,
                    IsEntrySide = p.IsEntrySide,
                    ParameterName = p.ParameterName,
                    MinValue = p.MinValue,
                    MaxValue = p.MaxValue
                }).ToList();

                var results = await _optimizerService.OptimizeAsync(_candles, _profile, optParams);

                if (results != null && results.Any())
                {
                    foreach (var res in results)
                    {
                        OptimizationResults.Add(res);
                    }
                    StatusText = $"KÉSZ! {results.Count} eredményt találtam.";
                }
                //StatusText = $"KÉSZ! Score: {result.Score:N2} | Profit: {result.Profit:N0}$ | Kötés: {result.TradeCount}";
                else
                {
                    StatusText = "Nem találtam megfelelő beállítást (kevés kötés vagy rossz eredmény).";
                


                    //MessageBox.Show($"Optimalizálás sikeres!\n\nÚj Profit: {result.Profit:N0}$\nKötések száma: {result.TradeCount}\nDrawdown: {result.Drawdown:P1}",
                    //                "Eredmény", MessageBoxButton.OK, MessageBoxImage.Information);

                    //RefreshCurrentValues(selectedParams);

                    // Opcionális: Ha azt akarod, hogy sikeres futás után automatikusan záródjon be az ablak:
                    // OnRequestClose?.Invoke(true); 
                }
            }
            catch (Exception ex)
            {
                StatusText = "Hiba: " + ex.Message;
                MessageBox.Show(ex.ToString());
            }
            finally
            {
                IsRunning = false;
            }
        }

        private void RefreshCurrentValues(List<OptimizationParameterUI> selectedParams)
        {
            foreach (var p in selectedParams)
            {
                switch (p.ParameterName)
                {
                    case "LeftPeriod":
                        p.CurrentValue = p.Rule.LeftPeriod;
                        break;
                    case "RightPeriod":
                        p.CurrentValue = p.Rule.RightPeriod;
                        break;
                    case "RightValue":
                        p.CurrentValue = p.Rule.RightValue;
                        break;
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}