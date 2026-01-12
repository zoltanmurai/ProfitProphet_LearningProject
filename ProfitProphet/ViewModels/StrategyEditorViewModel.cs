using ProfitProphet.Models.Strategies;
using ProfitProphet.ViewModels.Commands;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;

namespace ProfitProphet.ViewModels
{
    public class StrategyEditorViewModel : INotifyPropertyChanged
    {
        // Ez a profil, amit éppen szerkesztünk
        public StrategyProfile Profile { get; private set; }

        // Listák a UI-nak (szabályok)
        public ObservableCollection<StrategyRule> EntryRules { get; set; }
        public ObservableCollection<StrategyRule> ExitRules { get; set; }

        // --- LEGÖRDÜLŐ MENÜK TARTALMA ---
        // Elérhető indikátorok (később jöhet a Registry-ből is)
        public ObservableCollection<string> AvailableIndicators { get; } = new ObservableCollection<string>
        {
            "CMF", "SMA", "EMA", "RSI", "Close", "Volume"
        };

        // Elérhető operátorok (Enum-ból konvertálva)
        public ObservableCollection<ComparisonOperator> AvailableOperators { get; }

        // Jobb oldal típusai
        public ObservableCollection<DataSourceType> AvailableSourceTypes { get; }

        // Parancsok
        public ICommand AddEntryRuleCommand { get; }
        public ICommand RemoveEntryRuleCommand { get; }
        public ICommand AddExitRuleCommand { get; }
        public ICommand RemoveExitRuleCommand { get; }
        public ICommand SaveCommand { get; }

        // Esemény a mentéskor (hogy bezárhassuk az ablakot)
        public event Action OnRequestClose;

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }

        public StrategyEditorViewModel(StrategyProfile profile = null)
        {
            // Ha nincs profil (új létrehozása), akkor csinálunk egy üreset
            Profile = profile ?? new StrategyProfile { Name = "Új Stratégia", Symbol = "MSFT" };

            // Betöltjük a meglévő szabályokat szerkeszthető listákba
            EntryRules = new ObservableCollection<StrategyRule>(Profile.EntryRules);
            ExitRules = new ObservableCollection<StrategyRule>(Profile.ExitRules);

            // Enum listák feltöltése
            AvailableOperators = new ObservableCollection<ComparisonOperator>(
                Enum.GetValues(typeof(ComparisonOperator)).Cast<ComparisonOperator>());

            AvailableSourceTypes = new ObservableCollection<DataSourceType>(
                Enum.GetValues(typeof(DataSourceType)).Cast<DataSourceType>());

            // Parancsok bekötése
            AddEntryRuleCommand = new RelayCommand(_ => AddRule(EntryRules));
            RemoveEntryRuleCommand = new RelayCommand(r => RemoveRule(EntryRules, r as StrategyRule));

            AddExitRuleCommand = new RelayCommand(_ => AddRule(ExitRules));
            RemoveExitRuleCommand = new RelayCommand(r => RemoveRule(ExitRules, r as StrategyRule));

            SaveCommand = new RelayCommand(_ => Save());
        }

        private void AddRule(ObservableCollection<StrategyRule> collection)
        {
            // Alapértelmezett új szabály: CMF(20) > 0
            collection.Add(new StrategyRule
            {
                LeftIndicatorName = "CMF",
                LeftPeriod = 20,
                Operator = ComparisonOperator.GreaterThan,
                RightSourceType = DataSourceType.Value,
                RightValue = 0
            });
        }

        private void RemoveRule(ObservableCollection<StrategyRule> collection, StrategyRule rule)
        {
            if (rule != null) collection.Remove(rule);
        }

        private void Save()
        {
            // Visszamásoljuk a listákat a Profilba
            Profile.EntryRules = EntryRules.ToList();
            Profile.ExitRules = ExitRules.ToList();

            // Jelezzük, hogy készen vagyunk (bezárás)
            OnRequestClose?.Invoke();
        }
    }
}