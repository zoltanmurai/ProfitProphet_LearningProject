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
            var result = new BacktestResult
            {
                Symbol = profile.Symbol,
                EquityCurve = new List<EquityPoint>(),
                Trades = new List<TradeRecord>()
            };

            if (candles == null || candles.Count < 50) return result;

            // Indikátorok előszámolása
            var indicatorCache = PrecalculateIndicators(candles, profile);

            double cash = initialCash;

            int holdings = 0;           // Jelenlegi részvény darabszám
            double avgEntryPrice = 0;   // Súlyozott átlagár

            // Statisztikai változók a Drawdown-hoz
            double peakEquity = initialCash;
            double maxDrawdown = 0;

            // Kezdőpont rögzítése
            result.EquityCurve.Add(new EquityPoint { Time = candles[0].TimestampUtc, Equity = initialCash });
            result.BalanceCurve.Add(new EquityPoint { Time = candles[0].TimestampUtc, Equity = initialCash });

            // FUTTATÁS
            int startIndex = 50;

            for (int i = startIndex; i < candles.Count; i++)
            {
                var currentCandle = candles[i];
                double price = (double)currentCandle.Close;

                // -----------------------------
                // 1. VÉTELI LOGIKA (ENTRY)
                // -----------------------------
                bool canBuy = (holdings == 0) || profile.AllowPyramiding;

                // Fontos: Az "else if" miatt a te logikád szerint vagy veszünk, vagy eladunk egy körben.
                // Ha a vételi feltétel igaz, akkor belépünk (és nem vizsgáljuk az eladást).
                if (canBuy && EvaluateGroups(profile.EntryGroups, indicatorCache, candles, i))
                {
                    int quantityToBuy = CalculatePositionSize(profile, cash, price);

                    if (quantityToBuy > 0)
                    {
                        double tradeValue = quantityToBuy * price;
                        double fee = CalculateFee(tradeValue, profile);

                        if (cash >= tradeValue + fee)
                        {
                            // Átlagár frissítése
                            double totalValueBefore = holdings * avgEntryPrice;
                            double totalValueNew = tradeValue;
                            avgEntryPrice = (totalValueBefore + totalValueNew) / (holdings + quantityToBuy);

                            // Tranzakció
                            cash -= (tradeValue + fee);
                            holdings += quantityToBuy;

                            result.Trades.Add(new TradeRecord
                            {
                                EntryDate = currentCandle.TimestampUtc,
                                EntryPrice = (decimal)price,
                                Quantity = quantityToBuy,
                                Type = "Long"
                            });
                        }
                    }
                }
                // -----------------------------
                // 2. ELADÁSI LOGIKA (EXIT)
                // -----------------------------
                else if (holdings > 0 && EvaluateGroups(profile.ExitGroups, indicatorCache, candles, i))
                {
                    bool shouldSell = true;

                    if (profile.OnlySellInProfit)
                    {
                        double potentialRevenue = holdings * price;
                        double potentialFee = CalculateFee(potentialRevenue, profile);
                        double netRevenue = potentialRevenue - potentialFee;

                        // Ha a nettó bevétel kisebb, mint amennyibe került (holdings * átlagár), nem adjuk el
                        if (netRevenue <= (holdings * avgEntryPrice))
                        {
                            shouldSell = false;
                        }
                    }

                    if (shouldSell)
                    {
                        double revenue = holdings * price;
                        double fee = CalculateFee(revenue, profile);

                        cash += (revenue - fee);

                        // Nyitott trade-ek lezárása a listában
                        foreach (var trade in result.Trades.Where(t => t.ExitDate == DateTime.MinValue))
                        {
                            trade.ExitDate = currentCandle.TimestampUtc;
                            trade.ExitPrice = (decimal)price;

                            // Egyszerűsített profit számítás a trade-re
                            double grossProfit = (double)(trade.ExitPrice - trade.EntryPrice) * trade.Quantity;
                            trade.Profit = (decimal)grossProfit;
                        }

                        holdings = 0;
                        avgEntryPrice = 0;
                    }
                }

                // -----------------------------
                // 3. EQUITY ÉS DRAWDOWN FRISSÍTÉS (MINDEN GYERTYÁNÁL!)
                // -----------------------------
                // Minden gyertyánál kiszámoljuk, mennyit ér (nem realizált)
                double currentEquity = cash + (holdings * price);

                // BALANCE (Realizált): Cash + (Darab * BEKERÜLÉSI Ár)
                double currentBalance = cash + (holdings * avgEntryPrice);

                // Drawdown (Visszaesés) számítása
                if (currentEquity > peakEquity)
                {
                    peakEquity = currentEquity; // Új csúcs
                }
                else
                {
                    double dd = (peakEquity - currentEquity) / peakEquity;
                    if (dd > maxDrawdown) maxDrawdown = dd;
                }

                // Görbe pont hozzáadása
                result.EquityCurve.Add(new EquityPoint
                {
                    Time = currentCandle.TimestampUtc,
                    Equity = currentEquity
                });
                //Balance mentése
                result.BalanceCurve.Add(new EquityPoint
                {
                    Time = currentCandle.TimestampUtc,
                    Equity = currentBalance // Itt a Balance értéket mentjük Equity néven a pontba
                });
            }

            // Kényszerített zárás a végén a pontos végeredményhez
            if (holdings > 0)
            {
                double price = (double)candles.Last().Close;
                double revenue = holdings * price;
                double fee = CalculateFee(revenue, profile);
                cash += (revenue - fee);
            }

            result.TotalProfitLoss = cash - initialCash;
            result.MaxDrawdown = maxDrawdown; // Itt mentjük el a maximumot a táblázathoz
            result.TradeCount = result.Trades.Count;

            var closedTrades = result.Trades.Where(t => t.ExitDate != DateTime.MinValue).ToList();
            result.WinRate = closedTrades.Count > 0
                ? (double)closedTrades.Count(t => t.ExitPrice > t.EntryPrice) / closedTrades.Count
                : 0;

            return result;
        }

        // --- SEGÉDMETÓDUSOK (Ezek változatlanul maradhatnak, de a teljesség kedvéért itt vannak) ---

        private double CalculateFee(double tradeValue, StrategyProfile profile)
        {
            double calculatedFee = tradeValue * (profile.CommissionPercent / 100.0);
            return Math.Max(calculatedFee, profile.MinCommission);
        }

        private int CalculatePositionSize(StrategyProfile profile, double currentCash, double price)
        {
            if (price <= 0) return 0;
            double amountToInvest = 0;

            switch (profile.AmountType)
            {
                case TradeAmountType.AllCash:
                    amountToInvest = currentCash * 0.98;
                    break;
                case TradeAmountType.FixedCash:
                    amountToInvest = profile.TradeAmount;
                    if (amountToInvest > currentCash) amountToInvest = currentCash;
                    break;
                case TradeAmountType.FixedShareCount:
                    double requiredCash = profile.TradeAmount * price;
                    if (requiredCash > currentCash) return (int)(currentCash / price);
                    return (int)profile.TradeAmount;
                case TradeAmountType.PercentageOfEquity:
                    double percent = Math.Max(0, Math.Min(100, profile.TradeAmount));
                    amountToInvest = currentCash * (percent / 100.0);
                    break;
            }
            return (int)(amountToInvest / price);
        }

        private Dictionary<string, double[]> PrecalculateIndicators(List<Candle> candles, StrategyProfile profile)
        {
            var cache = new Dictionary<string, double[]>();
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

        private bool EvaluateGroups(List<StrategyGroup> groups, Dictionary<string, double[]> cache, List<Candle> candles, int index)
        {
            if (groups == null || groups.Count == 0) return false;
            foreach (var group in groups)
            {
                if (group.Rules.Count == 0) continue;
                bool isGroupValid = true;
                foreach (var rule in group.Rules)
                {
                    if (!EvaluateSingleRule(rule, cache, candles, index))
                    {
                        isGroupValid = false;
                        break;
                    }
                }
                if (isGroupValid) return true;
            }
            return false;
        }

        private bool EvaluateSingleRule(StrategyRule rule, Dictionary<string, double[]> cache, List<Candle> candles, int index)
        {
            double leftValue = GetValue(rule.LeftIndicatorName, rule.LeftPeriod, rule.LeftIndicatorName == "Close", cache, candles, index);
            double rightValue = rule.RightSourceType == DataSourceType.Value
                ? rule.RightValue
                : GetValue(rule.RightIndicatorName, rule.RightPeriod, rule.RightIndicatorName == "Close", cache, candles, index, rule.LeftPeriod);

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
            string key = dependencyPeriod > 0 ? $"{name}_{period}_dep{dependencyPeriod}" : $"{name}_{period}";
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
            if (isPrice || name.ToUpper() == "CLOSE" || name.ToUpper() == "OPEN" || name.ToUpper() == "HIGH" || name.ToUpper() == "LOW")
            {
                int targetIndex = index - period;
                if (targetIndex < 0) return 0;
                switch (name.ToUpper())
                {
                    case "OPEN": return (double)candles[targetIndex].Open;
                    case "HIGH": return (double)candles[targetIndex].High;
                    case "LOW": return (double)candles[targetIndex].Low;
                    default: return (double)candles[targetIndex].Close;
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