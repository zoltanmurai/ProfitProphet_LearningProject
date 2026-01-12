using ProfitProphet.Entities;
using ProfitProphet.Models.Backtesting;
using ProfitProphet.Services;
using ProfitProphet.ViewModels.Commands;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;

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

        private PlotModel _equityModel;
        public PlotModel EquityModel
        {
            get => _equityModel;
            set { _equityModel = value; OnPropertyChanged(); }
        }

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
            CreateEquityChart(res.EquityCurve);
            OnTestFinished?.Invoke(res); // ESEMÉNY
        }

        private void CreateEquityChart(List<EquityPoint> points)
        {
            var model = new PlotModel
            {
                Title = "Tőke Növekedés",
                TextColor = OxyColors.White,
                PlotAreaBorderColor = OxyColors.Transparent
            };

            // X tengely (Dátum)
            model.Axes.Add(new DateTimeAxis
            {
                Position = AxisPosition.Bottom,
                StringFormat = "yyyy.MM.dd",
                TextColor = OxyColors.LightGray,
                AxislineColor = OxyColors.Gray,
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColor.FromAColor(40, OxyColors.Gray)
            });

            // Y tengely (Pénz)
            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                TextColor = OxyColors.LightGray,
                AxislineColor = OxyColors.Gray,
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColor.FromAColor(40, OxyColors.Gray),
                StringFormat = "N0" // Ezres tagolás
            });

            // A Vonal
            var series = new LineSeries
            {
                Color = OxyColors.LimeGreen, // Nyerő szín :)
                StrokeThickness = 2,
                MarkerType = MarkerType.None
            };

            foreach (var p in points)
            {
                series.Points.Add(new DataPoint(DateTimeAxis.ToDouble(p.Time), p.Equity));
            }

            model.Series.Add(series);
            EquityModel = model;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}