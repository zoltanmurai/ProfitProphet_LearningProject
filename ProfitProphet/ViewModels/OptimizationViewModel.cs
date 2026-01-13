using ProfitProphet.Entities;
using ProfitProphet.Models.Strategies;
using ProfitProphet.Services;
using ProfitProphet.ViewModels.Commands;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ProfitProphet.ViewModels
{
    public class OptimizationViewModel : INotifyPropertyChanged
    {
        private readonly OptimizerService _optimizerService;
        private readonly StrategyProfile _profile;
        private readonly List<Candle> _candles;
        private bool _isRunning;
        private string _statusText = "Készen áll az indításra";

        public ObservableCollection<OptimizationParameterUI> AvailableParameters { get; } = new();
        public ICommand RunOptimizationCommand { get; }

        public bool IsRunning
        {
            get => _isRunning;
            set { _isRunning = value; OnPropertyChanged(); }
        }

        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); }
        }

        public OptimizationViewModel(StrategyProfile profile, List<Candle> candles, OptimizerService optimizerService)
        {
            _profile = profile;
            _candles = candles;
            _optimizerService = optimizerService;

            // Összeszedjük a szabályokat a stratégiából, amiknek van periódusa (optimalizálhatóak)
            var allRules = profile.EntryGroups.SelectMany(g => g.Rules)
                                 .Concat(profile.ExitGroups.SelectMany(g => g.Rules));

            foreach (var rule in allRules)
            {
                // Bal oldal mindig optimalizálható (ha nem konstans ár)
                AvailableParameters.Add(new OptimizationParameterUI { Rule = rule, IsLeftSide = true });

                // Jobb oldal csak ha indikátor
                if (rule.RightSourceType == DataSourceType.Indicator)
                {
                    AvailableParameters.Add(new OptimizationParameterUI { Rule = rule, IsLeftSide = false });
                }
            }

            RunOptimizationCommand = new RelayCommand(async _ => await RunOptimizationAsync(), _ => !IsRunning);
        }

        private async Task RunOptimizationAsync()
        {
            var selectedParams = AvailableParameters.Where(p => p.IsSelected).ToList();
            if (!selectedParams.Any()) return;

            IsRunning = true;
            StatusText = "Optimalizálás folyamatban (Zoli-logika: Coarse Search)...";

            try
            {
                // Átalakítjuk a UI paramétereket a motor számára érthető formátumba
                var optParams = selectedParams.Select(p => new OptimizationParameter
                {
                    Rule = p.Rule,
                    IsLeftSide = p.IsLeftSide,
                    MinValue = p.MinValue,
                    MaxValue = p.MaxValue
                }).ToList();

                // INDÍTÁS
                var result = await _optimizerService.OptimizeAsync(_candles, _profile, optParams);

                if (result != null)
                {
                    StatusText = $"SIKER! Új Score: {result.Score:N2} | Profit: {result.Profit:N0}$ | Kötések: {result.TradeCount}";
                    // Itt a motor már átírta az eredeti _profile értékeit a legjobbra!
                }
                else
                {
                    StatusText = "Nem találtam a feltételeknek megfelelő beállítást.";
                }
            }
            catch (Exception ex)
            {
                StatusText = "Hiba történt: " + ex.Message;
            }
            finally
            {
                IsRunning = false;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}