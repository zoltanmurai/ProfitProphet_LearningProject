using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization; // Ez kell a [JsonIgnore]-hoz!

namespace ProfitProphet.Models.Strategies
{
    public class StrategyRule : INotifyPropertyChanged
    {
        private void SetDefaults(bool isLeft)
        {
            string name = isLeft ? LeftIndicatorName : RightIndicatorName;
            if (string.IsNullOrEmpty(name)) return;

            string n = name.ToUpper();

            // Segédfüggvény a beállításhoz
            void Set(int p1, int p2, int p3)
            {
                if (isLeft)
                {
                    if (LeftPeriod == 0) LeftPeriod = p1; // Csak akkor írjuk felül, ha 0
                    if (LeftParameter2 == 0) LeftParameter2 = p2;
                    if (LeftParameter3 == 0) LeftParameter3 = p3;
                }
                else
                {
                    if (RightPeriod == 0) RightPeriod = p1;
                    if (RightParameter2 == 0) RightParameter2 = p2;
                    if (RightParameter3 == 0) RightParameter3 = p3;
                }
            }

            if (n.Contains("MACD")) Set(12, 26, 9);
            else if (n.Contains("STOCH")) Set(14, 3, 3);
            else if (n.Contains("BB") || n.Contains("BOLLINGER")) Set(20, 2, 0); // Bollinger: 20, 2
            else if (n.Contains("RSI")) Set(14, 0, 0);
            else if (n.Contains("CMF")) Set(20, 0, 0);
            else if (n.Contains("EMA") || n.Contains("SMA")) Set(14, 0, 0);
        }
        // --- BAL OLDAL ---
        private string _leftIndicatorName;
        public string LeftIndicatorName
        {
            get => _leftIndicatorName;
            set
            {
                if (_leftIndicatorName != value)
                {
                    _leftIndicatorName = value;
                    OnPropertyChanged();
                    UpdateAllowedRightIndicators();
                    SetDefaults(true);

                    // UI Frissítések a paraméter mezőkhöz
                    OnPropertyChanged(nameof(ShowLeftParam2));
                    OnPropertyChanged(nameof(ShowLeftParam3));
                }
            }
        }

        // 1. Paraméter (Period) - Ez mindig volt
        private int _leftPeriod;
        public int LeftPeriod
        {
            get => _leftPeriod;
            set { _leftPeriod = value; OnPropertyChanged(); }
        }

        // ÚJ: 2. Paraméter (pl. MACD Slow / Stoch D)
        private int _leftParameter2;
        public int LeftParameter2
        {
            get => _leftParameter2;
            set { _leftParameter2 = value; OnPropertyChanged(); }
        }

        // ÚJ: 3. Paraméter (pl. MACD Signal / Stoch Slowing)
        private int _leftParameter3;
        public int LeftParameter3
        {
            get => _leftParameter3;
            set { _leftParameter3 = value; OnPropertyChanged(); }
        }

        // --- OPERÁTOR ---
        private ComparisonOperator _operator;
        public ComparisonOperator Operator
        {
            get => _operator;
            set { _operator = value; OnPropertyChanged(); }
        }

        // --- JOBB OLDAL TÍPUS ---
        private DataSourceType _rightSourceType = DataSourceType.Value;
        public DataSourceType RightSourceType
        {
            get => _rightSourceType;
            set
            {
                if (_rightSourceType != value)
                {
                    _rightSourceType = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsRightSideIndicator));
                }
            }
        }

        // --- JOBB OLDAL ADATOK ---
        private string _rightIndicatorName;
        public string RightIndicatorName
        {
            get => _rightIndicatorName;
            set
            {
                if (_rightIndicatorName != value)
                {
                    _rightIndicatorName = value;
                    OnPropertyChanged();
                    SetDefaults(false);

                    OnPropertyChanged(nameof(ShowRightParam2));
                    OnPropertyChanged(nameof(ShowRightParam3));
                }
            }
        }

        private int _rightPeriod;
        public int RightPeriod
        {
            get => _rightPeriod;
            set { _rightPeriod = value; OnPropertyChanged(); }
        }

        // ÚJ: Jobb oldal 2. paraméter
        private int _rightParameter2;
        public int RightParameter2
        {
            get => _rightParameter2;
            set { _rightParameter2 = value; OnPropertyChanged(); }
        }

        // ÚJ: Jobb oldal 3. paraméter
        private int _rightParameter3;
        public int RightParameter3
        {
            get => _rightParameter3;
            set { _rightParameter3 = value; OnPropertyChanged(); }
        }

        private double _rightValue;
        public double RightValue
        {
            get => _rightValue;
            set { _rightValue = value; OnPropertyChanged(); }
        }

        private int _shift = 0;
        public int Shift
        {
            get => _shift;
            set { _shift = value; OnPropertyChanged(); }
        }

        // --- OKOS LISTA (Context Aware) ---
        private ObservableCollection<string> _allowedRightIndicators;
        public ObservableCollection<string> AllowedRightIndicators
        {
            get => _allowedRightIndicators;
            set { _allowedRightIndicators = value; OnPropertyChanged(); }
        }

        public bool IsRightSideIndicator => RightSourceType == DataSourceType.Indicator;

        // --- UI LÁTHATÓSÁGI LOGIKA (BAL OLDAL) ---
        [JsonIgnore]
        public bool ShowLeftParam2 => IsMultiParam(LeftIndicatorName);

        [JsonIgnore]
        public bool ShowLeftParam3 => IsThreeParam(LeftIndicatorName);

        // --- UI LÁTHATÓSÁGI LOGIKA (JOBB OLDAL) ---
        [JsonIgnore]
        public bool ShowRightParam2 => IsMultiParam(RightIndicatorName) && IsRightSideIndicator;

        [JsonIgnore]
        public bool ShowRightParam3 => IsThreeParam(RightIndicatorName) && IsRightSideIndicator;

        // Segédfüggvények a láthatósághoz
        private bool IsMultiParam(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string n = name.ToUpper();
            return n.Contains("MACD") || n.Contains("STOCH") || n.Contains("BB") || n.Contains("BOLLINGER");
        }

        private bool IsThreeParam(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string n = name.ToUpper();
            return n.Contains("MACD") || n.Contains("STOCH");
        }

        public StrategyRule()
        {
            AllowedRightIndicators = new ObservableCollection<string>();
            UpdateAllowedRightIndicators();
        }

        private void UpdateAllowedRightIndicators()
        {
            if (AllowedRightIndicators == null) AllowedRightIndicators = new ObservableCollection<string>();
            AllowedRightIndicators.Clear();

            //switch (LeftIndicatorName)
            //{
            //    case "CMF":
            //        AllowedRightIndicators.Add("CMF_MA");
            //        if (string.IsNullOrEmpty(RightIndicatorName)) RightIndicatorName = "CMF_MA";
            //        break;

            //    case "RSI":
            //        AllowedRightIndicators.Add("RSI_MA");
            //        AllowedRightIndicators.Add("RSI"); // Önmagával is összevethető (pl. másik periódus)
            //        break;

            //    case "STOCH":
            //        AllowedRightIndicators.Add("Stoch_Signal");
            //        break;
            //    case "MACD":
            //        AllowedRightIndicators.Add("MACD_SIGNAL");
            //        AllowedRightIndicators.Add("MACD_MAIN");
            //        break;
            //    case "Close": // Árfolyam
            //    case "Open":
            //    case "High":
            //    case "Low":
            //        AllowedRightIndicators.Add("SMA");
            //        AllowedRightIndicators.Add("EMA");
            //        //AllowedRightIndicators.Add("BollingerUpper");
            //        //AllowedRightIndicators.Add("BollingerLower");
            //        AllowedRightIndicators.Add("BB_UPPER");
            //        AllowedRightIndicators.Add("BB_LOWER");
            //        AllowedRightIndicators.Add("BB_MIDDLE");
            //        if (string.IsNullOrEmpty(RightIndicatorName)) RightIndicatorName = "SMA";
            //        break;

            //    default:
            //        AllowedRightIndicators.Add("SMA");
            //        AllowedRightIndicators.Add("EMA");
            //        break;
            //}
            // Null ellenőrzés
            if (string.IsNullOrEmpty(LeftIndicatorName)) return;

            string left = LeftIndicatorName.ToUpper();

            // 1. CMF
            if (left.Contains("CMF"))
            {
                AllowedRightIndicators.Add("CMF_MA");
                //AllowedRightIndicators.Add("Value"); // Ha 0-hoz akarod nézni
                if (string.IsNullOrEmpty(RightIndicatorName)) RightIndicatorName = "CMF_MA";
            }
            // 2. RSI
            else if (left.Contains("RSI"))
            {
                AllowedRightIndicators.Add("RSI_MA"); // Ha van ilyen
                //AllowedRightIndicators.Add("SMA");    // Ha mozgóátlagot akarsz rá
                AllowedRightIndicators.Add("RSI");    // RSI vs RSI
                //AllowedRightIndicators.Add("Value");  // 30 / 70 szintekhez
            }
            // 3. STOCHASTIC
            else if (left.Contains("STOCH"))
            {
                AllowedRightIndicators.Add("STOCH_SIGNAL");
                //AllowedRightIndicators.Add("Value"); // 20 / 80 szintekhez
            }
            // 4. MACD (Ez volt a kritikus pont!)
            // A .Contains("MACD") elkapja a "MACD", "MACD_MAIN", "MACD_SIGNAL" neveket is
            else if (left.Contains("MACD"))
            {
                AllowedRightIndicators.Add("MACD_SIGNAL");
                AllowedRightIndicators.Add("MACD_MAIN");
                AllowedRightIndicators.Add("MACD_HIST");
                //AllowedRightIndicators.Add("Value"); // 0 szinthez
            }
            // 5. ÁRFOLYAM (Price)
            else if (left == "CLOSE" || left == "OPEN" || left == "HIGH" || left == "LOW")
            {
                AllowedRightIndicators.Add("SMA");
                AllowedRightIndicators.Add("EMA");
                AllowedRightIndicators.Add("BB_UPPER");
                AllowedRightIndicators.Add("BB_LOWER");
                AllowedRightIndicators.Add("BB_MIDDLE");

                if (string.IsNullOrEmpty(RightIndicatorName)) RightIndicatorName = "SMA";
            }
            // 6. EGYÉB (Fallback)
            else
            {
                AllowedRightIndicators.Add("SMA");
                AllowedRightIndicators.Add("EMA");
                //AllowedRightIndicators.Add("Value");
            }

            OnPropertyChanged(nameof(AllowedRightIndicators));
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}