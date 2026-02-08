using ProfitProphet.Entities;
using ProfitProphet.Models.Backtesting;
using ProfitProphet.Models.Strategies;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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
        public async Task<List<OptimizationResult>> OptimizeAsync(List<Candle> candles, StrategyProfile profile, List<OptimizationParameter> parameters, IProgress<int> progress, CancellationToken token)
        {
            return await RunIterationAsync(candles, profile, parameters, 5, progress, token);
        }

        // Itt is List<OptimizationResult> a típus!
        private async Task<List<OptimizationResult>> RunIterationAsync(List<Candle> candles, StrategyProfile profile, List<OptimizationParameter> parameters, int step, IProgress<int> progress, CancellationToken token)
        {
            var combinations = GenerateCombinations(parameters, step);
            var results = new System.Collections.Concurrent.ConcurrentBag<OptimizationResult>();

            int total = combinations.Count;
            int current = 0;

            await Task.Run(() =>
            {
                try
                {
                    // ITT A VÁLTOZÁS: Átadjuk a tokent a ParallelOptions-nek
                    var options = new ParallelOptions { CancellationToken = token, MaxDegreeOfParallelism = Environment.ProcessorCount };

                    Parallel.ForEach(combinations, options, (combo) =>
                    {
                        // Minden körben ellenőrizzük, kérték-e a leállást (opcionális, mert az options kezeli, de jó ha itt is van)
                        options.CancellationToken.ThrowIfCancellationRequested();

                        var testProfile = DeepCopyProfile(profile);
                        for (int i = 0; i < parameters.Count; i++) ApplyValue(testProfile, parameters[i], combo[i]);

                        var res = _backtestService.RunBacktest(candles, testProfile); // Sima futtatás

                        // ... (A Score számítás és eredmény hozzáadás marad a régi) ...
                        if (res.TradeCount >= 5)
                        {
                            // ... (marad a régi)
                            var paramSummary = string.Join(", ", parameters.Select((p, idx) => $"{p.Rule.LeftIndicatorName}: {combo[idx]}"));
                            results.Add(new OptimizationResult
                            {
                                Values = combo,
                                ParameterSummary = paramSummary,
                                Score = res.TotalProfitLoss, // Vagy ProfitFactor, ahogy beszéltük
                                Profit = res.TotalProfitLoss,
                                Drawdown = res.MaxDrawdown,
                                TradeCount = res.TradeCount
                            });
                        }

                        // Progress
                        int c = System.Threading.Interlocked.Increment(ref current);
                        if (total > 0 && c % (total / 100 + 1) == 0) progress?.Report((int)((double)c / total * 100));
                    });
                }
                catch (OperationCanceledException)
                {
                    // Ha leállították, nem csinálunk semmit, csak kilépünk a catch-be
                }
            });

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