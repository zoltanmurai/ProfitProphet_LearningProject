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
        private readonly StrategyProfile _profile;

        // Esemény az ablak bezárásához (True = OK, False = Mégse)
        public event Action<bool> OnRequestClose;

        // A paraméterek listája
        public ObservableCollection<OptimizationParameterUI> AvailableParameters { get; } = new();

        // Parancsok
        public ICommand OkCommand { get; }
        public ICommand CancelCommand { get; }

        public OptimizationViewModel(StrategyProfile profile)
        {
            _profile = profile;

            // Betöltjük az adatokat a profilból
            LoadParametersFromProfile();

            // Gombok bekötése
            OkCommand = new RelayCommand(o => Close(true));
            CancelCommand = new RelayCommand(o => Close(false));
        }

        private void Close(bool result)
        {
            OnRequestClose?.Invoke(result);
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

            string side = "Bal";

            string GetParamName(string indicatorName, int paramIndex)
            {
                string n = indicatorName.ToUpper();
                if (n.Contains("BB") || n.Contains("BOLLINGER"))
                {
                    if (paramIndex == 2) return "Deviancia";
                }
                if (n.Contains("STOCH"))
                {
                    if (paramIndex == 1) return "%K Period";
                    if (paramIndex == 2) return "%D Period"; // vagy Signal
                    if (paramIndex == 3) return "Slowing";
                }
                if (n.Contains("MACD"))
                {
                    if (paramIndex == 1) return "Fast";
                    if (paramIndex == 2) return "Slow";
                    if (paramIndex == 3) return "Signal";
                }

                // Alapértelmezett
                return paramIndex == 1 ? "Period" : $"Param {paramIndex}";
            }
            // --- BAL OLDAL ---
            // 1. Fő Period
            if (rule.LeftPeriod > 0)
            {
                string name = GetParamName(rule.LeftIndicatorName, 1);
                AddParameterUI(rule, isEntry, "LeftPeriod", rule.LeftPeriod,
                    $"{prefix} - {rule.LeftIndicatorName} ({side}): {name}", isDecimal: false);
            }

            // 2. Második Paraméter (Stoch %D, MACD Slow, BB Dev)
            if (rule.LeftParameter2 > 0)
            {
                bool isBollinger = rule.LeftIndicatorName.ToUpper().Contains("BB");
                string name = GetParamName(rule.LeftIndicatorName, 2);

                AddParameterUI(rule, isEntry, "LeftParameter2", rule.LeftParameter2,
                    $"{prefix} - {rule.LeftIndicatorName} ({side}): {name}",
                    isDecimal: isBollinger); // Csak a Bollinger tizedes!
            }

            // 3. Harmadik Paraméter (Stoch Slowing, MACD Signal)
            if (rule.LeftParameter3 > 0)
            {
                string name = GetParamName(rule.LeftIndicatorName, 3);
                AddParameterUI(rule, isEntry, "LeftParameter3", rule.LeftParameter3,
                    $"{prefix} - {rule.LeftIndicatorName} ({side}): {name}", isDecimal: false);
            }

            // --- JOBB OLDAL (Ha indikátor) ---
            if (rule.RightSourceType == DataSourceType.Indicator)
            {
                side = "Jobb";

                if (rule.RightPeriod > 0)
                {
                    string name = GetParamName(rule.RightIndicatorName, 1);
                    AddParameterUI(rule, isEntry, "RightPeriod", rule.RightPeriod,
                        $"{prefix} - {rule.RightIndicatorName} ({side}): {name}", false);
                }

                if (rule.RightParameter2 > 0)
                {
                    bool isBollinger = rule.RightIndicatorName.ToUpper().Contains("BB");
                    string name = GetParamName(rule.RightIndicatorName, 2);
                    AddParameterUI(rule, isEntry, "RightParameter2", rule.RightParameter2,
                        $"{prefix} - {rule.RightIndicatorName} ({side}): {name}", isBollinger);
                }

                if (rule.RightParameter3 > 0)
                {
                    string name = GetParamName(rule.RightIndicatorName, 3);
                    AddParameterUI(rule, isEntry, "RightParameter3", rule.RightParameter3,
                        $"{prefix} - {rule.RightIndicatorName} ({side}): {name}", false);
                }
            }

            if (rule.RightSourceType == DataSourceType.Value)
            {
                AddParameterUI(rule, isEntry, "RightValue", rule.RightValue,
                    $"{prefix} - {rule.LeftIndicatorName} vs Fix Érték", isDecimal: true);
            }
        }

        private void AddParameterUI(StrategyRule rule, bool isEntry, string paramName, double currentValue, string displayName, bool isDecimal)
        {
            double min, max, step;

            if (isDecimal)
            {
                // TIZEDES LOGIKA (pl. Bollinger Deviancia: 2.0 -> 1.0 - 3.0, lépés 0.1)
                // Vagy ha az érték nagyon kicsi (0.001), akkor finomabb lépés kell

                step = 0.1;
                if (currentValue < 1.0 && currentValue > 0) step = 0.01; // Finomhangolás kis számokhoz

                min = Math.Max(0.1, Math.Round(currentValue * 0.5, 1));
                max = Math.Round(currentValue * 1.5, 1);

                if (max <= min) max = min + 2.0;
            }
            else
            {
                // EGÉSZ SZÁM LOGIKA (pl. Period: 14 -> 7 - 21, lépés 1)
                step = 1;
                min = Math.Max(1, Math.Floor(currentValue * 0.5));
                max = Math.Ceiling(currentValue * 1.5);

                if (max <= min) max = min + 10;
            }

            AvailableParameters.Add(new OptimizationParameterUI
            {
                Name = displayName,
                Rule = rule,
                ParameterName = paramName,
                IsEntrySide = isEntry,
                IsSelected = false,

                CurrentValue = currentValue,

                // FONTOS: Az OptimizationParameterUI-ban a Min/Max/Step típusának double-nek kell lennie!
                MinValue = min,
                MaxValue = max,
                Step = step
            });
        }

        // --- INotifyPropertyChanged Implementáció ---
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}