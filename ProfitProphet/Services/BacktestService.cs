using ProfitProphet.Entities;
using ProfitProphet.Models.Backtesting;
using ProfitProphet.Models.Strategies;
using ProfitProphet.Services.Indicators;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProfitProphet.Services
{
    public class BacktestService
    {
        public BacktestResult RunBacktest(List<Candle> candles, StrategyProfile profile, double initialCash = 10000)
        {
            var result = new BacktestResult { Symbol = profile.Symbol };
            if (candles == null || candles.Count < 50) return result;

            // 1. ADATOK ELŐKÉSZÍTÉSE
            var indicatorCache = PrecalculateIndicators(candles, profile);

            double cash = initialCash;
            int holdings = 0;
            bool inPosition = false;

            result.EquityCurve.Add(new EquityPoint { Time = candles[0].TimestampUtc, Equity = cash });

            // 2. FUTTATÁS
            int startIndex = 50;

            for (int i = startIndex; i < candles.Count; i++)
            {
                var currentCandle = candles[i];

                // --- VÉTEL (Bármelyik Entry Csoport igaz?) ---
                if (!inPosition && EvaluateGroups(profile.EntryGroups, indicatorCache, candles, i))
                {
                    double price = (double)currentCandle.Close;
                    int quantity = (int)(cash / price);

                    if (quantity > 0)
                    {
                        double cost = quantity * price;
                        double fee = cost * 0.001;
                        cash -= (cost + fee);
                        holdings = quantity;
                        inPosition = true;

                        result.Trades.Add(new TradeRecord
                        {
                            EntryDate = currentCandle.TimestampUtc,
                            EntryPrice = (decimal)price,
                            Type = "Long"
                        });
                    }
                }
                // --- ELADÁS (Bármelyik Exit Csoport igaz?) ---
                else if (inPosition && EvaluateGroups(profile.ExitGroups, indicatorCache, candles, i))
                {
                    double price = (double)currentCandle.Close;
                    double revenue = holdings * price;
                    double fee = revenue * 0.001;
                    cash += (revenue - fee);

                    var lastTrade = result.Trades.Last();
                    lastTrade.ExitDate = currentCandle.TimestampUtc;
                    lastTrade.ExitPrice = (decimal)price;
                    lastTrade.Profit = (decimal)((revenue - fee) - ((double)lastTrade.EntryPrice * holdings));

                    holdings = 0;
                    inPosition = false;

                    result.EquityCurve.Add(new EquityPoint { Time = currentCandle.TimestampUtc, Equity = cash });
                }
            }

            // Zárás a végén
            if (inPosition)
            {
                double price = (double)candles.Last().Close;
                cash += holdings * price;
                var lastTrade = result.Trades.Last();
                lastTrade.ExitDate = candles.Last().TimestampUtc;
                lastTrade.ExitPrice = (decimal)price;
            }

            result.TotalProfitLoss = cash - initialCash;
            result.TradeCount = result.Trades.Count;
            result.WinRate = result.Trades.Count > 0
                ? (double)result.Trades.Count(t => t.Profit > 0) / result.Trades.Count
                : 0;

            return result;
        }

        // --- SEGÉDMETÓDUSOK ---

        private Dictionary<string, double[]> PrecalculateIndicators(List<Candle> candles, StrategyProfile profile)
        {
            var cache = new Dictionary<string, double[]>();

            // Itt most már a Csoportokon belül keressük a szabályokat (.SelectMany)
            var allEntryRules = profile.EntryGroups.SelectMany(g => g.Rules);
            var allExitRules = profile.ExitGroups.SelectMany(g => g.Rules);
            var allRules = allEntryRules.Concat(allExitRules);

            foreach (var rule in allRules)
            {
                EnsureIndicatorCalculated(rule.LeftIndicatorName, rule.LeftPeriod, 0, candles, cache);
                if (rule.RightSourceType == DataSourceType.Indicator)
                {
                    EnsureIndicatorCalculated(rule.RightIndicatorName, rule.RightPeriod, rule.LeftPeriod, candles, cache);
                }
            }
            return cache;
        }

        // --- A FŐ LOGIKA (DNF Kiértékelés) ---
        private bool EvaluateGroups(List<StrategyGroup> groups, Dictionary<string, double[]> cache, List<Candle> candles, int index)
        {
            if (groups == null || groups.Count == 0) return false;

            // 1. Szint: VAGY kapcsolat (Bármelyik csoport teljesül, az jó)
            foreach (var group in groups)
            {
                if (group.Rules.Count == 0) continue;

                bool isGroupValid = true;

                // 2. Szint: ÉS kapcsolat (A csoporton belül mindennek igaznak kell lennie)
                foreach (var rule in group.Rules)
                {
                    if (!EvaluateSingleRule(rule, cache, candles, index))
                    {
                        isGroupValid = false;
                        break; // Bukott a csoport, nem kell tovább nézni a szabályait
                    }
                }

                // Ha a csoport "túlélte" a vizsgálatot (minden szabálya igaz), akkor BINGO!
                if (isGroupValid) return true;
            }

            // Ha végigértünk az összes csoporton és egyik sem nyert
            return false;
        }

        private bool EvaluateSingleRule(StrategyRule rule, Dictionary<string, double[]> cache, List<Candle> candles, int index)
        {
            double leftValue = GetValue(rule.LeftIndicatorName, rule.LeftPeriod, rule.LeftIndicatorName == "Close", cache, candles, index);

            double rightValue = 0;
            if (rule.RightSourceType == DataSourceType.Value)
            {
                rightValue = rule.RightValue;
            }
            else
            {
                rightValue = GetValue(rule.RightIndicatorName, rule.RightPeriod, rule.RightIndicatorName == "Close", cache, candles, index, rule.LeftPeriod);
            }

            double leftPrev = GetValue(rule.LeftIndicatorName, rule.LeftPeriod, false, cache, candles, index - 1);
            double rightPrev = rule.RightSourceType == DataSourceType.Value
                ? rule.RightValue
                : GetValue(rule.RightIndicatorName, rule.RightPeriod, false, cache, candles, index - 1, rule.LeftPeriod);

            switch (rule.Operator)
            {
                case ComparisonOperator.GreaterThan: return leftValue > rightValue;
                case ComparisonOperator.LessThan: return leftValue < rightValue;
                case ComparisonOperator.Equals: return Math.Abs(leftValue - rightValue) < 0.0001;
                case ComparisonOperator.CrossesAbove: return (leftValue > rightValue) && (leftPrev <= rightPrev);
                case ComparisonOperator.CrossesBelow: return (leftValue < rightValue) && (leftPrev >= rightPrev);
                default: return false;
            }
        }

        private void EnsureIndicatorCalculated(string name, int period, int dependencyPeriod, List<Candle> candles, Dictionary<string, double[]> cache)
        {
            string key = dependencyPeriod > 0
                ? $"{name}_{period}_dep{dependencyPeriod}"
                : $"{name}_{period}";

            if (cache.ContainsKey(key)) return;

            double[] values = new double[candles.Count];

            switch (name.ToUpper())
            {
                case "SMA": values = IndicatorAlgorithms.CalculateSMA(candles, period); break;
                case "EMA": values = IndicatorAlgorithms.CalculateEMA(candles, period); break;
                case "CMF": values = IndicatorAlgorithms.CalculateCMF(candles, period); break;
                case "CLOSE": values = candles.Select(c => (double)c.Close).ToArray(); break;
                case "CMF_MA":
                    EnsureIndicatorCalculated("CMF", dependencyPeriod, 0, candles, cache);
                    string parentKey = $"CMF_{dependencyPeriod}";
                    if (cache.ContainsKey(parentKey))
                        values = IndicatorAlgorithms.CalculateSMAOnArray(cache[parentKey], period);
                    break;
            }
            cache[key] = values;
        }

        private double GetValue(string name, int period, bool isPrice, Dictionary<string, double[]> cache, List<Candle> candles, int index, int dependencyPeriod = 0)
        {
            if (index < 0) return 0;
            //if (name.ToUpper() == "CLOSE") return (double)candles[index].Close;

            if (isPrice || name.ToUpper() == "CLOSE" || name.ToUpper() == "OPEN" || name.ToUpper() == "HIGH" || name.ToUpper() == "LOW")
            {
                int targetIndex = index - period; // Itt történik az eltolás (pl. index - 1)

                // Védelem: Ha a chart eleje előtt vagyunk, 0-t adunk vissza
                if (targetIndex < 0) return 0;

                switch (name.ToUpper())
                {
                    case "OPEN": return (double)candles[targetIndex].Open;
                    case "HIGH": return (double)candles[targetIndex].High;
                    case "LOW": return (double)candles[targetIndex].Low;
                    default: return (double)candles[targetIndex].Close; // Alapértelmezés a Close
                }
            }

            if (dependencyPeriod > 0)
            {
                string keyWithDep = $"{name}_{period}_dep{dependencyPeriod}";
                if (cache.ContainsKey(keyWithDep)) return cache[keyWithDep][index];
            }

            string keySimple = $"{name}_{period}";
            if (cache.ContainsKey(keySimple)) return cache[keySimple][index];

            return 0;
        }
    }
}