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

            // Indikátorok előszámítása (Cache feltöltése)
            var indicatorCache = PrecalculateIndicators(candles, profile);

            double cash = initialCash;
            int holdings = 0;

            double avgEntryPriceRaw = 0;
            double avgBreakEvenPrice = 0;

            double peakEquity = initialCash;
            double maxDrawdown = 0;

            result.EquityCurve.Add(new EquityPoint { Time = candles[0].TimestampUtc, Equity = cash });
            result.BalanceCurve.Add(new EquityPoint { Time = candles[0].TimestampUtc, Equity = cash });

            // Hagyunk időt az indikátoroknak (pl. EMA 50 vagy MACD Slow 26)
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
                            double totalValueRaw = (holdings * avgEntryPriceRaw) + tradeValue;
                            avgEntryPriceRaw = totalValueRaw / (holdings + quantityToBuy);

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
                        if ((revenue - sellFee) <= (holdings * avgBreakEvenPrice))
                        {
                            shouldSell = false;
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
                            trade.Profit = (decimal)((double)(trade.ExitPrice - trade.EntryPrice) * trade.Quantity);
                        }

                        holdings = 0;
                        avgEntryPriceRaw = 0;
                        avgBreakEvenPrice = 0;
                    }
                }

                // --- 3. GÖRBÉK FRISSÍTÉSE ---
                double currentEquity = cash + (holdings * price);
                double currentBalance = cash + (holdings * avgEntryPriceRaw);

                if (currentEquity > peakEquity) peakEquity = currentEquity;
                else
                {
                    double dd = (peakEquity - currentEquity) / peakEquity;
                    if (dd > maxDrawdown) maxDrawdown = dd;
                }

                result.EquityCurve.Add(new EquityPoint { Time = currentCandle.TimestampUtc, Equity = currentEquity });
                result.BalanceCurve.Add(new EquityPoint { Time = currentCandle.TimestampUtc, Equity = currentBalance });
            }

            // Kényszerített zárás a végén
            if (holdings > 0)
            {
                double lastPrice = (double)candles.Last().Close;
                double revenue = holdings * lastPrice;
                double fee = CalculateFee(revenue, profile);
                cash += (revenue - fee);

                foreach (var trade in result.Trades.Where(t => t.ExitDate == DateTime.MinValue))
                {
                    trade.ExitDate = candles.Last().TimestampUtc;
                    trade.ExitPrice = (decimal)lastPrice;
                    trade.Profit = (decimal)((double)(trade.ExitPrice - trade.EntryPrice) * trade.Quantity);
                }
            }

            result.TotalProfitLoss = cash - initialCash;
            result.MaxDrawdown = maxDrawdown;
            result.TradeCount = result.Trades.Count;

            var closedTrades = result.Trades.Where(t => t.ExitDate != DateTime.MinValue).ToList();
            double grossProfit = closedTrades.Where(t => t.Profit > 0).Sum(t => (double)t.Profit);
            double grossLoss = closedTrades.Where(t => t.Profit < 0).Sum(t => Math.Abs((double)t.Profit));

            if (grossLoss == 0) result.ProfitFactor = grossProfit > 0 ? 100 : 0;
            else result.ProfitFactor = grossProfit / grossLoss;

            result.WinRate = closedTrades.Count > 0
                ? (double)closedTrades.Count(t => t.ExitPrice > t.EntryPrice) / closedTrades.Count
                : 0;

            return result;
        }

        // --- SEGÉDMETÓDUSOK ---

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

        // --- JAVÍTOTT PRECALCULATE (ÁTADJA AZ ÖSSZES PARAMÉTERT) ---
        private Dictionary<string, double[]> PrecalculateIndicators(List<Candle> candles, StrategyProfile profile)
        {
            var cache = new Dictionary<string, double[]>();
            var allRules = profile.EntryGroups.SelectMany(g => g.Rules).Concat(profile.ExitGroups.SelectMany(g => g.Rules));

            foreach (var rule in allRules)
            {
                // Bal oldal: p1, p2, p3
                EnsureIndicatorCalculated(rule.LeftIndicatorName, rule.LeftPeriod, rule.LeftParameter2, rule.LeftParameter3, 0, candles, cache);

                // Jobb oldal: p1, p2, p3 (ha indikátor)
                if (rule.RightSourceType == DataSourceType.Indicator)
                {
                    EnsureIndicatorCalculated(rule.RightIndicatorName, rule.RightPeriod, rule.RightParameter2, rule.RightParameter3, rule.LeftPeriod, candles, cache);
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

        // --- JAVÍTOTT EVALUATE (HASZNÁLJA A TÖBBI PARAMÉTERT IS) ---
        private bool EvaluateSingleRule(StrategyRule rule, Dictionary<string, double[]> cache, List<Candle> candles, int currentIndex)
        {
            int index = currentIndex - rule.Shift;
            if (index < 1) return false;

            // Bal oldal lekérése (p1, p2, p3)
            double leftValue = GetValue(rule.LeftIndicatorName, rule.LeftPeriod, rule.LeftParameter2, rule.LeftParameter3,
                                        rule.LeftIndicatorName == "Close", cache, candles, index);

            // Jobb oldal lekérése (p1, p2, p3)
            double rightValue;
            if (rule.RightSourceType == DataSourceType.Value)
            {
                rightValue = rule.RightValue;
            }
            else
            {
                rightValue = GetValue(rule.RightIndicatorName, rule.RightPeriod, rule.RightParameter2, rule.RightParameter3,
                                      rule.RightIndicatorName == "Close", cache, candles, index, rule.LeftPeriod);
            }

            // Előző értékek (Crosses figyeléshez)
            double leftPrev = GetValue(rule.LeftIndicatorName, rule.LeftPeriod, rule.LeftParameter2, rule.LeftParameter3, false, cache, candles, index - 1);

            double rightPrev;
            if (rule.RightSourceType == DataSourceType.Value)
            {
                rightPrev = rule.RightValue;
            }
            else
            {
                rightPrev = GetValue(rule.RightIndicatorName, rule.RightPeriod, rule.RightParameter2, rule.RightParameter3, false, cache, candles, index - 1, rule.LeftPeriod);
            }

            // Ha bármelyik érték NaN (azaz érvénytelen adat a chart elején), nem adunk jelzést
            if (double.IsNaN(leftValue) || double.IsNaN(rightValue) || double.IsNaN(leftPrev) || double.IsNaN(rightPrev))
                return false;

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

        // --- JAVÍTOTT CALCULATE (KIBŐVÍTETT PARAMÉTEREK) ---
        // Most már átvesszük a p2-t és p3-at is!
        private void EnsureIndicatorCalculated(string name, int p1, int p2, int p3, int dependencyPeriod, List<Candle> candles, Dictionary<string, double[]> cache)
        {
            if (string.IsNullOrEmpty(name)) return;
            // A kulcsba beleégetjük mindhárom paramétert, hogy pl. MACD_12_26_9 ne keveredjen a MACD_10_20_5-tel
            string key = dependencyPeriod > 0
                ? $"{name}_{p1}_{p2}_{p3}_dep{dependencyPeriod}"
                : $"{name}_{p1}_{p2}_{p3}";

            if (cache.ContainsKey(key)) return;

            double[] values = new double[candles.Count];
            var prices = candles.Select(c => (double)c.Close).ToList();

            // Alapértelmezések kezelése, ha a felhasználó 0-t hagyott ott
            // MACD (12, 26, 9)
            // Stoch (14, 3, 3)
            // Bollinger (20, 2)

            switch (name.ToUpper())
            {
                case "SMA":
                    values = IndicatorAlgorithms.CalculateSMA(candles, p1);
                    break;
                case "EMA":
                    values = IndicatorAlgorithms.CalculateEMA(candles, p1);
                    break;
                case "CMF":
                    values = IndicatorAlgorithms.CalculateCMF(candles, p1);
                    break;
                case "CLOSE":
                    values = prices.ToArray();
                    break;
                case "CMF_MA":
                    // Itt a p2, p3 nem kell a CMF-nek, csak a dependency
                    EnsureIndicatorCalculated("CMF", dependencyPeriod, 0, 0, 0, candles, cache);
                    string parentKey = $"CMF_{dependencyPeriod}_0_0"; // Szülő kulcs
                    if (cache.ContainsKey(parentKey))
                        values = IndicatorAlgorithms.CalculateSMAOnArray(cache[parentKey], p1);
                    break;
                case "RSI":
                    values = IndicatorAlgorithms.CalculateRSI(prices, p1).ToArray();
                    break;
                case "RSI_MA":
                    {
                        // 1. Először biztosítjuk, hogy az alap RSI ki legyen számolva
                        // (p1 az RSI periódusa, pl. 14)
                        EnsureIndicatorCalculated("RSI", p1, 0, 0, 0, candles, cache);

                        // 2. Megkeressük a cache-ben az alap RSI adatait
                        string rsiKey = $"RSI_{p1}_0_0";
                        if (cache.ContainsKey(rsiKey))
                        {
                            // 3. Lefuttatunk rajta egy SMA-t (p2 lesz a mozgóátlag hossza, pl. 9)
                            // Fontos: p2 paramétert használjuk a hosszhoz!
                            int maPeriod = p2 == 0 ? 9 : p2; // Ha 0, akkor alapértelmezett 9

                            values = IndicatorAlgorithms.CalculateSMAOnArray(cache[rsiKey], maPeriod);
                        }
                    }
                    break;
                case "MACD_MAIN":
                case "MACD": // Ha valaki csak simán MACD-t választ
                    {
                        int fast = p1 == 0 ? 12 : p1;
                        int slow = p2 == 0 ? 26 : p2;
                        int sig = p3 == 0 ? 9 : p3;
                        var macd = IndicatorAlgorithms.CalculateMACD(prices, fast, slow, sig);
                        values = macd.Item1.ToArray(); // Item1 = MACD Line
                    }
                    break;

                case "MACD_SIGNAL":
                    {
                        int fast = p1 == 0 ? 12 : p1;
                        int slow = p2 == 0 ? 26 : p2;
                        int sig = p3 == 0 ? 9 : p3;
                        var macdSig = IndicatorAlgorithms.CalculateMACD(prices, fast, slow, sig);
                        values = macdSig.Item2.ToArray(); // Item2 = Signal Line
                    }
                    break;

                case "MACD_HIST":
                    {
                        int fast = p1 == 0 ? 12 : p1;
                        int slow = p2 == 0 ? 26 : p2;
                        int sig = p3 == 0 ? 9 : p3;
                        var macdHist = IndicatorAlgorithms.CalculateMACD(prices, fast, slow, sig);
                        values = macdHist.Item3.ToArray(); // Item3 = Histogram
                    }
                    break;

                case "STOCH":
                    {
                        int kPer = p1 == 0 ? 14 : p1;
                        int dPer = p2 == 0 ? 3 : p2;
                        int slow = p3 == 0 ? 3 : p3;
                        var stoch = IndicatorAlgorithms.CalculateStoch(candles, kPer, dPer, slow);
                        values = stoch.Item1.ToArray(); // Item1 = K% (általában ez a fő)
                    }
                    break;
                case "STOCH_SIGNAL":
                    {
                        int kPer = p1 == 0 ? 14 : p1;
                        int dPer = p2 == 0 ? 3 : p2;
                        int slow = p3 == 0 ? 3 : p3;

                        var stoch = IndicatorAlgorithms.CalculateStoch(candles, kPer, dPer, slow);

                        // Item2-t mentjük el, ami a D%!
                        values = stoch.Item2.ToArray();
                    }
                    break;

                case "BOLLINGER_UPPER":
                case "BB_UPPER":
                    {
                        double dev = p2 == 0 ? 2.0 : (double)p2; // p2 a szórás
                        var bb = IndicatorAlgorithms.CalculateBollingerBands(prices, p1, dev);
                        values = bb.Item2.ToArray();
                    }
                    break;

                case "BOLLINGER_LOWER":
                case "BB_LOWER":
                    {
                        double dev = p2 == 0 ? 2.0 : (double)p2;
                        var bb = IndicatorAlgorithms.CalculateBollingerBands(prices, p1, dev);
                        values = bb.Item3.ToArray();
                    }
                    break;

                case "BOLLINGER_MIDDLE":
                case "BOLLINGER_MID":
                case "BB_MIDDLE":
                case "BB_MID":
                    {
                        double dev = p2 == 0 ? 2.0 : (double)p2;
                        var bb = IndicatorAlgorithms.CalculateBollingerBands(prices, p1, dev);
                        values = bb.Item1.ToArray();
                    }
                    break;
            }
            cache[key] = values;
        }

        // --- JAVÍTOTT GETVALUE (KIBŐVÍTETT PARAMÉTEREK) ---
        //private double GetValue(string name, int p1, int p2, int p3, bool isPrice, Dictionary<string, double[]> cache, List<Candle> candles, int index, int dependencyPeriod = 0)
        //{
        //    if (index < 0) return double.NaN;

        //    if (isPrice || name.ToUpper() == "CLOSE" || name.ToUpper() == "OPEN" || name.ToUpper() == "HIGH" || name.ToUpper() == "LOW")
        //    {
        //        if (string.IsNullOrEmpty(name)) return double.NaN;
        //        int targetIndex = index - p1; // "p1" itt eltolást jelenthet a Close-nál
        //        if (targetIndex < 0) return double.NaN;

        //        switch (name.ToUpper())
        //        {
        //            case "OPEN": return (double)candles[targetIndex].Open;
        //            case "HIGH": return (double)candles[targetIndex].High;
        //            case "LOW": return (double)candles[targetIndex].Low;
        //            default: return (double)candles[targetIndex].Close;
        //        }
        //    }

        //    // Kulcs generálás a lekéréshez (ugyanaz a logika, mint fent)
        //    if (dependencyPeriod > 0)
        //    {
        //        string keyWithDep = $"{name}_{p1}_{p2}_{p3}_dep{dependencyPeriod}";
        //        if (cache.ContainsKey(keyWithDep)) return cache[keyWithDep][index];
        //    }

        //    string keySimple = $"{name}_{p1}_{p2}_{p3}";
        //    if (cache.ContainsKey(keySimple)) return cache[keySimple][index];

        //    return double.NaN;
        //}
        private double GetValue(string name, int p1, int p2, int p3, bool isPrice, Dictionary<string, double[]> cache, List<Candle> candles, int index, int dependencyPeriod = 0)
        {
            // 1. VÉDELEM: Ha nincs név, vagy rossz az index, azonnal kilépünk.
            if (string.IsNullOrEmpty(name)) return double.NaN;
            if (index < 0) return double.NaN;

            // 2. VÉDELEM: string.Equals használata .ToUpper() helyett (ez NULL-biztos!)
            bool isClose = string.Equals(name, "CLOSE", StringComparison.OrdinalIgnoreCase);
            bool isOpen = string.Equals(name, "OPEN", StringComparison.OrdinalIgnoreCase);
            bool isHigh = string.Equals(name, "HIGH", StringComparison.OrdinalIgnoreCase);
            bool isLow = string.Equals(name, "LOW", StringComparison.OrdinalIgnoreCase);

            if (isPrice || isClose || isOpen || isHigh || isLow)
            {
                int targetIndex = index - p1;
                if (targetIndex < 0) return double.NaN;

                if (isOpen) return (double)candles[targetIndex].Open;
                if (isHigh) return (double)candles[targetIndex].High;
                if (isLow) return (double)candles[targetIndex].Low;

                // Default is Close
                return (double)candles[targetIndex].Close;
            }

            // Kulcs generálás (dependency kezelés)
            string key;
            if (dependencyPeriod > 0)
            {
                key = $"{name}_{p1}_{p2}_{p3}_dep{dependencyPeriod}";
            }
            else
            {
                key = $"{name}_{p1}_{p2}_{p3}";
            }

            // Érték kiolvasása a cache-ből
            if (cache != null && cache.ContainsKey(key))
            {
                // Extra védelem: index határok ellenőrzése a tömbön belül
                double[] values = cache[key];
                if (index < values.Length)
                {
                    return values[index];
                }
            }

            return double.NaN;
        }
    }
}