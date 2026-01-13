using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ProfitProphet.Entities;
using ProfitProphet.Models.Strategies;

namespace ProfitProphet.Services
{
    public class OptimizerService
    {
        private readonly BacktestService _backtestService;
        private const double K_DD = 0.5;

        public OptimizerService(BacktestService backtestService)
        {
            _backtestService = backtestService;
        }

        public async Task<OptimizationResult> OptimizeAsync(List<Candle> candles, StrategyProfile profile, List<OptimizationParameter> parameters)
        {
            int coarseStep = 5;
            OptimizationResult bestCoarse = null;

            // --- 1. COARSE SEARCH ---
            // Addig csökkentjük a lépésközt, amíg nem találunk érvényes eredményt (mint a VBA Do While-ban)
            while (coarseStep >= 1 && bestCoarse == null)
            {
                bestCoarse = await RunSearch(candles, profile, parameters, coarseStep);
                if (bestCoarse == null) coarseStep -= 2;
            }

            if (bestCoarse == null) return null;

            // --- 2. FINE SEARCH (±CoarseStep tartományban, 1-es lépésközzel) ---
            var fineParameters = parameters.Select((p, i) => new OptimizationParameter
            {
                Rule = p.Rule,
                IsLeftSide = p.IsLeftSide,
                MinValue = Math.Max(p.MinValue, bestCoarse.Values[i] - 5),
                MaxValue = Math.Min(p.MaxValue, bestCoarse.Values[i] + 5)
            }).ToList();

            var bestFine = await RunSearch(candles, profile, fineParameters, 1);

            // --- 3. ROBUSTNESS CHECK ---
            // Megnézzük a szomszédokat (távolság: 1)
            double robustnessScore = await CalculateRobustness(candles, profile, parameters, bestFine);

            if (robustnessScore < bestFine.Score * 0.6)
            {
                System.Diagnostics.Debug.WriteLine("FIGYELEM: Az optimum nem robusztus (Overfitting gyanú)!");
            }

            return bestFine;
        }

        private async Task<OptimizationResult> RunSearch(List<Candle> candles, StrategyProfile profile, List<OptimizationParameter> parameters, int step)
        {
            // Legyártjuk az összes kombinációt
            var combinations = GenerateCombinations(parameters, step);
            var results = new System.Collections.Concurrent.ConcurrentBag<OptimizationResult>();

            // Párhuzamos futtatás az összes magon!
            await Task.Run(() =>
            {
                Parallel.ForEach(combinations, (combo) =>
                {
                    var tempProfile = DeepCopy(profile);
                    // Beállítjuk az aktuális kombináció értékeit
                    for (int i = 0; i < parameters.Count; i++)
                    {
                        var p = parameters[i];
                        // Megkeressük ugyanazt a szabályt a másolt profilban (index alapján)
                        // (Egyszerűsítve: feltesszük a sorrend azonos)
                        ApplyValue(tempProfile, p, i, combo[i]);
                    }

                    var res = _backtestService.RunBacktest(candles, tempProfile);

                    // Zoli Score matekja:
                    double score = res.TotalProfitLoss - (K_DD * Math.Abs(res.MaxDrawdown));

                    // VBA-s feltétel: MIN_TRADES szűrő
                    int minTradesHard = 10;
                    if (res.TradeCount >= minTradesHard)
                    {
                        results.Add(new OptimizationResult
                        {
                            Values = combo,
                            Score = score,
                            Profit = res.TotalProfitLoss,
                            MaxDrawdown = res.MaxDrawdown,
                            TradeCount = res.TradeCount
                        });
                    }
                });
            });

            return results.OrderByDescending(r => r.Score).FirstOrDefault();
        }

        // Segédmetódus a kombinációkhoz (rekurzív)
        private List<int[]> GenerateCombinations(List<OptimizationParameter> parameters, int step)
        {
            var combos = new List<int[]>();
            AddLevel(parameters, 0, new int[parameters.Count], combos, step);
            return combos;
        }

        private void AddLevel(List<OptimizationParameter> paramsList, int index, int[] current, List<int[]> result, int step)
        {
            if (index == paramsList.Count)
            {
                result.Add((int[])current.Clone());
                return;
            }

            for (int v = paramsList[index].MinValue; v <= paramsList[index].MaxValue; v += step)
            {
                current[index] = v;
                AddLevel(paramsList, index + 1, current, result, step);
            }
        }

        private async Task<double> CalculateRobustness(List<Candle> candles, StrategyProfile profile, List<OptimizationParameter> parameters, OptimizationResult center)
        {
            // Egy egyszerűsített szomszédság-átlag (±1 minden paraméternél)
            // Itt a VBA logika szerinti átlagolás jön...
            return center.Score; // Placeholder
        }

        private void ApplyValue(StrategyProfile profile, OptimizationParameter p, int paramIndex, int value)
        {
            // Ez a metódus megkeresi a szabályt és beállítja a periódust
            // (A DeepCopy miatt a profil teljesen új objektum)
            // Implementáció: Rule ID vagy index alapján...
        }

        private StrategyProfile DeepCopy(StrategyProfile original)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(original);
            return System.Text.Json.JsonSerializer.Deserialize<StrategyProfile>(json);
        }
    }
}