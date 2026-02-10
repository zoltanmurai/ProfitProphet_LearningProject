using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ProfitProphet.ViewModels
{
    public class WatchlistItemViewModel : INotifyPropertyChanged
    {
        private string _symbol;
        private bool _hasSignal;
        private string _signalType;
        private bool _isAcknowledged;

        public WatchlistItemViewModel(string symbol)
        {
            _symbol = symbol;
            //_hasSignal = false;
            //_signalType = string.Empty;
            //_isAcknowledged = false;
        }

        public string Symbol
        {
            get => _symbol;
            set
            {
                _symbol = value;
                OnPropertyChanged();
            }
        }

        public bool HasSignal
        {
            get => _hasSignal;
            set
            {
                _hasSignal = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsNewSignal));
            }
        }

        public string SignalType
        {
            get => _signalType;
            set
            {
                _signalType = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Jelzi, hogy a felhasználó látta-e már ezt a signalt (rákattintott-e a szimbólumra)
        /// </summary>
        public bool IsAcknowledged
        {
            get => _isAcknowledged;
            set
            {
                _isAcknowledged = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsNewSignal)); // Frissítjük az IsNewSignal-t is
            }
        }

        /// <summary>
        /// TRUE = Új signal (narancssárga keret kell)
        /// FALSE = Látta már vagy nincs signal
        /// </summary>
        public bool IsNewSignal => HasSignal && !IsAcknowledged;

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}