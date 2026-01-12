using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ProfitProphet.Models.Strategies
{
    // Mostantól ez is értesíti a felületet a változásokról
    public class StrategyRule : INotifyPropertyChanged
    {
        private string _leftIndicatorName;
        public string LeftIndicatorName
        {
            get => _leftIndicatorName;
            set { _leftIndicatorName = value; OnPropertyChanged(); }
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

                    // EZ A KULCS: Ha a típus változik, szólunk, 
                    // hogy a "IsRightSideIndicator" tulajdonság is megváltozott!
                    // Így a felület tudni fogja, hogy cserélni kell a mezőket.
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

        // Ez a segédproperty vezérli a láthatóságot
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
