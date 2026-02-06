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
            // Alapértelmezett tartományok (pl. +/- 50%)
            int defaultMin = (int)Math.Max(1, Math.Floor(currentValue * 0.5));
            int defaultMax = (int)Math.Ceiling(currentValue * 1.5);

            if (defaultMax <= defaultMin) defaultMax = defaultMin + 5;

            AvailableParameters.Add(new OptimizationParameterUI
            {
                Name = displayName,
                Rule = rule,
                ParameterName = paramName,
                IsEntrySide = isEntry,
                IsSelected = false, // Alapból nincs kijelölve
                CurrentValue = currentValue,
                MinValue = defaultMin,
                MaxValue = defaultMax
            });
        }

        // --- INotifyPropertyChanged Implementáció ---
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}