using Microsoft.Win32;
using ProfitProphet.Models.Strategies; // StrategyGroup, StrategyProfile
using ProfitProphet.Services.Indicators;
using ProfitProphet.ViewModels.Commands;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Input;

namespace ProfitProphet.ViewModels
{
    public class StrategyEditorViewModel : INotifyPropertyChanged
    {
        public StrategyProfile Profile { get; private set; }

        // LISTÁK A CSOPORTOKNAK
        public ObservableCollection<StrategyGroup> EntryGroups { get; set; }
        public ObservableCollection<StrategyGroup> ExitGroups { get; set; }

        private List<string> _availableIndicators;
        public List<string> AvailableIndicators
        {
            get => _availableIndicators;
            set { _availableIndicators = value; OnPropertyChanged(); }
        }

        public ObservableCollection<ComparisonOperator> AvailableOperators { get; }
        public ObservableCollection<DataSourceType> AvailableSourceTypes { get; }

        // --- PARANCSOK ---
        public ICommand AddEntryGroupCommand { get; }
        public ICommand AddExitGroupCommand { get; }
        public ICommand RemoveGroupCommand { get; }
        public ICommand AddRuleToGroupCommand { get; }
        public ICommand RemoveRuleCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand SaveGroupCommand { get; }
        public ICommand LoadEntryGroupCommand { get; }
        public ICommand LoadExitGroupCommand { get; }
        public event Action OnRequestClose;

        public StrategyEditorViewModel(StrategyProfile profile, IIndicatorRegistry registry)
        {
            Profile = profile ?? new StrategyProfile { Name = "Új Stratégia", Symbol = "MSFT" };
            PopulateIndicators(registry);

            SaveCommand = new RelayCommand(o => Save());
            CancelCommand = new RelayCommand(o => Cancel());

            EntryGroups = new ObservableCollection<StrategyGroup>(Profile.EntryGroups);
            ExitGroups = new ObservableCollection<StrategyGroup>(Profile.ExitGroups);

            AvailableOperators = new ObservableCollection<ComparisonOperator>(Enum.GetValues(typeof(ComparisonOperator)).Cast<ComparisonOperator>());
            AvailableSourceTypes = new ObservableCollection<DataSourceType>(Enum.GetValues(typeof(DataSourceType)).Cast<DataSourceType>());

            // --- Parancsok bekötése ---
            AddEntryGroupCommand = new RelayCommand(_ => EntryGroups.Add(new StrategyGroup { Name = "Új Vételi Setup" }));
            AddExitGroupCommand = new RelayCommand(_ => ExitGroups.Add(new StrategyGroup { Name = "Új Eladási Setup" }));

            SaveGroupCommand = new RelayCommand(param => SaveGroup(param as StrategyGroup));

            LoadEntryGroupCommand = new RelayCommand(_ => LoadGroup(true));
            LoadExitGroupCommand = new RelayCommand(_ => LoadGroup(false));

            RemoveGroupCommand = new RelayCommand(param =>
            {
                if (param is StrategyGroup group)
                {
                    if (EntryGroups.Remove(group)) return;
                    ExitGroups.Remove(group);
                }
            });

            AddRuleToGroupCommand = new RelayCommand(param =>
            {
                if (param is StrategyGroup group)
                {
                    var newRule = new StrategyRule
                    {
                        LeftIndicatorName = "CMF",
                        LeftPeriod = 20,
                        Operator = ComparisonOperator.GreaterThan,
                        RightValue = 0
                    };

                    // FONTOS: Az új szabályra is feliratkozunk!
                    newRule.PropertyChanged += OnRulePropertyChanged;
                    group.Rules.Add(newRule);
                }
            });

            RemoveRuleCommand = new RelayCommand(param =>
            {
                if (param is StrategyRule rule)
                {
                    rule.PropertyChanged -= OnRulePropertyChanged; // Leiratkozás törlés előtt
                    foreach (var group in EntryGroups) if (group.Rules.Remove(rule)) return;
                    foreach (var group in ExitGroups) if (group.Rules.Remove(rule)) return;
                }
            });

            //SaveCommand = new RelayCommand(_ => Save());
            // Feliratkozás a meglévő szabályokra
            SubscribeToRuleChanges();
        }

        private void SaveGroup(StrategyGroup group)
        {
            if (group == null) return;

            var dlg = new SaveFileDialog
            {
                FileName = group.Name.Replace(" ", "_") + ".json",
                DefaultExt = ".json",
                Filter = "Strategy Setup (.json)|*.json",
                Title = "Setup (Kártya) Mentése"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    string jsonString = JsonSerializer.Serialize(group, options);
                    File.WriteAllText(dlg.FileName, jsonString);
                    MessageBox.Show("Sikeres mentés!", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Hiba a mentés során: {ex.Message}", "Hiba", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void LoadGroup(bool isEntry)
        {
            var dlg = new OpenFileDialog
            {
                DefaultExt = ".json",
                Filter = "Strategy Setup (.json)|*.json",
                Title = isEntry ? "Vételi Setup Betöltése" : "Eladási Setup Betöltése"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    string jsonString = File.ReadAllText(dlg.FileName);
                    var loadedGroup = JsonSerializer.Deserialize<StrategyGroup>(jsonString);

                    if (loadedGroup != null)
                    {
                        // Betöltés után fel kell iratkozni az új szabályokra is!
                        foreach (var rule in loadedGroup.Rules)
                        {
                            rule.PropertyChanged += OnRulePropertyChanged;
                        }

                        if (isEntry) EntryGroups.Add(loadedGroup);
                        else ExitGroups.Add(loadedGroup);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Hiba a betöltés során: {ex.Message}\nEllenőrizd, hogy megfelelő fájlt választottál-e!", "Hiba", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void PopulateIndicators(IIndicatorRegistry registry)
        {
            var list = new List<string>();
            list.AddRange(new[] { "Close", "Open", "High", "Low", "Volume" });

            if (registry != null)
            {
                var registeredIndicators = registry.GetAll();
                foreach (var indicator in registeredIndicators)
                {
                    string id = indicator.Id.ToUpper();

                    if (id.Contains("MACD"))
                    {
                        list.Add("MACD_MAIN");
                        list.Add("MACD_SIGNAL");
                        list.Add("MACD_HIST");
                    }
                    else if (id.Contains("BB") || id.Contains("BOLLINGER"))
                    {
                        list.Add("BB_UPPER");
                        list.Add("BB_LOWER");
                        list.Add("BB_MIDDLE");
                    }
                    else if (id.Contains("STOCH"))
                    {
                        list.Add("STOCH");
                        list.Add("STOCH_SIGNAL");
                    }
                    else
                    {
                        list.Add(id);
                    }
                }
            }
            else
            {
                list.AddRange(new[] {
                    "SMA", "EMA", "RSI", "CMF", "CCI", "ATR", "MOMENTUM",
                    "STOCH", "STOCH_SIGNAL",
                    "MACD_MAIN", "MACD_SIGNAL", "MACD_HIST",
                    "BB_UPPER", "BB_LOWER", "BB_MIDDLE"
                });
            }
            AvailableIndicators = list.Distinct().OrderBy(x => x).ToList();
        }

        // ==========================================================
        // SZINKRONIZÁCIÓS LOGIKA (LOCK TO PREVIOUS)
        // ==========================================================

        private void SubscribeToRuleChanges()
        {
            foreach (var group in EntryGroups)
            {
                foreach (var rule in group.Rules)
                {
                    rule.PropertyChanged -= OnRulePropertyChanged;
                    rule.PropertyChanged += OnRulePropertyChanged;
                }
            }

            foreach (var group in ExitGroups)
            {
                foreach (var rule in group.Rules)
                {
                    rule.PropertyChanged -= OnRulePropertyChanged;
                    rule.PropertyChanged += OnRulePropertyChanged;
                }
            }
        }

        private void OnRulePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var changedRule = sender as StrategyRule;
            if (changedRule == null) return;

            if (e.PropertyName == nameof(StrategyRule.IsLeftLinked) ||
                e.PropertyName == nameof(StrategyRule.IsRightLinked) ||
                e.PropertyName == nameof(StrategyRule.LeftPeriod) ||
                e.PropertyName == nameof(StrategyRule.LeftParameter2) ||
                e.PropertyName == nameof(StrategyRule.LeftParameter3) ||
                e.PropertyName == nameof(StrategyRule.RightPeriod) ||
                e.PropertyName == nameof(StrategyRule.RightParameter2) ||
                e.PropertyName == nameof(StrategyRule.RightParameter3))
            {
                var group = FindGroupOfRule(changedRule);
                if (group != null)
                {
                    SyncGroupRules(group);
                }
            }
        }

        private StrategyGroup FindGroupOfRule(StrategyRule rule)
        {
            foreach (var g in EntryGroups) if (g.Rules.Contains(rule)) return g;
            foreach (var g in ExitGroups) if (g.Rules.Contains(rule)) return g;
            return null;
        }

        private void SyncGroupRules(StrategyGroup group)
        {
            if (group == null || group.Rules.Count < 2) return;

            // A 2. elemtől indulunk (index 1)
            for (int i = 1; i < group.Rules.Count; i++)
            {
                var currentRule = group.Rules[i];
                var prevRule = group.Rules[i - 1];

                // 1. BAL OLDAL
                if (currentRule.IsLeftLinked)
                {
                    if (currentRule.LeftPeriod != prevRule.LeftPeriod) currentRule.LeftPeriod = prevRule.LeftPeriod;
                    if (currentRule.LeftParameter2 != prevRule.LeftParameter2) currentRule.LeftParameter2 = prevRule.LeftParameter2;
                    if (currentRule.LeftParameter3 != prevRule.LeftParameter3) currentRule.LeftParameter3 = prevRule.LeftParameter3;
                }

                // 2. JOBB OLDAL
                if (currentRule.IsRightLinked)
                {
                    if (prevRule.RightSourceType == DataSourceType.Indicator && currentRule.RightSourceType == DataSourceType.Indicator)
                    {
                        if (currentRule.RightPeriod != prevRule.RightPeriod) currentRule.RightPeriod = prevRule.RightPeriod;
                        if (currentRule.RightParameter2 != prevRule.RightParameter2) currentRule.RightParameter2 = prevRule.RightParameter2;
                        if (currentRule.RightParameter3 != prevRule.RightParameter3) currentRule.RightParameter3 = prevRule.RightParameter3;
                    }
                }
            }
        }

        private void Save()
        {
            Profile.EntryGroups = EntryGroups.ToList();
            Profile.ExitGroups = ExitGroups.ToList();
            OnRequestClose?.Invoke();
        }
        private void Cancel()
        {
            OnRequestClose?.Invoke();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}