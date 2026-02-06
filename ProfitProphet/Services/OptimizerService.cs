using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        private const double K_DD = 0.5;

        public OptimizerService(BacktestService backtestService)
        {
            _backtestService = backtestService ?? throw new ArgumentNullException(nameof(backtestService));
        }

        // FONTOS: Itt List<OptimizationResult> a visszatérési érték!
        public async Task<List<OptimizationResult>> OptimizeAsync(List<Candle> candles, StrategyProfile profile, List<OptimizationParameter> parameters)
        {
            // Durva keresés (5-ös lépésköz) az összes kombinációra
            return await RunIterationAsync(candles, profile, parameters, 5);
        }

        // Itt is List<OptimizationResult> a típus!
        private async Task<List<OptimizationResult>> RunIterationAsync(List<Candle> candles, StrategyProfile profile, List<OptimizationParameter> parameters, int step)
        {
            var combinations = GenerateCombinations(parameters, step);
            var results = new System.Collections.Concurrent.ConcurrentBag<OptimizationResult>();

            await Task.Run(() =>
            {
                Parallel.ForEach(combinations, (combo) =>
                {
                    var testProfile = DeepCopyProfile(profile);
                    for (int i = 0; i < parameters.Count; i++)
                    {
                        ApplyValue(testProfile, parameters[i], combo[i]);
                    }

                    var res = _backtestService.RunBacktest(candles, testProfile);
                    double score = res.TotalProfitLoss - (K_DD * Math.Abs(res.MaxDrawdown));

                    // Csak a releváns eredményeket mentjük el (min. 5 kötés)
                    if (res.TradeCount >= 5)
                    {
                        // Paraméterek szöveges összefűzése a táblázathoz
                        var paramSummary = string.Join(", ", parameters.Select((p, idx) => $"{p.Rule.LeftIndicatorName}: {combo[idx]}"));

                        results.Add(new OptimizationResult
                        {
                            Values = combo,
                            ParameterSummary = paramSummary, // FONTOS: Ez kell a táblázatba!
                            Score = score,
                            Profit = res.TotalProfitLoss,
                            Drawdown = res.MaxDrawdown,
                            TradeCount = res.TradeCount
                        });
                    }
                });
            });

            // Visszaadjuk a teljes listát
            return results.OrderByDescending(r => r.Score).ToList();
        }

        // ... A többi segédmetódus (GenerateCombinations, GenerateRecursive) maradhat változatlan ...
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
            for (int v = (int)parameters[depth].MinValue; v <= (int)parameters[depth].MaxValue; v += step)
            {
                current[depth] = v;
                GenerateRecursive(parameters, depth + 1, current, results, step);
            }
        }

        // FONTOS: Ennek PUBLIC-nak kell lennie a ViewModel miatt!
        public void ApplyValue(StrategyProfile profile, OptimizationParameter param, int value)
        {
            var targetList = param.IsEntrySide ? profile.EntryGroups : profile.ExitGroups;
            if (targetList == null) return;

            foreach (var group in targetList)
            {
                var ruleToUpdate = group.Rules.FirstOrDefault(r =>
                    r.LeftIndicatorName == param.Rule.LeftIndicatorName &&
                    r.Operator == param.Rule.Operator);

                if (ruleToUpdate != null)
                {
                    switch (param.ParameterName)
                    {
                        case "LeftPeriod": ruleToUpdate.LeftPeriod = value; break;
                        case "RightPeriod": ruleToUpdate.RightPeriod = value; break;
                        case "RightValue": ruleToUpdate.RightValue = value; break;
                    }
                    return;
                }
            }
        }

        // FONTOS: Ennek is PUBLIC-nak kell lennie!
        public StrategyProfile DeepCopyProfile(StrategyProfile original)
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