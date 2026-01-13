using ProfitProphet.Entities;
using ProfitProphet.Models.Backtesting;
using ProfitProphet.Models.Strategies;
using ProfitProphet.Services.Indicators; // Fontos!
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
            int startIndex = 50; // Hagyunk időt az indikátoroknak felépülni

            for (int i = startIndex; i < candles.Count; i++)
            {
                var currentCandle = candles[i];

                // --- VÉTEL ---
                if (!inPosition && EvaluateRules(profile.EntryRules, indicatorCache, candles, i))
                {
                    double price = (double)currentCandle.Close;
                    int quantity = (int)(cash / price);

                    if (quantity > 0)
                    {
                        double cost = quantity * price;
                        double fee = cost * 0.001; // 0.1% jutalék
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
                // --- ELADÁS ---
                else if (inPosition && EvaluateRules(profile.ExitRules, indicatorCache, candles, i))
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
            var allRules = profile.EntryRules.Concat(profile.ExitRules);

            foreach (var rule in allRules)
            {
                // Bal oldal
                EnsureIndicatorCalculated(rule.LeftIndicatorName, rule.LeftPeriod, 0, candles, cache);

                // Jobb oldal (ha indikátor)
                if (rule.RightSourceType == DataSourceType.Indicator)
                {
                    // Itt adjuk át a bal oldal periódusát függőségként!
                    EnsureIndicatorCalculated(rule.RightIndicatorName, rule.RightPeriod, rule.LeftPeriod, candles, cache);
                }
            }
            return cache;
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
                case "SMA":
                    values = IndicatorAlgorithms.CalculateSMA(candles, period);
                    break;
                case "CMF":
                    values = IndicatorAlgorithms.CalculateCMF(candles, period);
                    break;
                case "CLOSE":
                    values = candles.Select(c => (double)c.Close).ToArray();
                    break;
                case "CMF_MA":
                    // 1. Szülő CMF
                    EnsureIndicatorCalculated("CMF", dependencyPeriod, 0, candles, cache);
                    string parentKey = $"CMF_{dependencyPeriod}";

                    // 2. MA a Szülőre
                    if (cache.ContainsKey(parentKey))
                    {
                        values = IndicatorAlgorithms.CalculateSMAOnArray(cache[parentKey], period);
                    }
                    break;
            }

            cache[key] = values;
        }

        private bool EvaluateRules(List<StrategyRule> rules, Dictionary<string, double[]> cache, List<Candle> candles, int index)
        {
            if (rules.Count == 0) return false;

            foreach (var rule in rules)
            {
                // Bal érték
                double leftValue = GetValue(rule.LeftIndicatorName, rule.LeftPeriod, rule.LeftIndicatorName == "Close", cache, candles, index);

                // Jobb érték (Dependency-vel!)
                double rightValue = 0;
                if (rule.RightSourceType == DataSourceType.Value)
                {
                    rightValue = rule.RightValue;
                }
                else
                {
                    rightValue = GetValue(rule.RightIndicatorName, rule.RightPeriod, rule.RightIndicatorName == "Close", cache, candles, index, rule.LeftPeriod);
                }

                bool conditionMet = false;

                // Előző értékek (keresztezéshez)
                double leftPrev = GetValue(rule.LeftIndicatorName, rule.LeftPeriod, false, cache, candles, index - 1);
                double rightPrev = rule.RightSourceType == DataSourceType.Value
                    ? rule.RightValue
                    : GetValue(rule.RightIndicatorName, rule.RightPeriod, false, cache, candles, index - 1, rule.LeftPeriod);

                switch (rule.Operator)
                {
                    case ComparisonOperator.GreaterThan:
                        conditionMet = leftValue > rightValue; break;
                    case ComparisonOperator.LessThan:
                        conditionMet = leftValue < rightValue; break;
                    case ComparisonOperator.Equals:
                        conditionMet = Math.Abs(leftValue - rightValue) < 0.0001; break;
                    case ComparisonOperator.CrossesAbove:
                        conditionMet = (leftValue > rightValue) && (leftPrev <= rightPrev); break;
                    case ComparisonOperator.CrossesBelow:
                        conditionMet = (leftValue < rightValue) && (leftPrev >= rightPrev); break;
                }

                if (!conditionMet) return false;
            }

            return true;
        }

        private double GetValue(string name, int period, bool isPrice, Dictionary<string, double[]> cache, List<Candle> candles, int index, int dependencyPeriod = 0)
        {
            if (index < 0) return 0;
            if (name.ToUpper() == "CLOSE") return (double)candles[index].Close;

            // 1. Próba függőséggel
            if (dependencyPeriod > 0)
            {
                string keyWithDep = $"{name}_{period}_dep{dependencyPeriod}";
                if (cache.ContainsKey(keyWithDep)) return cache[keyWithDep][index];
            }

            // 2. Próba simán
            string keySimple = $"{name}_{period}";
            if (cache.ContainsKey(keySimple)) return cache[keySimple][index];

            return 0;
        }
    }
}