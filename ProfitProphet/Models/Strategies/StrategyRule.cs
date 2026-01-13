using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ProfitProphet.Models.Strategies
{
    public class StrategyRule : INotifyPropertyChanged
    {
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
                    UpdateAllowedRightIndicators(); // Frissítjük a jobb oldali listát!
                }
            }
        }

        private int _leftPeriod;
        public int LeftPeriod
        {
            get => _leftPeriod;
            set { _leftPeriod = value; OnPropertyChanged(); }
        }

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
                    // Szólunk a UI-nak, hogy változott a láthatóság!
                    OnPropertyChanged(nameof(IsRightSideIndicator));
                }
            }
        }

        // --- JOBB OLDAL ADATOK ---
        private string _rightIndicatorName;
        public string RightIndicatorName
        {
            get => _rightIndicatorName;
            set { _rightIndicatorName = value; OnPropertyChanged(); }
        }

        private int _rightPeriod;
        public int RightPeriod
        {
            get => _rightPeriod;
            set { _rightPeriod = value; OnPropertyChanged(); }
        }

        private double _rightValue;
        public double RightValue
        {
            get => _rightValue;
            set { _rightValue = value; OnPropertyChanged(); }
        }

        // --- OKOS LISTA (Context Aware) ---
        private ObservableCollection<string> _allowedRightIndicators;
        public ObservableCollection<string> AllowedRightIndicators
        {
            get => _allowedRightIndicators;
            set { _allowedRightIndicators = value; OnPropertyChanged(); }
        }

        // Ez vezérli a mezők eltüntetését/megjelenítését
        public bool IsRightSideIndicator => RightSourceType == DataSourceType.Indicator;

        public StrategyRule()
        {
            AllowedRightIndicators = new ObservableCollection<string>();
            // Alapértelmezett lista
            UpdateAllowedRightIndicators(); 
        }

        // --- AZ AGY: ITT DÖNTJÜK EL, MIT AJÁNLUNK FEL ---
        private void UpdateAllowedRightIndicators()
        {
            // Ha null a lista, létrehozzuk
            if (AllowedRightIndicators == null) AllowedRightIndicators = new ObservableCollection<string>();
            AllowedRightIndicators.Clear();

            switch (LeftIndicatorName)
            {
                case "CMF":
                    AllowedRightIndicators.Add("CMF_MA");
                    if (string.IsNullOrEmpty(RightIndicatorName)) RightIndicatorName = "CMF_MA";
                    break;

                case "RSI":
                    AllowedRightIndicators.Add("RSI_MA");
                    AllowedRightIndicators.Add("RSI"); // Önmagával is összevethető (pl. másik periódus)
                    break;

                case "Stoch":
                    AllowedRightIndicators.Add("Stoch_Signal");
                    break;

                case "Close": // Árfolyam
                case "Open":
                case "High":
                case "Low":
                    AllowedRightIndicators.Add("SMA");
                    AllowedRightIndicators.Add("EMA");
                    AllowedRightIndicators.Add("BollingerUpper");
                    AllowedRightIndicators.Add("BollingerLower");
                    if (string.IsNullOrEmpty(RightIndicatorName)) RightIndicatorName = "SMA";
                    break;

                default:
                    AllowedRightIndicators.Add("SMA");
                    AllowedRightIndicators.Add("EMA");
                    break;
            }
            OnPropertyChanged(nameof(AllowedRightIndicators));
        }

        public override string ToString()
        {
            string left = $"{LeftIndicatorName}({LeftPeriod})";
            string op = Operator.ToString(); // Egyszerűsítve
            string right = RightSourceType == DataSourceType.Indicator 
                ? $"{RightIndicatorName}({RightPeriod})" 
                : $"{RightValue}";
            return $"{left} {op} {right}";
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}