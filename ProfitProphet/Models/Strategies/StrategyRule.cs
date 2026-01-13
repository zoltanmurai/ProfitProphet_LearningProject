using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

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
                    // HA VÁLTOZIK A BAL OLDAL, VÁLTOZZON A JOBB OLDAL!
                    UpdateAllowedRightIndicators();
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

        private ObservableCollection<string> _allowedRightIndicators;
        public ObservableCollection<string> AllowedRightIndicators
        {
            get => _allowedRightIndicators;
            set { _allowedRightIndicators = value; OnPropertyChanged(); }
        }

        public StrategyRule()
        {
            AllowedRightIndicators = new ObservableCollection<string>();
        }

        private void UpdateAllowedRightIndicators()
        {
            AllowedRightIndicators.Clear();

            switch (LeftIndicatorName)
            {
                case "CMF":
                    // CMF-hez csak a saját mozgóátlaga illik
                    AllowedRightIndicators.Add("CMF_MA");
                    // Esetleg beállíthatjuk alapértelmezettnek is rögtön:
                    RightIndicatorName = "CMF_MA";
                    break;

                case "RSI":
                    // RSI-hez illik az RSI mozgóátlaga (ha van), vagy más oszcillátorok
                    AllowedRightIndicators.Add("RSI_MA");
                    break;

                case "Stoch":
                    AllowedRightIndicators.Add("Stoch_Signal");
                    RightIndicatorName = "Stoch_Signal";
                    break;

                case "Close": // Árfolyam
                case "Open":
                case "High":
                case "Low":
                    // Árhoz bármilyen mozgóátlag illik
                    AllowedRightIndicators.Add("SMA");
                    AllowedRightIndicators.Add("EMA");
                    AllowedRightIndicators.Add("BollingerUpper");
                    AllowedRightIndicators.Add("BollingerLower");
                    // Alapértelmezés
                    if (string.IsNullOrEmpty(RightIndicatorName) || !AllowedRightIndicators.Contains(RightIndicatorName))
                        RightIndicatorName = "SMA";
                    break;

                default:
                    // Ha nem ismerjük, adunk egy általános listát
                    AllowedRightIndicators.Add("SMA");
                    AllowedRightIndicators.Add("EMA");
                    break;
            }

            // Értesítjük a felületet, hogy változott a jobb oldali név is (ha automatikusan átírtuk)
            OnPropertyChanged(nameof(RightIndicatorName));
        }

        // segédproperty
        public bool IsRightSideIndicator => RightSourceType == DataSourceType.Indicator;

        public override string ToString()
        {
            string left = $"{LeftIndicatorName}({LeftPeriod})";
            string op = Operator switch
            {
                ComparisonOperator.GreaterThan => ">",
                ComparisonOperator.LessThan => "<",
                ComparisonOperator.Equals => "=",
                ComparisonOperator.CrossesAbove => "Keresztezi Felfelé",
                ComparisonOperator.CrossesBelow => "Keresztezi Lefelé",
                _ => "?"
            };
            string right = RightSourceType == DataSourceType.Indicator
                ? $"{RightIndicatorName}({RightPeriod})"
                : $"{RightValue}";

            return $"{left} {op} {right}";
        }

        // --- INotifyPropertyChanged Implementáció ---
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
