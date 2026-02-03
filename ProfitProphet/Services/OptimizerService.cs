using System;
using System.Collections.Generic;
using System.Collections.ObjectModel; // EZ kellett az ObservableCollection-höz!
using System.Linq;
using System.Threading.Tasks;
using ProfitProphet.Entities;
using ProfitProphet.Models.Backtesting;
using ProfitProphet.Models.Strategies;

namespace ProfitProphet.Services
{
    public class OptimizerService
    {
        private readonly BacktestService _backtestService;
        private const double K_DD = 0.5; // Kockázati büntető (Score számításhoz)

        public OptimizerService(BacktestService backtestService)
        {
            _backtestService = backtestService ?? throw new ArgumentNullException(nameof(backtestService));
        }

        //public async Task<OptimizationResult> OptimizeAsync(List<Candle> candles, StrategyProfile profile, List<OptimizationParameter> parameters)
        //{
        public async Task<List<OptimizationResult>> OptimizeAsync(List<Candle> candles, StrategyProfile profile, List<OptimizationParameter> parameters)
        {
            int coarseStep = 5;
            OptimizationResult bestResult = null;

            // 1. COARSE SEARCH (Durva pásztázás 5-ös lépésközzel)
            while (coarseStep >= 1 && bestResult == null)
            {
                bestResult = await RunIterationAsync(candles, profile, parameters, coarseStep);
                if (bestResult == null) coarseStep -= 2; // Ha nem talál, csökkenti a lépésközt (5 -> 3 -> 1)
            }

            if (bestResult == null) return null;

            // 2. FINE SEARCH (Finomhangolás a legjobb találat körül)
            // Itt hozzuk létre az új paraméterlistát a szűkített tartománnyal
            var fineParams = parameters.Select((p, i) => new OptimizationParameter
            {
                Rule = p.Rule,
                IsEntrySide = p.IsEntrySide,
                ParameterName = p.ParameterName,
                // A talált érték +/- coarseStep tartományában keresünk tovább, de 1-es lépésközzel
                MinValue = Math.Max(p.MinValue, bestResult.Values[i] - coarseStep),
                MaxValue = Math.Min(p.MaxValue, bestResult.Values[i] + coarseStep)
            }).ToList();

            // Futtatás 1-es lépésközzel (precíz keresés)
            var finalResult = await RunIterationAsync(candles, profile, fineParams, 1);

            // Ha a finom keresés jobb, azt adjuk vissza, egyébként a durvát
            //return finalResult ?? bestResult;
            //var allResults = await RunIterationAsync(candles, profile, parameters, 5);
            //return allResults.OrderByDescending(r => r.Score).ToList();
            var allResults = new List<OptimizationResult>();
            var bestResultSingle = await RunIterationAsync(candles, profile, parameters, 5);
            if (bestResultSingle != null)
            {
                allResults.Add(bestResultSingle);
            }
            return allResults.OrderByDescending(r => r.Score).ToList();
        }

        private async Task<OptimizationResult> RunIterationAsync(List<Candle> candles, StrategyProfile profile, List<OptimizationParameter> parameters, int step)
        {
            var combinations = GenerateCombinations(parameters, step);
            var results = new System.Collections.Concurrent.ConcurrentBag<OptimizationResult>();

            await Task.Run(() =>
            {
                Parallel.ForEach(combinations, (combo) =>
                {
                    // Fontos: Minden szálon saját másolatot használunk a profilból!
                    var testProfile = DeepCopyProfile(profile);

                    // Paraméterek beállítása a másolaton
                    for (int i = 0; i < parameters.Count; i++)
                    {
                        ApplyValue(testProfile, parameters[i], combo[i]);
                    }

                    // Teszt futtatása
                    var res = _backtestService.RunBacktest(candles, testProfile);

                    // Score számítás (Profit - Drawdown büntetés)
                    double score = res.TotalProfitLoss - (K_DD * Math.Abs(res.MaxDrawdown));

                    // Csak akkor tároljuk el, ha van elég kötés (statisztikai minimum)
                    if (res.TradeCount >= 5)
                    {
                        var paramSummary = string.Join(", ", parameters.Select((p, idx) => $"{p.Rule.LeftIndicatorName}: {combo[idx]}"));

                        results.Add(new OptimizationResult
                        {
                            Values = combo,
                            Score = score,
                            Profit = res.TotalProfitLoss,
                            Drawdown = res.MaxDrawdown,
                            TradeCount = res.TradeCount
                        });
                    }
                });
            });

            return results.OrderByDescending(r => r.Score).FirstOrDefault();
        }

        private List<int[]> GenerateCombinations(List<OptimizationParameter> parameters, int step)
        {
            var results = new List<int[]>();
            GenerateRecursive(parameters, 0, new int[parameters.Count], results, step);
            return results;
        }

        private void GenerateRecursive(List<OptimizationParameter> parameters, int depth, int[] current, List<int[]> results, int step)
        {
            if (depth == parameters.Count)
            {
                results.Add((int[])current.Clone());
                return;
            }

            // Explicit (int) castolás a MinValue/MaxValue-ra
            for (int v = (int)parameters[depth].MinValue; v <= (int)parameters[depth].MaxValue; v += step)
            {
                current[depth] = v;
                GenerateRecursive(parameters, depth + 1, current, results, step);
            }
        }

        private void ApplyValue(StrategyProfile profile, OptimizationParameter param, int value)
        {
            // 1. Megkeressük, melyik listában van (Entry vagy Exit)
            var targetList = param.IsEntrySide ? profile.EntryGroups : profile.ExitGroups;

            if (targetList == null) return;

            // 2. Megkeressük a szabályt. 
            foreach (var group in targetList)
            {
                var ruleToUpdate = group.Rules.FirstOrDefault(r =>
                    r.LeftIndicatorName == param.Rule.LeftIndicatorName &&
                    r.Operator == param.Rule.Operator);

                if (ruleToUpdate != null)
                {
                    // Az új ParameterName alapján döntjük el, mit írunk át
                    switch (param.ParameterName)
                    {
                        case "LeftPeriod":
                            ruleToUpdate.LeftPeriod = value;
                            break;
                        case "RightPeriod":
                            ruleToUpdate.RightPeriod = value;
                            break;
                        case "RightValue":
                            ruleToUpdate.RightValue = value;
                            break;
                    }
                    return;
                }
            }
        }

        // Egyszerű mélymásolat készítő
        private StrategyProfile DeepCopyProfile(StrategyProfile original)
        {
            var copy = new StrategyProfile
            {
                Name = original.Name,
                Symbol = original.Symbol,
                TradeAmount = original.TradeAmount,
                AmountType = original.AmountType,
                CommissionPercent = original.CommissionPercent,
                MinCommission = original.MinCommission,
                AllowPyramiding = original.AllowPyramiding,
                OnlySellInProfit = original.OnlySellInProfit,

                EntryGroups = new List<StrategyGroup>(),
                ExitGroups = new List<StrategyGroup>()
            };

            foreach (var g in original.EntryGroups) copy.EntryGroups.Add(DeepCopyGroup(g));
            foreach (var g in original.ExitGroups) copy.ExitGroups.Add(DeepCopyGroup(g));

            return copy;
        }

        private StrategyGroup DeepCopyGroup(StrategyGroup g)
        {
            // JAVÍTVA: List helyett ObservableCollection
            var newG = new StrategyGroup { Rules = new ObservableCollection<StrategyRule>() };

            foreach (var r in g.Rules)
            {
                newG.Rules.Add(new StrategyRule
                {
                    LeftIndicatorName = r.LeftIndicatorName,
                    LeftPeriod = r.LeftPeriod,
                    Operator = r.Operator,
                    RightSourceType = r.RightSourceType,
                    RightIndicatorName = r.RightIndicatorName,
                    RightPeriod = r.RightPeriod,
                    RightValue = r.RightValue
                });
            }
            return newG;
        }
    }
}