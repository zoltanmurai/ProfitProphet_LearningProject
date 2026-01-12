using ProfitProphet.Entities;
using ProfitProphet.Services;
using ProfitProphet.ViewModels.Commands;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ProfitProphet.ViewModels
{
    public class StrategyTestViewModel : INotifyPropertyChanged
    {
        private readonly BacktestService _backtestService;
        private readonly List<Candle> _candles;
        public event Action<BacktestResult> OnTestFinished;

        // Paraméterek (a VBA-ban Steps, MAOnCMF1, MA)
        public int CmfPeriod { get; set; } = 20;
        public int CmfMaPeriod { get; set; } = 14;
        public int PriceMaPeriod { get; set; } = 50;

        public string Symbol { get; set; }

        // Eredmény megjelenítéséhez
        private BacktestResult _result;
        public BacktestResult Result
        {
            get => _result;
            set { _result = value; OnPropertyChanged(); }
        }

        public ICommand RunCommand { get; }

        public StrategyTestViewModel(List<Candle> candles, string symbol)
        {
            _candles = candles;
            Symbol = symbol;
            _backtestService = new BacktestService();
            RunCommand = new RelayCommand(_ => RunTest());
        }

        private void RunTest()
        {
            if (_candles == null || _candles.Count == 0)
            {
                MessageBox.Show("Nincs betöltött adat a teszteléshez!", "Hiba");
                return;
            }

            // Futtatás
            var res = _backtestService.RunBacktest(
                _candles,
                CmfPeriod,
                CmfMaPeriod,
                PriceMaPeriod
            );

            Result = res; // frissíti a felületet
            OnTestFinished?.Invoke(res); // ESEMÉNY
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}