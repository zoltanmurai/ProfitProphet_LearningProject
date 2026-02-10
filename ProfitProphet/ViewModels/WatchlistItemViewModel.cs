using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ProfitProphet.ViewModels
{
    public class WatchlistItemViewModel : INotifyPropertyChanged
    {
        private string _symbol;
        private bool _hasSignal;
        private string _signalType; // "Buy" vagy "Sell"

        public string Symbol
        {
            get => _symbol;
            set { _symbol = value; OnPropertyChanged(); }
        }

        // Ezt kötjük majd a narancssárga kerethez
        public bool HasSignal
        {
            get => _hasSignal;
            set { _hasSignal = value; OnPropertyChanged(); }
        }

        public string SignalType
        {
            get => _signalType;
            set { _signalType = value; OnPropertyChanged(); }
        }

        public WatchlistItemViewModel(string symbol)
        {
            Symbol = symbol;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}