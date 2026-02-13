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
                        if (token.IsCancellationRequested)
                        {
                            state.Stop();
                            return;
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

                            // SZINKRONIZÁLÁS
                            // A) Sorok közötti szinkron (Lock ikon)
                            foreach (var group in testProfile.EntryGroups) SyncGroup(group);
                            foreach (var group in testProfile.ExitGroups) SyncGroup(group);

                            // B) Soron belüli szinkron (Smart Sync)
                            foreach (var group in testProfile.EntryGroups) SyncInternalRules(group);
                            foreach (var group in testProfile.ExitGroups) SyncInternalRules(group);

                            if (token.IsCancellationRequested)
                            {
                                state.Stop();
                                return;
                            }

                            // 3. Futtatjuk a tesztet
                            var res = _backtestService.RunBacktest(candles, testProfile);

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

                            int c = System.Threading.Interlocked.Increment(ref current);
                            if (total > 0 && c % Math.Max(1, total / 100) == 0)
                            {
                                progress?.Report((int)((double)c / total * 100));
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error in optimization iteration: {ex.Message}");
                        }
                    });

                    System.Diagnostics.Debug.WriteLine($"Parallel processing completed at {DateTime.Now}");
                }
                catch (OperationCanceledException)
                {
                    System.Diagnostics.Debug.WriteLine("Optimization cancelled before start");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Unexpected error in optimization: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                    throw;
                }

                if (token.IsCancellationRequested)
                {
                    System.Diagnostics.Debug.WriteLine("Optimization was cancelled - throwing OperationCanceledException");
                    throw new OperationCanceledException(token);
                }
            }, token);

            return results.OrderByDescending(r => r.Score).ToList();
        }

        // --- ÚJ: HIÁNYZÓ METÓDUS A SZINKRONIZÁLÁSHOZ ---
        private void SyncGroup(StrategyGroup group)
        {
            if (group == null || group.Rules.Count < 2) return;

            for (int i = 1; i < group.Rules.Count; i++)
            {
                var currentRule = group.Rules[i];
                var prevRule = group.Rules[i - 1];

                // Bal oldal
                if (currentRule.IsLeftLinked)
                {
                    currentRule.LeftPeriod = prevRule.LeftPeriod;
                    currentRule.LeftParameter2 = prevRule.LeftParameter2;
                    currentRule.LeftParameter3 = prevRule.LeftParameter3;
                }

                // Jobb oldal
                if (currentRule.IsRightLinked &&
                    currentRule.RightSourceType == DataSourceType.Indicator &&
                    prevRule.RightSourceType == DataSourceType.Indicator)
                {
                    currentRule.RightPeriod = prevRule.RightPeriod;
                    currentRule.RightParameter2 = prevRule.RightParameter2;
                    currentRule.RightParameter3 = prevRule.RightParameter3;
                }
            }
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
            if (parameters[depth].Step > 0) currentStep = (int)parameters[depth].Step;

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
                    IsSameRule(r, param.Rule));

                if (ruleToUpdate != null)
                {
                    switch (param.ParameterName)
                    {
                        case "LeftPeriod": ruleToUpdate.LeftPeriod = value; break;
                        case "LeftParameter2": ruleToUpdate.LeftParameter2 = value; break;
                        case "LeftParameter3": ruleToUpdate.LeftParameter3 = value; break;
                        case "RightPeriod": ruleToUpdate.RightPeriod = value; break;
                        case "RightParameter2": ruleToUpdate.RightParameter2 = value; break;
                        case "RightParameter3": ruleToUpdate.RightParameter3 = value; break;
                        case "RightValue": ruleToUpdate.RightValue = value; break;
                        case "LeftShift": ruleToUpdate.LeftShift = value; break;
                        case "RightShift": ruleToUpdate.RightShift = value; break;
                    }
                }
            }
        }

        private bool IsSameRule(StrategyRule r1, StrategyRule r2)
        {
            return r1.LeftIndicatorName == r2.LeftIndicatorName &&
                   r1.LeftPeriod == r2.LeftPeriod &&
                   r1.LeftParameter2 == r2.LeftParameter2 &&
                   r1.LeftParameter3 == r2.LeftParameter3 &&
                   r1.Operator == r2.Operator &&
                   r1.RightSourceType == r2.RightSourceType &&
                   r1.RightIndicatorName == r2.RightIndicatorName &&
                   r1.RightPeriod == r2.RightPeriod &&
                   r1.RightParameter2 == r2.RightParameter2 &&
                   r1.RightParameter3 == r2.RightParameter3 &&
                   Math.Abs(r1.RightValue - r2.RightValue) < 0.0001;
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

                    // LINKELÉSEK MÁSOLÁSA 
                    IsLeftLinked = r.IsLeftLinked,
                    IsRightLinked = r.IsRightLinked,

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

        private void SyncInternalRules(StrategyGroup group)
        {
            if (group == null) return;

            foreach (var rule in group.Rules)
            {
                // Ugyanaz a logika, mint az OptimizationViewModel-ben:
                // Ha egy családba tartoznak, a Jobb oldalnak követnie KELL a Balt.
                if (IsSameFamily(rule.LeftIndicatorName, rule.RightIndicatorName))
                {
                    rule.RightPeriod = rule.LeftPeriod;
                    rule.RightParameter2 = rule.LeftParameter2;
                    rule.RightParameter3 = rule.LeftParameter3;
                }
            }
        }

        private bool IsSameFamily(string leftName, string rightName)
        {
            if (string.IsNullOrEmpty(leftName) || string.IsNullOrEmpty(rightName)) return false;
            string l = leftName.ToUpper();
            string r = rightName.ToUpper();

            // --- KIVÉTELEK (Ezeket NEM szabad szinkronizálni) ---

            // 1. Mozgóátlagok (SMA 50 vs SMA 200 - el kell térniük!)
            if (l.StartsWith("SMA") || l.StartsWith("EMA")) return false;

            // 2. CMF vs CMF_MA (Az MA hossza független a CMF hosszától!)
            // Ha az egyik sima, a másik MA, akkor engedni kell a külön állítást.
            if (l == "CMF" && r == "CMF_MA") return false;
            if (l == "CMF_MA" && r == "CMF") return false;

            // 3. RSI vs RSI_MA (Szintén: az MA simítás hossza független)
            if (l == "RSI" && r == "RSI_MA") return false;
            if (l == "RSI_MA" && r == "RSI") return false;

            // --- VALÓDI CSALÁDOK (Ezeket szinkronizáljuk) ---

            // STOCHASTIC (Main és Signal ugyanabból a periódusból számolódik)
            if (l.Contains("STOCH") && r.Contains("STOCH")) return true;

            // MACD (Main és Signal ugyanazokkal a paraméterekkel kell fusson)
            if (l.Contains("MACD") && r.Contains("MACD")) return true;

            // BOLLINGER (Alsó/Felső szalag ugyanazzal a beállítással)
            if ((l.Contains("BB") || l.Contains("BOLLINGER")) &&
                (r.Contains("BB") || r.Contains("BOLLINGER"))) return true;

            // (Az RSI vs RSI és CMF vs CMF marad true)
            if (l == "RSI" && r == "RSI") return true;
            if (l == "CMF" && r == "CMF") return true;

            return false;
        }
    }
}