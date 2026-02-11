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

            // És itt továbbadjuk őket a belső, privát metódusnak:
            return await RunIterationAsync(candles, profile, parameters, progress, token, visualMode, realtimeHandler);
        }

        //private async Task<List<OptimizationResult>> RunIterationAsync(
        //    List<Candle> candles,
        //    StrategyProfile profile,
        //    List<OptimizationParameter> parameters,
        //    IProgress<int> progress,
        //    CancellationToken token,
        //    bool visualMode,
        //    IProgress<OptimizationResult> realtimeHandler) 
        //{
        //    var combinations = GenerateCombinations(parameters);
        //    var results = new System.Collections.Concurrent.ConcurrentBag<OptimizationResult>();

        //    int total = combinations.Count;
        //    int current = 0;

        //    // Egy egyszerű lock objektum, hogy ne egyszerre akarjon minden szál rajzolni
        //    object syncLock = new object();
        //    double bestScoreSoFar = double.MinValue;

        //    await Task.Run(() =>
        //    {
        //        var options = new ParallelOptions 
        //        { 
        //            CancellationToken = token,
        //            //MaxDegreeOfParallelism = Environment.ProcessorCount
        //            // hagyunk x magot.
        //            MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 2)
        //        };
        //        try
        //        {
        //            Parallel.ForEach(combinations, options, (combo, state) =>
        //            {
        //                //options.CancellationToken.ThrowIfCancellationRequested();
        //                //if (options.CancellationToken.IsCancellationRequested)
        //                //{
        //                //    state.Stop(); // Jelzi a többi szálnak, hogy álljanak le
        //                //    return;       // Kilépünk az aktuális iterációból
        //                //}
        //                // HELYES CANCELLATION fent nem jó, leáll, de nem látok semmit a UI-n, hogy leállt, ezért így csinálom:
        //                token.ThrowIfCancellationRequested();

        //                if (state.ShouldExitCurrentIteration) return;

        //                var testProfile = DeepCopyProfile(profile);
        //                for (int i = 0; i < parameters.Count; i++) ApplyValue(testProfile, parameters[i], combo[i]);

        //                // Futtatás
        //                var res = _backtestService.RunBacktest(candles, testProfile);

        //                //if (res.TradeCount >= 5)
        //                if (visualMode || res.TradeCount > 0)
        //                {
        //                    //var paramSummary = string.Join(", ", parameters.Select((p, idx) => $"{p.Rule.LeftIndicatorName}: {combo[idx]}"));
        //                    var paramSummary = string.Join(", ", parameters.Select((p, idx) =>
        //                    {
        //                        // Megnézzük, hogy a paraméter neve alapján a Jobb vagy a Bal oldalt állítjuk-e
        //                        string name;

        //                        if (p.ParameterName.Contains("Right"))
        //                        {
        //                            // Ha a jobb oldalt állítjuk (pl. RightPeriod), akkor a jobb indikátor nevét írjuk ki
        //                            name = !string.IsNullOrEmpty(p.Rule.RightIndicatorName)
        //                                   ? p.Rule.RightIndicatorName
        //                                   : "Right Value"; // Ha fix érték
        //                        }
        //                        else
        //                        {
        //                            // Ha a bal oldalt (pl. LeftPeriod), akkor a bal indikátort
        //                            name = p.Rule.LeftIndicatorName;
        //                        }

        //                        return $"{name}: {combo[idx]}";
        //                    }));

        //                    var optRes = new OptimizationResult
        //                    {
        //                        Values = combo,
        //                        ParameterSummary = paramSummary,
        //                        Score = res.TotalProfitLoss, // Vagy ProfitFactor
        //                        Profit = res.TotalProfitLoss,
        //                        Drawdown = res.MaxDrawdown,
        //                        TradeCount = res.TradeCount,
        //                        ProfitFactor = res.ProfitFactor,
        //                        // KULCSFONTOSSÁGÚ: Ha vizuális mód van, el kell mentenünk a görbét,
        //                        // hogy a ViewModel ki tudja rajzolni!
        //                        EquityCurve = visualMode ? res.EquityCurve : null,
        //                        BalanceCurve = visualMode ? res.BalanceCurve : null,
        //                        Trades = visualMode ? res.Trades : null
        //                    };

        //                    results.Add(optRes);

        //                    if (visualMode && realtimeHandler != null)
        //                    {
        //                        if (options.CancellationToken.IsCancellationRequested) return;

        //                        bool isNewBest = false;
        //                        lock (syncLock)
        //                        {
        //                            if (optRes.Score > bestScoreSoFar)
        //                            {
        //                                bestScoreSoFar = optRes.Score;
        //                                isNewBest = true;
        //                            }
        //                        }

        //                        //if (isNewBest || optRes.Profit > 0)
        //                        //{
        //                        //    realtimeHandler.Report(optRes);
        //                        //}
        //                        if (isNewBest || optRes.Profit > 0 || results.Count < 5)
        //                        {
        //                            realtimeHandler.Report(optRes);
        //                        }
        //                    }
        //                }

        //                // Progress update (százalék)
        //                if (!options.CancellationToken.IsCancellationRequested)
        //                {
        //                    int c = System.Threading.Interlocked.Increment(ref current);
        //                    if (total > 0 && c % (Math.Max(1, total / 100)) == 0)
        //                        progress?.Report((int)((double)c / total * 100));
        //                }
        //            });
        //        }
        //        catch (OperationCanceledException) { }
        //        catch (AggregateException ae)
        //        {
        //            ae.Handle(ex => ex is OperationCanceledException); // Ha csak Cancel volt, "lenyeljük" a hibát
        //        }
        //    });

        //    return results.OrderByDescending(r => r.Score).ToList();
        //}

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
                        // HELYES CANCELLATION
                        token.ThrowIfCancellationRequested();

                        try
                        {
                            //  MINDEN iteráció saját másolatot kap
                            var testProfile = DeepCopyProfile(profile);

                            for (int i = 0; i < parameters.Count; i++)
                            {
                                ApplyValue(testProfile, parameters[i], combo[i]);
                            }

                            //  Backtest futtatás SAFE módon
                            var res = _backtestService.RunBacktest(candles, testProfile);

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
                                    Trades = visualMode ? res.Trades : null
                                };

                                results.Add(optRes);

                                //  Realtime report THREAD-SAFE módon
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

                            //  Progress update
                            int c = System.Threading.Interlocked.Increment(ref current);
                            if (total > 0 && c % Math.Max(1, total / 100) == 0)
                            {
                                progress?.Report((int)((double)c / total * 100));
                            }
                        }
                        catch (Exception ex)
                        {
                            //  HIBÁK LOGOLÁSA (ne nyelje el!)
                            System.Diagnostics.Debug.WriteLine($"Error in optimization iteration: {ex.Message}");
                            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                            // Opcionálisan dobhatod tovább, ha le akarod állítani az egész folyamatot
                            // throw;
                        }
                    });
                }
                catch (OperationCanceledException)
                {
                    //  Normális leállítás - OK
                    System.Diagnostics.Debug.WriteLine("Optimization cancelled by user");
                }
                catch (AggregateException ae)
                {
                    // ""Csak a OperationCanceledException-t engedjük el
                    foreach (var inner in ae.InnerExceptions)
                    {
                        if (inner is OperationCanceledException)
                        {
                            System.Diagnostics.Debug.WriteLine("Optimization cancelled");
                        }
                        else
                        {
                            // MINDEN MÁS HIBÁT LOGOLUNK!
                            System.Diagnostics.Debug.WriteLine($"Optimization error: {inner.Message}");
                            System.Diagnostics.Debug.WriteLine($"Stack trace: {inner.StackTrace}");
                            throw; // Dobjuk tovább!
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Minden egyéb nem várt hiba
                    System.Diagnostics.Debug.WriteLine($"Unexpected error in optimization: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                    throw; // Dobjuk tovább a hívónak!
                }
            }, token); // Adjuk át a token-t a Task.Run-nak is!

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

                        case "LeftShift": ruleToUpdate.LeftShift = value; break;   // Feltételezve, hogy a StrategyRule-ban így hívják
                        case "RightShift": ruleToUpdate.RightShift = value; break; // Feltételezve, hogy a StrategyRule-ban így hívják
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