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
            System.Diagnostics.Debug.WriteLine($"Processors: {Environment.ProcessorCount}, Using: {Math.Max(1, Environment.ProcessorCount - 2)}");

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

            object syncLock = new object();
            double bestScoreSoFar = double.MinValue;

            await Task.Run(() =>
            {
                var options = new ParallelOptions
                {
                    CancellationToken = token,
                    MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 2)
                };

                try
                {
                    System.Diagnostics.Debug.WriteLine($"Starting parallel processing at {DateTime.Now}");

                    Parallel.ForEach(combinations, options, (combo, state) =>
                    {
                        // 1. CANCELLATION CHECK - Kivétel dobás HELYETT return
                        if (token.IsCancellationRequested)
                        {
                            state.Stop(); // Jelezzük a Parallel.ForEach-nek: ne indítson új iterációkat
                            return;       // Kilépünk ebből az iterációból
                        }

                        try
                        {
                            // 1. Létrehozzuk a munkapéldányt
                            var testProfile = DeepCopyProfile(profile);

                            // 2. Beállítjuk a paramétereket
                            for (int i = 0; i < parameters.Count; i++)
                            {
                                ApplyValue(testProfile, parameters[i], combo[i]);
                            }

                            // Munka közben is ellenőrizzük (ha hosszú a backtest)
                            if (token.IsCancellationRequested)
                            {
                                state.Stop();
                                return;
                            }

                            // 3. Futtatjuk a tesztet
                            var res = _backtestService.RunBacktest(candles, testProfile);

                            // Újabb ellenőrzés az eredmény feldolgozása előtt
                            if (token.IsCancellationRequested)
                            {
                                state.Stop();
                                return;
                            }

                            if (visualMode || res.TradeCount > 0)
                            {
                                var paramSummary = string.Join(", ", parameters.Select((p, idx) =>
                                {
                                    string name;
                                    if (p.ParameterName.Contains("Right"))
                                    {
                                        name = !string.IsNullOrEmpty(p.Rule.RightIndicatorName)
                                               ? p.Rule.RightIndicatorName
                                               : "Right Value";
                                    }
                                    else
                                    {
                                        name = p.Rule.LeftIndicatorName;
                                    }
                                    return $"{name}: {combo[idx]}";
                                }));

                                double currentProfit = res.TotalProfitLoss;

                                if (currentProfit > 0 || (visualMode && results.Count < 5))
                                {
                                    var optRes = new OptimizationResult
                                    {
                                        Values = combo,
                                        ParameterSummary = paramSummary,
                                        Score = res.TotalProfitLoss,
                                        Profit = res.TotalProfitLoss,
                                        Drawdown = res.MaxDrawdown,
                                        TradeCount = res.TradeCount,
                                        ProfitFactor = res.ProfitFactor,
                                        EquityCurve = visualMode ? res.EquityCurve : null,
                                        BalanceCurve = visualMode ? res.BalanceCurve : null,
                                        Trades = visualMode ? res.Trades : null,
                                        OptimizedProfile = testProfile.DeepClone()
                                    };

                                    lock (results)
                                    {
                                        results.Add(optRes);
                                    }

                                    // Realtime report
                                    if (visualMode && realtimeHandler != null)
                                    {
                                        bool isNewBest = false;
                                        lock (syncLock)
                                        {
                                            if (optRes.Score > bestScoreSoFar)
                                            {
                                                bestScoreSoFar = optRes.Score;
                                                isNewBest = true;
                                            }
                                        }

                                        if (isNewBest || optRes.Profit > 0 || results.Count < 5)
                                        {
                                            realtimeHandler.Report(optRes);
                                        }
                                    }
                                }
                            }

                            // Progress update
                            int c = System.Threading.Interlocked.Increment(ref current);
                            if (total > 0 && c % Math.Max(1, total / 100) == 0)
                            {
                                progress?.Report((int)((double)c / total * 100));
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error in optimization iteration: {ex.Message}");
                            // Egyéb hibákat csak logoljuk, ne állítsuk le az egész optimalizációt
                        }
                    });

                    System.Diagnostics.Debug.WriteLine($"Parallel processing completed at {DateTime.Now}");
                }
                catch (OperationCanceledException)
                {
                    // Ez csak akkor fut, ha a ParallelOptions dobta a kivételt (indulás előtt)
                    System.Diagnostics.Debug.WriteLine("Optimization cancelled before start");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Unexpected error in optimization: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                    throw; // Valódi hibát továbbdobjuk
                }

                // 2. A CIKLUS UTÁN ellenőrizzük és dobjuk a kivételt EGYETLEN EGYSZER
                // Így a ViewModel tudni fogja, hogy stop volt
                if (token.IsCancellationRequested)
                {
                    System.Diagnostics.Debug.WriteLine("Optimization was cancelled - throwing OperationCanceledException");
                    throw new OperationCanceledException(token);
                }
            }, token);


            return results.OrderByDescending(r => r.Score).ToList();
        }

        private List<int[]> GenerateCombinations(List<OptimizationParameter> parameters)
        {
            var results = new List<int[]>();
            GenerateRecursive(parameters, 0, new int[parameters.Count], results);
            return results;
        }

        private void GenerateRecursive(List<OptimizationParameter> parameters, int depth, int[] current, List<int[]> results)
        {
            if (depth == parameters.Count)
            {
                results.Add((int[])current.Clone());
                return;
            }

            int currentStep = 1;

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
                        case "LeftShift": ruleToUpdate.LeftShift = value; break;
                        case "RightShift": ruleToUpdate.RightShift = value; break;
                    }
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
                    LeftParameter2 = r.LeftParameter2,
                    LeftParameter3 = r.LeftParameter3,
                    LeftShift = r.LeftShift,

                    Operator = r.Operator,

                    RightSourceType = r.RightSourceType,
                    RightIndicatorName = r.RightIndicatorName,
                    RightPeriod = r.RightPeriod,
                    RightParameter2 = r.RightParameter2,
                    RightParameter3 = r.RightParameter3,
                    RightShift = r.RightShift,
                    RightValue = r.RightValue
                });
            }
            return newG;
        }
    }
}