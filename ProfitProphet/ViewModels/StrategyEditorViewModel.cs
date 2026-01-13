using ProfitProphet.Models.Strategies; // StrategyGroup, StrategyProfile
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
        public StrategyProfile Profile { get; private set; }

        // LISTÁK A CSOPORTOKNAK
        public ObservableCollection<StrategyGroup> EntryGroups { get; set; }
        public ObservableCollection<StrategyGroup> ExitGroups { get; set; }

        // --- LEGÖRDÜLŐ MENÜK (Adatforrás a UI-nak) ---
        public ObservableCollection<string> AvailableIndicators { get; } = new ObservableCollection<string>
        { "CMF", "SMA", "EMA", "RSI", "Stoch", "Close", "Open", "High", "Low", "Volume" };

        public ObservableCollection<ComparisonOperator> AvailableOperators { get; }
        public ObservableCollection<DataSourceType> AvailableSourceTypes { get; }

        // --- PARANCSOK ---
        // Csoport műveletek
        public ICommand AddEntryGroupCommand { get; }
        //public ICommand RemoveEntryGroupCommand { get; }
        public ICommand AddExitGroupCommand { get; }
        //public ICommand RemoveExitGroupCommand { get; }
        public ICommand RemoveGroupCommand { get; }

        // Szabály műveletek (Csoporton belül)
        public ICommand AddRuleToGroupCommand { get; }
        public ICommand RemoveRuleCommand { get; } // Ez trükkös lesz

        public ICommand SaveCommand { get; }
        public event Action OnRequestClose;

        public StrategyEditorViewModel(StrategyProfile profile = null)
        {
            Profile = profile ?? new StrategyProfile { Name = "Új Stratégia", Symbol = "MSFT" };

            // Csoportok betöltése
            EntryGroups = new ObservableCollection<StrategyGroup>(Profile.EntryGroups);
            ExitGroups = new ObservableCollection<StrategyGroup>(Profile.ExitGroups);

            // Enumok betöltése
            AvailableOperators = new ObservableCollection<ComparisonOperator>(Enum.GetValues(typeof(ComparisonOperator)).Cast<ComparisonOperator>());
            AvailableSourceTypes = new ObservableCollection<DataSourceType>(Enum.GetValues(typeof(DataSourceType)).Cast<DataSourceType>());

            // --- Parancsok bekötése ---

            // 1. Csoport hozzáadása (Üres csoport létrehozása)
            AddEntryGroupCommand = new RelayCommand(_ => EntryGroups.Add(new StrategyGroup { Name = "Új Vételi Setup" }));
            AddExitGroupCommand = new RelayCommand(_ => ExitGroups.Add(new StrategyGroup { Name = "Új Eladási Setup" }));

            // 2. Csoport törlése
            //RemoveEntryGroupCommand = new RelayCommand(g => EntryGroups.Remove(g as StrategyGroup));
            //RemoveExitGroupCommand = new RelayCommand(g => ExitGroups.Remove(g as StrategyGroup));
            //RemoveEntryGroupCommand = new RelayCommand(param =>
            //{
            //    if (param is StrategyGroup group)
            //    {
            //        // Ha a vételi listában van, onnan töröljük
            //        if (EntryGroups.Contains(group))
            //        {
            //            EntryGroups.Remove(group);
            //            return;
            //        }
            //        // Ha az eladási listában van, onnan töröljük
            //        if (ExitGroups.Contains(group))
            //        {
            //            ExitGroups.Remove(group);
            //            return;
            //        }
            //    }
            //});

            RemoveGroupCommand = new RelayCommand(param =>
            {
                if (param is StrategyGroup group)
                {
                    if (EntryGroups.Remove(group)) return; // Ha sikerült törölni a vételiből, kész.
                    ExitGroups.Remove(group); // Ha nem, megpróbáljuk az eladásiból.
                }
            });

            // 3. Szabály hozzáadása (Paraméter: A CSOPORT, amihez adjuk)
            AddRuleToGroupCommand = new RelayCommand(param =>
            {
                if (param is StrategyGroup group)
                {
                    group.Rules.Add(new StrategyRule
                    {
                        LeftIndicatorName = "CMF",
                        LeftPeriod = 20,
                        Operator = ComparisonOperator.GreaterThan,
                        RightValue = 0
                    });
                }
            });

            // 4. Szabály törlése (Megkeressük, melyik csoportban van, és kivesszük)
            RemoveRuleCommand = new RelayCommand(param =>
            {
                if (param is StrategyRule rule)
                {
                    // Végignézzük az összes csoportot mindkét oldalon
                    foreach (var group in EntryGroups) if (group.Rules.Remove(rule)) return;
                    foreach (var group in ExitGroups) if (group.Rules.Remove(rule)) return;
                }
            });

            SaveCommand = new RelayCommand(_ => Save());
        }

        private void Save()
        {
            Profile.EntryGroups = EntryGroups.ToList();
            Profile.ExitGroups = ExitGroups.ToList();
            OnRequestClose?.Invoke();
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }
    }
}