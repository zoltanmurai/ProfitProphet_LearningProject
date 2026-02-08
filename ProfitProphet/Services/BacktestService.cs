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
                BalanceCurve = new List<EquityPoint>(),
                Trades = new List<TradeRecord>()
            };

            if (candles == null || candles.Count < 50) return result;

            var indicatorCache = PrecalculateIndicators(candles, profile);

            double cash = initialCash;
            int holdings = 0;

            // Kétféle átlagárat tartunk nyilván:
            double avgEntryPriceRaw = 0;      // Tiszta árfolyam (a Balance számításhoz)
            double avgBreakEvenPrice = 0;     // Árfolyam + Vételi Jutalék (a Profit logikához)

            double peakEquity = initialCash;
            double maxDrawdown = 0;

            result.EquityCurve.Add(new EquityPoint { Time = candles[0].TimestampUtc, Equity = cash });
            result.BalanceCurve.Add(new EquityPoint { Time = candles[0].TimestampUtc, Equity = cash });

            int startIndex = 50;

            for (int i = startIndex; i < candles.Count; i++)
            {
                var currentCandle = candles[i];
                double price = (double)currentCandle.Close;

                // --- 1. VÉTEL ---
                bool canBuy = (holdings == 0) || profile.AllowPyramiding;

                if (canBuy && EvaluateGroups(profile.EntryGroups, indicatorCache, candles, i))
                {
                    int quantityToBuy = CalculatePositionSize(profile, cash, price);

                    if (quantityToBuy > 0)
                    {
                        double tradeValue = quantityToBuy * price;
                        double fee = CalculateFee(tradeValue, profile);

                        if (cash >= tradeValue + fee)
                        {
                            // A) Tiszta átlagár frissítése (Balance-hoz)
                            double totalValueRaw = (holdings * avgEntryPriceRaw) + tradeValue;
                            avgEntryPriceRaw = totalValueRaw / (holdings + quantityToBuy);

                            // B) Fedezeti ár frissítése (Profit Logikához) - Itt hozzáadjuk a vételi díjat is!
                            double totalCostWithFee = (holdings * avgBreakEvenPrice) + (tradeValue + fee);
                            avgBreakEvenPrice = totalCostWithFee / (holdings + quantityToBuy);

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
                // --- 2. ELADÁS ---
                else if (holdings > 0 && EvaluateGroups(profile.ExitGroups, indicatorCache, candles, i))
                {
                    bool shouldSell = true;

                    if (profile.OnlySellInProfit)
                    {
                        double revenue = holdings * price;
                        double sellFee = CalculateFee(revenue, profile);

                        // JAVÍTOTT LOGIKA: 
                        // A bevételnek (mínusz eladási díj) nagyobbnak kell lennie, mint a teljes bekerülési költség (vételi díjjal együtt)
                        if ((revenue - sellFee) <= (holdings * avgBreakEvenPrice))
                        {
                            shouldSell = false; // Még nem vagyunk valódi pluszban
                        }
                    }

                    if (shouldSell)
                    {
                        double revenue = holdings * price;
                        double fee = CalculateFee(revenue, profile);

                        cash += (revenue - fee);

                        foreach (var trade in result.Trades.Where(t => t.ExitDate == DateTime.MinValue))
                        {
                            trade.ExitDate = currentCandle.TimestampUtc;
                            trade.ExitPrice = (decimal)price;
                            // Profit = (Eladási ár - Vételi ár) * db
                            // Megjegyzés: Ez bruttó profit a trade listában, a díjakat a cash kezeli pontosan
                            trade.Profit = (decimal)((double)(trade.ExitPrice - trade.EntryPrice) * trade.Quantity);
                        }

                        holdings = 0;
                        avgEntryPriceRaw = 0;
                        avgBreakEvenPrice = 0;
                    }
                }

                // --- 3. GÖRBÉK ---
                double currentEquity = cash + (holdings * price);

                // Balance: A "lekötött" pénzt a tiszta bekerülési áron számoljuk
                // Ez így vízszintes marad, amíg nem adunk el (csak a vételi jutalék miatt ugrik egy picit lejjebb vételkor)
                double currentBalance = cash + (holdings * avgEntryPriceRaw);

                if (currentEquity > peakEquity) peakEquity = currentEquity;
                else
                {
                    double dd = (peakEquity - currentEquity) / peakEquity;
                    if (dd > maxDrawdown) maxDrawdown = dd;
                }

                result.TotalProfitLoss = cash - initialCash;
                result.MaxDrawdown = maxDrawdown;
                result.TradeCount = result.Trades.Count;
                result.EquityCurve.Add(new EquityPoint { Time = currentCandle.TimestampUtc, Equity = currentEquity });
                result.BalanceCurve.Add(new EquityPoint { Time = currentCandle.TimestampUtc, Equity = currentBalance });
            }

            // Kényszerített zárás a végén
            //if (holdings > 0)
            //{
            //    double price = (double)candles.Last().Close;
            //    double revenue = holdings * price;
            //    double fee = CalculateFee(revenue, profile);
            //    cash += (revenue - fee);
            //}

            //result.TotalProfitLoss = cash - initialCash;
            //result.MaxDrawdown = maxDrawdown;
            //result.TradeCount = result.Trades.Count;

            //var closedTrades = result.Trades.Where(t => t.ExitDate != DateTime.MinValue).ToList();

            //double grossProfit = closedTrades.Where(t => t.Profit > 0).Sum(t => (double)t.Profit);
            //double grossLoss = closedTrades.Where(t => t.Profit < 0).Sum(t => Math.Abs((double)t.Profit));

            //result.ProfitFactor = grossLoss == 0 ? grossProfit : grossProfit / grossLoss;

            //result.WinRate = closedTrades.Count > 0
            //    ? (double)closedTrades.Count(t => t.ExitPrice > t.EntryPrice) / closedTrades.Count
            //    : 0;

            //return result;
            // Kényszerített zárás a végén (ha maradt részvényünk)
            if (holdings > 0)
            {
                double lastPrice = (double)candles.Last().Close;
                double revenue = holdings * lastPrice;
                double fee = CalculateFee(revenue, profile);
                cash += (revenue - fee);

                // JAVÍTÁS: A Trade listában is lezárjuk a nyitott pozíciókat!
                foreach (var trade in result.Trades.Where(t => t.ExitDate == DateTime.MinValue))
                {
                    trade.ExitDate = candles.Last().TimestampUtc;
                    trade.ExitPrice = (decimal)lastPrice;
                    // Profit számítása
                    trade.Profit = (decimal)((double)(trade.ExitPrice - trade.EntryPrice) * trade.Quantity);
                }
            }

            result.TotalProfitLoss = cash - initialCash;
            result.MaxDrawdown = maxDrawdown;
            result.TradeCount = result.Trades.Count;

            // --- PROFIT FAKTOR SZÁMÍTÁS (JAVÍTOTT) ---
            // Csak a lezárt trade-eket nézzük (de a fenti javítás miatt most már mind le van zárva)
            var closedTrades = result.Trades.Where(t => t.ExitDate != DateTime.MinValue).ToList();

            double grossProfit = closedTrades.Where(t => t.Profit > 0).Sum(t => (double)t.Profit);
            double grossLoss = closedTrades.Where(t => t.Profit < 0).Sum(t => Math.Abs((double)t.Profit));

            if (grossLoss == 0)
            {
                // Ha nincs veszteség, a Profit Faktor végtelen lenne. 
                // Ilyenkor 0-t adunk vissza, ha profit sincs, vagy 100-at (mint "tökéletes"), ha van profit.
                result.ProfitFactor = grossProfit > 0 ? 100 : 0;
            }
            else
            {
                result.ProfitFactor = grossProfit / grossLoss;
            }

            // WinRate
            result.WinRate = closedTrades.Count > 0
                ? (double)closedTrades.Count(t => t.ExitPrice > t.EntryPrice) / closedTrades.Count
                : 0;

            return result;
        }

        // --- SEGÉDMETÓDUSOK (Változatlanok) ---
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
                case TradeAmountType.AllCash: amountToInvest = currentCash * 0.98; break;
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
            var allRules = profile.EntryGroups.SelectMany(g => g.Rules).Concat(profile.ExitGroups.SelectMany(g => g.Rules));
            foreach (var rule in allRules)
            {
                EnsureIndicatorCalculated(rule.LeftIndicatorName, rule.LeftPeriod, 0, candles, cache);
                if (rule.RightSourceType == DataSourceType.Indicator) EnsureIndicatorCalculated(rule.RightIndicatorName, rule.RightPeriod, rule.LeftPeriod, candles, cache);
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
                foreach (var rule in group.Rules) { if (!EvaluateSingleRule(rule, cache, candles, index)) { isGroupValid = false; break; } }
                if (isGroupValid) return true;
            }
            return false;
        }

        private bool EvaluateSingleRule(StrategyRule rule, Dictionary<string, double[]> cache, List<Candle> candles, int index)
        {
            double leftValue = GetValue(rule.LeftIndicatorName, rule.LeftPeriod, rule.LeftIndicatorName == "Close", cache, candles, index);
            double rightValue = rule.RightSourceType == DataSourceType.Value ? rule.RightValue : GetValue(rule.RightIndicatorName, rule.RightPeriod, rule.RightIndicatorName == "Close", cache, candles, index, rule.LeftPeriod);
            double leftPrev = GetValue(rule.LeftIndicatorName, rule.LeftPeriod, false, cache, candles, index - 1);
            double rightPrev = rule.RightSourceType == DataSourceType.Value ? rule.RightValue : GetValue(rule.RightIndicatorName, rule.RightPeriod, false, cache, candles, index - 1, rule.LeftPeriod);

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
                case "CMF_MA": EnsureIndicatorCalculated("CMF", dependencyPeriod, 0, candles, cache); string parentKey = $"CMF_{dependencyPeriod}"; if (cache.ContainsKey(parentKey)) values = IndicatorAlgorithms.CalculateSMAOnArray(cache[parentKey], period); break;
            }
            cache[key] = values;
        }

        private double GetValue(string name, int period, bool isPrice, Dictionary<string, double[]> cache, List<Candle> candles, int index, int dependencyPeriod = 0)
        {
            if (index < 0) return 0;
            if (isPrice || name.ToUpper() == "CLOSE" || name.ToUpper() == "OPEN" || name.ToUpper() == "HIGH" || name.ToUpper() == "LOW")
            {
                int targetIndex = index - period; if (targetIndex < 0) return 0;
                switch (name.ToUpper()) { case "OPEN": return (double)candles[targetIndex].Open; case "HIGH": return (double)candles[targetIndex].High; case "LOW": return (double)candles[targetIndex].Low; default: return (double)candles[targetIndex].Close; }
            }
            if (dependencyPeriod > 0) { string keyWithDep = $"{name}_{period}_dep{dependencyPeriod}"; if (cache.ContainsKey(keyWithDep)) return cache[keyWithDep][index]; }
            string keySimple = $"{name}_{period}"; if (cache.ContainsKey(keySimple)) return cache[keySimple][index];
            return 0;
        }
    }
}