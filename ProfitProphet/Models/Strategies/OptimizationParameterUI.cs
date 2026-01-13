using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ProfitProphet.Models.Strategies
{
    public class OptimizationParameterUI : INotifyPropertyChanged
    {
        private bool _isSelected;
        private int _minValue = 10;
        private int _maxValue = 100;

        public StrategyRule Rule { get; set; }
        public bool IsLeftSide { get; set; } // Periódust vagy Értéket tekerünk?

        public string Name => $"{(IsLeftSide ? Rule.LeftIndicatorName : Rule.RightIndicatorName)} ({(IsLeftSide ? "Bal" : "Jobb")})";

        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        public int MinValue
        {
            get => _minValue;
            set { _minValue = value; OnPropertyChanged(); }
        }

        public int MaxValue
        {
            get => _maxValue;
            set { _maxValue = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
