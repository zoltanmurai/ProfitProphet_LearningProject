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

        // --- LEGÖRDÜLŐ MENÜK (Adatforrás a UI-nak) ---
        //public ObservableCollection<string> AvailableIndicators { get; } = new ObservableCollection<string>
        //{ "CMF", "SMA", "EMA", "RSI", "Stoch", "Close", "Open", "High", "Low", "Volume" };
        private List<string> _availableIndicators;
        public List<string> AvailableIndicators
        {
            get => _availableIndicators;
            set { _availableIndicators = value; OnPropertyChanged(); }
        }

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

            SaveGroupCommand = new RelayCommand(param => SaveGroup(param as StrategyGroup));

            LoadEntryGroupCommand = new RelayCommand(_ => LoadGroup(true));  // True = Entry
            LoadExitGroupCommand = new RelayCommand(_ => LoadGroup(false));

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

        private void SaveGroup(StrategyGroup group)
        {
            if (group == null) return;

            // Fájl mentése ablak
            var dlg = new SaveFileDialog
            {
                FileName = group.Name.Replace(" ", "_") + ".json", // Alapértelmezett név
                DefaultExt = ".json",
                Filter = "Strategy Setup (.json)|*.json",
                Title = "Setup (Kártya) Mentése"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    // JSON szerializálás (formázva, hogy olvasható legyen)
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

        // --- BETÖLTÉS LOGIKA ---
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
                        // Hozzáadjuk a megfelelő listához
                        if (isEntry)
                        {
                            EntryGroups.Add(loadedGroup);
                        }
                        else
                        {
                            ExitGroups.Add(loadedGroup);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Hiba a betöltés során: {ex.Message}\nEllenőrizd, hogy megfelelő fájlt választottál-e!", "Hiba", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        //private void PopulateIndicators(IIndicatorRegistry registry)
        //{
        //    var list = new List<string>();

        //    // A) Alapvető árfolyam adatok (Ezek mindig kellenek)
        //    list.AddRange(new[] { "Close", "Open", "High", "Low", "Volume" });

        //    // B) Lekérjük a Registry-ből az összes indikátort
        //    if (registry != null)
        //    {
        //        var registeredIndicators = registry.GetAll();

        //        foreach (var indicator in registeredIndicators)
        //        {
        //            // Itt egy kis trükk kell: 
        //            // A BacktestService switch-case alapján tudnunk kell, 
        //            // hogy melyik indikátornak vannak "al-vonalai".

        //            string id = indicator.Id.ToUpper(); // pl. "MACD"

        //            if (id == "MACD")
        //            {
        //                // A MACD-nek több kimenete van, ezeket külön kell felvenni,
        //                // hogy a stratégiában ki tudd választani pl: MACD_Main > MACD_Signal
        //                list.Add("MACD_MAIN");
        //                list.Add("MACD_SIGNAL");
        //                list.Add("MACD_HIST");
        //            }
        //            else if (id == "BB" || id == "BOLLINGER")
        //            {
        //                list.Add("BB_UPPER");  // Felső szalag
        //                list.Add("BB_LOWER");  // Alsó szalag
        //                list.Add("BB_MIDDLE"); // Középső szalag (ami valójában az SMA)
        //            }
        //            else
        //            {
        //                // Egyszerű indikátorok (RSI, SMA, EMA, CMF...)
        //                // Csak simán a nevüket (ID) adjuk hozzá
        //                list.Add(indicator.Id.ToUpper()); // pl. "RSI", "SMA"
        //            }
        //        }
        //    }
        //    else
        //    {
        //        // Fallback, ha nincs registry (teszteléshez)
        //        list.AddRange(new[] { "SMA", "EMA", "RSI", "CMF", "STOCH", "STOCH_SIGNAL", "MACD_MAIN", "MACD_SIGNAL" });
        //    }

        //    AvailableIndicators = list;
        //}

        private void PopulateIndicators(IIndicatorRegistry registry)
        {
            var list = new List<string>();

            // A) Alapvető árfolyam adatok
            list.AddRange(new[] { "Close", "Open", "High", "Low", "Volume" });

            // B) Indikátorok betöltése
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
                    // JAVÍTVA: Jobb felismerés a Bollingerhez
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

            // Ismétlődések kiszűrése és rendezés
            AvailableIndicators = list.Distinct().OrderBy(x => x).ToList();
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

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }
    }
}