using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ProfitProphet.Models.Strategies
{
    public class OptimizationParameterUI : INotifyPropertyChanged
    {
        private bool _isSelected;
        private double _minValue;
        private double _maxValue;
        private double _currentValue;

        // Adatok
        public StrategyRule Rule { get; set; }
        public string ParameterName { get; set; } // Pl: "LeftPeriod"
        public bool IsEntrySide { get; set; }

        // Megjelenítés
        public string Name { get; set; }

        public double CurrentValue
        {
            get => _currentValue;
            set { _currentValue = value; OnPropertyChanged(); }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        public double MinValue
        {
            get => _minValue;
            set { _minValue = value; OnPropertyChanged(); }
        }

        public double MaxValue
        {
            get => _maxValue;
            set { _maxValue = value; OnPropertyChanged(); }
        }

        private double _step = 1; // Alapértelmezés: 1
        public double Step
        {
            get => _step;
            set { _step = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}