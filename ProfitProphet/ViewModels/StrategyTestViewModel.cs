using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using ProfitProphet.Entities;
using ProfitProphet.Models.Backtesting;
using ProfitProphet.Models.Strategies;
using ProfitProphet.Services;
using ProfitProphet.ViewModels.Commands;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
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
        private readonly IStrategySettingsService _strategyService;
        public event Action<BacktestResult> OnTestFinished;

        public StrategyProfile CurrentProfile { get; set; }
        public RelayCommand RunTestCommand { get; }
        public ICommand EditStrategyCommand { get; }

        // Paraméterek (a VBA-ban Steps, MAOnCMF1, MA)
        //public int CmfPeriod { get; set; } = 20;
        //public int CmfMaPeriod { get; set; } = 14;
        //public int PriceMaPeriod { get; set; } = 50;

        public string Symbol { get; set; }

        private double _initialCash = 10000; // Alapértéknek jó a 10k, de átírható
        public double InitialCash
        {
            get => _initialCash;
            set { _initialCash = value; OnPropertyChanged(); }
        }

        private DateTime _startDate;
        public DateTime StartDate
        {
            get => _startDate;
            set { _startDate = value; OnPropertyChanged(); }
        }

        private DateTime _endDate;
        public DateTime EndDate
        {
            get => _endDate;
            set { _endDate = value; OnPropertyChanged(); }
        }

        private PlotModel _equityModel;
        public PlotModel EquityModel
        {
            get => _equityModel;
            set { _equityModel = value; OnPropertyChanged(); }
        }

        // Eredmény megjelenítéséhez
        private BacktestResult _result;
        private string selectedSymbol;

        public BacktestResult Result
        {
            get => _result;
            set { _result = value; OnPropertyChanged(); }
        }

        public ICommand RunCommand { get; }

        public StrategyTestViewModel(List<Candle> candles, string symbol, IStrategySettingsService strategyService)
        {
            _candles = candles;
            _strategyService = strategyService;
            Symbol = symbol;
            _backtestService = new BacktestService();
            RunCommand = new RelayCommand(_ => RunTest());

            var allProfiles = _strategyService.LoadProfiles();
            var savedProfile = allProfiles.FirstOrDefault(p => p.Symbol == symbol);

            if (_candles.Any())
            {
                StartDate = _candles.First().TimestampUtc; // Vagy: DateTime.Now.AddYears(-1);
                EndDate = _candles.Last().TimestampUtc;
            }
            else
            {
                StartDate = DateTime.Now.AddYears(-1);
                EndDate = DateTime.Now;
            }

            //CurrentProfile = new StrategyProfile { Symbol = symbol, Name = "Teszt Stratégia" };
            //var defaultGroup = new StrategyGroup { Name = "Alap Setup" };
            if (savedProfile != null)
            {
                CurrentProfile = savedProfile;
            }
            else
            {
                // Ha nincs mentve, csinálunk egy alapértelmezettet
                CurrentProfile = new StrategyProfile { Symbol = symbol, Name = "Alap Stratégia" };
                // ... (Opcionális: alap csoport hozzáadása, hogy ne legyen üres) ...
                var defaultGroup = new StrategyGroup { Name = "Alap Setup" };
                //defaultGroup.Rules.Add(new StrategyRule { LeftIndicatorName = "CMF", LeftPeriod = 20, Operator = ComparisonOperator.GreaterThan, RightValue = 0 });
                defaultGroup.Rules.Add(new StrategyRule
                {
                    LeftIndicatorName = "CMF",
                    LeftPeriod = 20,
                    Operator = ComparisonOperator.GreaterThan,
                    RightSourceType = DataSourceType.Value,
                    RightValue = 0
                });
                CurrentProfile.EntryGroups.Add(defaultGroup);
            }

            // Alapértelmezett szabály (hogy ne legyen üres)
            //CurrentProfile.EntryRules.Add(new StrategyRule
            //{
            //    LeftIndicatorName = "CMF",
            //    LeftPeriod = 20,
            //    Operator = ComparisonOperator.GreaterThan,
            //    RightValue = 0
            //});

            RunTestCommand = new RelayCommand(_ => RunTest());
            EditStrategyCommand = new RelayCommand(OpenStrategyEditor);
        }

        //public StrategyTestViewModel(List<Candle> candles, string selectedSymbol)
        //{
        //    _candles = candles;
        //    this.selectedSymbol = selectedSymbol;
        //}

        private void OpenStrategyEditor(object obj)
        {
            // 1. Létrehozzuk a VM-et az aktuális profillal
            var editorVm = new StrategyEditorViewModel(CurrentProfile);

            // 2. Létrehozzuk az ablakot
            var editorWin = new Views.StrategyEditorWindow();
            editorWin.DataContext = editorVm;

            // 3. Feliratkozunk a bezárásra (hogy a ViewModel-ből a Mentés gomb be tudja zárni)
            editorVm.OnRequestClose += () =>
            {
                editorWin.DialogResult = true; // Ez jelzi a ShowDialog-nak, hogy sikeres (true) volt a bezárás
                editorWin.Close();
            };

            // 4. MEGJELENÍTÉS és EREDMÉNY VIZSGÁLAT
            // Csak egyszer hívjuk meg a ShowDialog-ot!
            bool? result = editorWin.ShowDialog();

            if (result == true)
            {
                // Ha a Mentés gombbal zárták be:
                CurrentProfile = editorVm.Profile;

                // MENTÉS A SERVICE-SZEL (Most már a jó mappába!)
                _strategyService.SaveProfile(CurrentProfile);

                OnPropertyChanged(nameof(CurrentProfile));
            }
        }

        private void RunTest()
        {

            if (_candles == null || _candles.Count == 0)
            {
                MessageBox.Show("Nincs betöltött adat a teszteléshez!", "Hiba");
                return;
            }

            var filteredCandles = _candles
                .Where(c => c.TimestampUtc.Date >= StartDate.Date && c.TimestampUtc.Date <= EndDate.Date)
                .OrderBy(c => c.TimestampUtc)
                .ToList();


            if (filteredCandles.Count < 50) // Ha túl kevés adat maradt
            {
                // Itt jelezhetnénk hibát, de most csak simán nem futtatjuk
                return;
            }

            // Futtatás
            var res = _backtestService.RunBacktest(
                filteredCandles,  // 1. Az adatok
                CurrentProfile    // 2. (ebben vannak a szabályok)
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