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

        public OptimizerService(BacktestService backtestService)
        {
            _backtestService = backtestService ?? throw new ArgumentNullException(nameof(backtestService));
        }

        public async Task<List<OptimizationResult>> OptimizeAsync(
            List<Candle> candles,
            StrategyProfile profile,
            List<OptimizationParameter> parameters,
            IProgress<int> progress,
            CancellationToken token,
            bool visualMode,
            IProgress<OptimizationResult> realtimeHandler)
        {
            // És itt továbbadjuk őket a belső, privát metódusnak:
            return await RunIterationAsync(candles, profile, parameters, progress, token, visualMode, realtimeHandler);
        }

        private async Task<List<OptimizationResult>> RunIterationAsync(
            List<Candle> candles,
            StrategyProfile profile,
            List<OptimizationParameter> parameters,
            IProgress<int> progress,
            CancellationToken token,
            bool visualMode,
            IProgress<OptimizationResult> realtimeHandler) 
        {
            var combinations = GenerateCombinations(parameters);
            var results = new System.Collections.Concurrent.ConcurrentBag<OptimizationResult>();

            int total = combinations.Count;
            int current = 0;

            // Egy egyszerű lock objektum, hogy ne egyszerre akarjon minden szál rajzolni
            object syncLock = new object();
            double bestScoreSoFar = double.MinValue;

            await Task.Run(() =>
            {
                try
                {
                    var options = new ParallelOptions { CancellationToken = token, MaxDegreeOfParallelism = Environment.ProcessorCount };

                    Parallel.ForEach(combinations, options, (combo) =>
                    {
                        options.CancellationToken.ThrowIfCancellationRequested();

                        var testProfile = DeepCopyProfile(profile);
                        for (int i = 0; i < parameters.Count; i++) ApplyValue(testProfile, parameters[i], combo[i]);

                        // Futtatás
                        var res = _backtestService.RunBacktest(candles, testProfile);

                        if (res.TradeCount >= 5)
                        {
                            var paramSummary = string.Join(", ", parameters.Select((p, idx) => $"{p.Rule.LeftIndicatorName}: {combo[idx]}"));

                            var optRes = new OptimizationResult
                            {
                                Values = combo,
                                ParameterSummary = paramSummary,
                                Score = res.TotalProfitLoss, // Vagy ProfitFactor
                                Profit = res.TotalProfitLoss,
                                Drawdown = res.MaxDrawdown,
                                TradeCount = res.TradeCount,
                                ProfitFactor = res.ProfitFactor,
                                // KULCSFONTOSSÁGÚ: Ha vizuális mód van, el kell mentenünk a görbét,
                                // hogy a ViewModel ki tudja rajzolni!
                                EquityCurve = visualMode ? res.EquityCurve : null,
                                BalanceCurve = visualMode ? res.BalanceCurve : null,
                                Trades = visualMode ? res.Trades : null
                            };

                            results.Add(optRes);

                            // --- ITT TÖRTÉNIK A VARÁZSLAT ---
                            if (visualMode && realtimeHandler != null)
                            {
                                // Teljesítmény védelem: Csak akkor küldjük ki a GUI-ra frissítésre,
                                // ha ez az eredmény jobb, mint amit eddig találtunk (így a Chart mindig javul),
                                // VAGY ha mindenképp látni akarjuk a listában a profitosokat.

                                bool isNewBest = false;
                                lock (syncLock)
                                {
                                    // Itt döntheted el: Profit vagy ProfitFactor alapján mi a "legjobb"
                                    if (optRes.Score > bestScoreSoFar)
                                    {
                                        bestScoreSoFar = optRes.Score;
                                        isNewBest = true;
                                    }
                                }

                                // Ha ez egy új rekord, vagy legalábbis profitos, küldjük a GUI-nak
                                if (isNewBest || optRes.Profit > 0)
                                {
                                    realtimeHandler.Report(optRes);
                                }
                            }
                        }

                        // Progress update (százalék)
                        int c = System.Threading.Interlocked.Increment(ref current);
                        if (total > 0 && c % (Math.Max(1, total / 100)) == 0)
                            progress?.Report((int)((double)c / total * 100));
                    });
                }
                catch (OperationCanceledException) { }
            });

            return results.OrderByDescending(r => r.Score).ToList();
        }

        private List<int[]> GenerateCombinations(List<OptimizationParameter> parameters)
        {
            var results = new List<int[]>();
            // Elindítjuk a rekurziót 0. mélységről
            GenerateRecursive(parameters, 0, new int[parameters.Count], results);
            return results;
        }

        private void GenerateRecursive(List<OptimizationParameter> parameters, int depth, int[] current, List<int[]> results)
        {
            // Kilépési feltétel (ha minden paraméternek van értéke)
            if (depth == parameters.Count)
            {
                results.Add((int[])current.Clone());
                return;
            }

            // Feltételezzük, hogy az OptimizationParameter objektumnak van 'Step' tulajdonsága (int).
            // Ha nincs, akkor itt helyettesítsd be a kívánt fix lépésközt (pl. 1).
            int currentStep = 1;
            // int currentStep = (int)parameters[depth].Step; // Ha van Step property

            for (int v = (int)parameters[depth].MinValue; v <= (int)parameters[depth].MaxValue; v += currentStep)
            {
                current[depth] = v;
                GenerateRecursive(parameters, depth + 1, current, results);
            }
        }

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