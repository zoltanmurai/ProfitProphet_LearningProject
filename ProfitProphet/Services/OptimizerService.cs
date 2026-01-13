using System;
using System.Collections.Generic;
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
        private const double K_DD = 0.5; // VBA: Score = PL - K_DD * |DD|

        public OptimizerService(BacktestService backtestService)
        {
            _backtestService = backtestService;
        }

        public async Task<OptimizationResult> OptimizeAsync(List<Candle> candles, StrategyProfile profile, List<OptimizationParameter> parameters)
        {
            int coarseStep = 5;
            OptimizationResult bestResult = null;

            // 1. COARSE SEARCH (Durva pásztázás - VBA: Coarse Search)
            while (coarseStep >= 1 && bestResult == null)
            {
                bestResult = await RunIterationAsync(candles, profile, parameters, coarseStep);
                if (bestResult == null) coarseStep -= 2;
            }

            if (bestResult == null) return null;

            // 2. FINE SEARCH (VBA: Fine Search ±COARSE_STEP körzetben)
            var fineParams = parameters.Select((p, i) => new OptimizationParameter
            {
                Rule = p.Rule,
                IsLeftSide = p.IsLeftSide,
                MinValue = Math.Max(p.MinValue, bestResult.Values[i] - 5),
                MaxValue = Math.Min(p.MaxValue, bestResult.Values[i] + 5)
            }).ToList();

            bestResult = await RunIterationAsync(candles, profile, fineParams, 1);

            // 3. ROBUSTNESS CHECK (VBA logika: Szomszéd paraméterek átlaga)
            // Megnézzük, hogy a talált csúcs környéke (±1) stabil-e.
            var robustness = await CalculateRobustnessAsync(candles, profile, parameters, bestResult);

            bestResult.IsRobust = robustness.IsRobust;
            bestResult.NeighborAvgScore = robustness.AvgScore;

            return bestResult;
        }

        private async Task<(bool IsRobust, double AvgScore)> CalculateRobustnessAsync(List<Candle> candles, StrategyProfile profile, List<OptimizationParameter> parameters, OptimizationResult center)
        {
            // A VBA logika szerint ±1 környezetet nézünk minden dimenzióban
            var neighborParams = parameters.Select((p, i) => new OptimizationParameter
            {
                Rule = p.Rule,
                IsLeftSide = p.IsLeftSide,
                MinValue = center.Values[i] - 1,
                MaxValue = center.Values[i] + 1
            }).ToList();

            // Legyártjuk a szomszédos kombinációkat (3 paraméter esetén ez 3^3 = 27 pont)
            var neighbors = GenerateCombinations(neighborParams, 1);
            double scoreSum = 0;
            int count = 0;

            foreach (var combo in neighbors)
            {
                // A központi pontot kihagyhatjuk az átlagból, ha csak a "környezetet" nézzük, 
                // de a VBA-ban benne maradt a ciklusban, így mi is benne hagyjuk.
                var testProfile = DeepCopyProfile(profile);
                for (int i = 0; i < parameters.Count; i++)
                    ApplyValue(testProfile, parameters[i], combo[i]);

                var res = _backtestService.RunBacktest(candles, testProfile);
                double score = res.TotalProfitLoss - (K_DD * Math.Abs(res.MaxDrawdown));

                // Csak akkor adjuk hozzá, ha érvényes trade-számot produkált (VBA: EvaluateCandidate)
                if (res.TradeCount >= 10)
                {
                    scoreSum += score;
                    count++;
                }
            }

            if (count == 0) return (false, 0);

            double avgScore = scoreSum / count;

            // VBA Logika: ha az átlag legalább a csúcs 60%-a, akkor robusztus
            bool isRobust = avgScore >= center.Score * 0.6;

            return (isRobust, avgScore);
        }

        // ... (A többi metódus: RunIterationAsync, GenerateCombinations, stb. változatlan) ...

        private void ApplyValue(StrategyProfile profile, OptimizationParameter p, int value)
        {
            // Keressük a szabályt a DeepCopy-zott profilban ID alapján
            var rule = profile.EntryGroups.SelectMany(g => g.Rules)
                              .Concat(profile.ExitGroups.SelectMany(g => g.Rules))
                              .FirstOrDefault(r => r.LeftIndicatorName == p.Rule.LeftIndicatorName &&
                                                 r.LeftPeriod == p.Rule.LeftPeriod);
            if (rule != null)
            {
                if (p.IsLeftSide) rule.LeftPeriod = value;
                else rule.RightPeriod = value;
            }
        }

        private StrategyProfile DeepCopyProfile(StrategyProfile original)
        {
            var options = new System.Text.Json.JsonSerializerOptions { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };
            var json = System.Text.Json.JsonSerializer.Serialize(original, options);
            return System.Text.Json.JsonSerializer.Deserialize<StrategyProfile>(json, options);
        }

        private List<int[]> GenerateCombinations(List<OptimizationParameter> parameters, int step)
        {
            var results = new List<int[]>();
            GenerateRecursive(parameters, 0, new int[parameters.Count], results, step);
            return results;
        }

        private void GenerateRecursive(List<OptimizationParameter> parameters, int depth, int[] current, List<int[]> results, int step)
        {
            if (depth == parameters.Count) { results.Add((int[])current.Clone()); return; }
            for (int v = parameters[depth].MinValue; v <= parameters[depth].MaxValue; v += step)
            {
                current[depth] = v;
                GenerateRecursive(parameters, depth + 1, current, results, step);
            }
        }

        private async Task<OptimizationResult> RunIterationAsync(List<Candle> candles, StrategyProfile profile, List<OptimizationParameter> parameters, int step)
        {
            var combinations = GenerateCombinations(parameters, step);
            var results = new System.Collections.Concurrent.ConcurrentBag<OptimizationResult>();

            await Task.Run(() =>
            {
                Parallel.ForEach(combinations, (combo) =>
                {
                    var testProfile = DeepCopyProfile(profile);
                    for (int i = 0; i < parameters.Count; i++)
                        ApplyValue(testProfile, parameters[i], combo[i]);

                    var res = _backtestService.RunBacktest(candles, testProfile);
                    double score = res.TotalProfitLoss - (K_DD * Math.Abs(res.MaxDrawdown));

                    if (res.TradeCount >= 10) // VBA: MIN_TRADES_HARD
                    {
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
    }
}