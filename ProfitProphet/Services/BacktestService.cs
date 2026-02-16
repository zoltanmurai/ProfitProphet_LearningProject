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
        public BacktestResult RunBacktest(List<Candle> candles, StrategyProfile profile, double initialCash)
        {
            var result = new BacktestResult
            {
                Symbol = profile.Symbol,
                EquityCurve = new List<EquityPoint>(),
                BalanceCurve = new List<EquityPoint>(),
                Trades = new List<TradeRecord>()
            };

            if (candles == null || candles.Count < 50) return result;

            //System.Diagnostics.Debug.WriteLine($"[BACKTEST START] Szimbólum: {result.Symbol}");
            //System.Diagnostics.Debug.WriteLine($"[BACKTEST DATA] Gyertyák száma: {candles.Count}");
            //System.Diagnostics.Debug.WriteLine($"[BACKTEST DATA] initialCash: {initialCash}");

            if (candles.Count > 0)
            {
                var first = candles[0];
                var last = candles[candles.Count - 1];
                //System.Diagnostics.Debug.WriteLine($"[DATA MINTA] Első: {first.TimestampUtc} | Ár: {first.Close}");
                //System.Diagnostics.Debug.WriteLine($"[DATA MINTA] Utolsó: {last.TimestampUtc} | Ár: {last.Close}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[HIBA] ÜRES a gyertyák listája! A Backtester nem kapott adatot.");
            }

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
            int lastEntryIndex = -999;

            for (int i = startIndex; i < candles.Count; i++)
            {
                var currentCandle = candles[i];
                double price = (double)currentCandle.Close;

                // --- 1. VÉTEL ---
                bool canBuy = (holdings == 0) || profile.AllowPyramiding;

                if (canBuy && EvaluateGroups(profile.EntryGroups, indicatorCache, candles, i))
                {
                    //var lastOpenTrade = result.Trades.LastOrDefault(t => t.ExitDate == DateTime.MinValue);
                    var activeTrades = result.Trades.Where(t => t.ExitDate == DateTime.MinValue).ToList();

                    if (i - lastEntryIndex < 1)
                    {
                        continue; // Ugrunk a következő napra/órára
                    }

                    // 2. MAX POZÍCIÓ LIMIT
                    int maxPyramidLayers = 10;
                    if (activeTrades.Count >= maxPyramidLayers)
                    {
                        continue;
                    }

                    //if (holdings > 0)
                    //{
                    //    int activeTradesCount = result.Trades.Count(t => t.ExitDate == DateTime.MinValue);
                    //    if (activeTradesCount >= 5) // Max 5 rávásárlás
                    //    {
                    //        continue;
                    //    }
                    //}

                    //if (lastOpenTrade != null)
                    //{
                    //    // A) MAX LIMIT ELLENŐRZÉS (Maradhat, hogy ne szálljon el a tőkeáttét)
                    //    int activeTradesCount = result.Trades.Count(t => t.ExitDate == DateTime.MinValue);
                    //    if (activeTradesCount >= 10) // Pl. Max 10 réteg engedélyezve
                    //    {
                    //        continue;
                    //    }
                    //    // B) ENVELOPE / GRID TÁVOLSÁG SZÁMÍTÁS
                    //    double lastPrice = (double)lastOpenTrade.EntryPrice;
                    //    // Kiszámoljuk a százalékos távolságot
                    //    double deviation = Math.Abs(price - lastPrice) / lastPrice;

                    //    // BEÁLLÍTÁS: Minimum sávméret. ezt kísérlezni kell
                    //    // 0.01 = 1%-os sáv. Amíg ezen belül mozog (oldalaz), NEM vesz újat.
                    //    double minEnvelopeSize = 0.01;

                    //    // oldalazunk?
                    //    if (deviation < minEnvelopeSize)
                    //    {
                    //        continue;
                    //    }

                    //    // Ha csak TREND irányba akarunk venni (Pyramiding). ez nem feltétel most
                    //    // if (price <= lastPrice * (1 + minEnvelopeSize)) continue; 
                    //}


                    // CASH ELLENŐRZÉS (ne menjen negatívba!)
                    int quantityToBuy = CalculatePositionSize(profile, cash, price);
                    double tradeValue = quantityToBuy * price;
                    double fee = CalculateFee(tradeValue, profile);

                    if (cash < tradeValue + fee)
                    {
                        continue; // Nincs elég pénz
                    }

                    bool isTooCloseToAnyTrade = false;
                    double minGridPercent = 0.01; // 1%-os minimális távolság (Ezt később ki lehet vezetni paraméternek)

                    foreach (var trade in activeTrades)
                    {
                        double existingPrice = (double)trade.EntryPrice;

                        // Kiszámoljuk a távolságot a JELENLEGI ár és a RÉGI kötés között
                        double difference = Math.Abs(price - existingPrice) / existingPrice;

                        // Ha BÁRMELYIK meglévő kötéshez túl közel vagyunk (< 1%)
                        if (difference < minGridPercent)
                        {
                            isTooCloseToAnyTrade = true;
                            break; // Találtunk egyet
                        }
                    }

                    // Ha túl közel van valamelyikhez, akkor kihagyjuk a vételt (lépünk a kövi gyertyára)
                    if (isTooCloseToAnyTrade)
                    {
                        continue;
                    }

                    //int quantityToBuy = CalculatePositionSize(profile, cash, price);

                    if (quantityToBuy > 0)
                    {
                        //double tradeValue = quantityToBuy * price;
                        //double fee = CalculateFee(tradeValue, profile);

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
                            lastEntryIndex = i;
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

                        //foreach (var trade in result.Trades.Where(t => t.ExitDate == DateTime.MinValue))
                        //{
                        //    trade.ExitDate = currentCandle.TimestampUtc;
                        //    trade.ExitPrice = (decimal)price;
                        //    trade.Profit = (decimal)((double)(trade.ExitPrice - trade.EntryPrice) * trade.Quantity);
                        //}

                        foreach (var trade in result.Trades.Where(t => t.ExitDate == DateTime.MinValue))
                        {
                            trade.ExitDate = currentCandle.TimestampUtc;
                            trade.ExitPrice = (decimal)price;

                            // Entry fee (már levonva volt a cash-ből, de itt nem szerepel)
                            double entryValue = (double)trade.EntryPrice * trade.Quantity;
                            double entryFee = CalculateFee(entryValue, profile);

                            // Exit fee
                            double exitValue = (double)trade.ExitPrice * trade.Quantity;
                            double exitFee = CalculateFee(exitValue, profile);

                            // Bruttó profit - költségek
                            double grossP = ((double)trade.ExitPrice - (double)trade.EntryPrice) * trade.Quantity;
                            trade.Profit = (decimal)(grossP - entryFee - exitFee);
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
            // Kényszerített zárás a végén (JAVÍTOTT VERZIÓ)
            if (holdings > 0)
            {
                double lastPrice = (double)candles.Last().Close;
                double revenue = holdings * lastPrice;
                double fee = CalculateFee(revenue, profile);

                // 1. A PÉNZT HOZZÁADJUK
                cash += (revenue - fee);

                // 2. A NYITOTT POZÍCIÓK KEZELÉSE
                foreach (var trade in result.Trades.Where(t => t.ExitDate == DateTime.MinValue))
                {
                    // --- KOMMENTELTTEM (Így nem lesz nyíl a charton az utolsó gyertyán): ---
                    // trade.ExitDate = candles.Last().TimestampUtc; 

                    // az árat és a profitot beírjuk
                    trade.ExitPrice = (decimal)lastPrice;
                    trade.Profit = (decimal)((double)(trade.ExitPrice - trade.EntryPrice) * trade.Quantity);
                }
            }

            // A statisztikák számítása változatlan marad
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
                case TradeAmountType.AllCash:
                    amountToInvest = currentCash * 0.98;
                    break;
                case TradeAmountType.FixedCash:
                    amountToInvest = Math.Min(profile.TradeAmount, currentCash);
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

            int quantity = (int)(amountToInvest / price);

            // EXTRA VÉDELEM
            double totalCost = quantity * price + CalculateFee(quantity * price, profile);
            if (totalCost > currentCash)
            {
                return 0; // Még a fee-vel is túllépné
            }

            return quantity;
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

        // --- JAVÍTOTT EVALUATE (MEGJAVÍTVA: LeftShift és RightShift kezelése) ---
        private bool EvaluateSingleRule(StrategyRule rule, Dictionary<string, double[]> cache, List<Candle> candles, int currentIndex)
        {
            // 1. Bal index kiszámítása (LeftShift)
            int leftIndex = currentIndex - rule.LeftShift;
            if (leftIndex < 0 || leftIndex >= candles.Count) return false;

            // 2. Jobb index kiszámítása (RightShift)
            int rightIndex = currentIndex - rule.RightShift;
            // Ha a jobb oldal indikátor, ellenőrizzük a határokat
            if (rule.RightSourceType == DataSourceType.Indicator)
            {
                if (rightIndex < 0 || rightIndex >= candles.Count) return false;
            }

            // Bal oldal lekérése
            double leftValue = GetValue(rule.LeftIndicatorName, rule.LeftPeriod, rule.LeftParameter2, rule.LeftParameter3,
                                        rule.LeftIndicatorName == "Close", cache, candles, leftIndex);

            // Jobb oldal lekérése
            double rightValue;
            if (rule.RightSourceType == DataSourceType.Value)
            {
                rightValue = rule.RightValue;
            }
            else
            {
                rightValue = GetValue(rule.RightIndicatorName, rule.RightPeriod, rule.RightParameter2, rule.RightParameter3,
                                      rule.RightIndicatorName == "Close", cache, candles, rightIndex, rule.LeftPeriod);
            }

            // Előző értékek (Crosses figyeléshez)
            // Itt is a SHIFTELT indexekből vonunk le 1-et!
            double leftPrev = GetValue(rule.LeftIndicatorName, rule.LeftPeriod, rule.LeftParameter2, rule.LeftParameter3, false, cache, candles, leftIndex - 1);

            double rightPrev;
            if (rule.RightSourceType == DataSourceType.Value)
            {
                rightPrev = rule.RightValue;
            }
            else
            {
                rightPrev = GetValue(rule.RightIndicatorName, rule.RightPeriod, rule.RightParameter2, rule.RightParameter3, false, cache, candles, rightIndex - 1, rule.LeftPeriod);
            }

            // Ha bármelyik érték NaN (azaz érvénytelen adat a chart elején), nem adunk jelzést
            if (double.IsNaN(leftValue) || double.IsNaN(rightValue) || double.IsNaN(leftPrev) || double.IsNaN(rightPrev))
                return false;

            switch (rule.Operator)
            {
                case ComparisonOperator.GreaterThan: return leftValue > rightValue;
                case ComparisonOperator.GreaterThanOrEqual:return leftValue >= rightValue; // >=
                case ComparisonOperator.LessThan: return leftValue < rightValue;
                case ComparisonOperator.LessThanOrEqual: return leftValue <= rightValue; // <=
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
                        EnsureIndicatorCalculated("RSI", p1, 0, 0, 0, candles, cache);
                        string rsiKey = $"RSI_{p1}_0_0";
                        if (cache.ContainsKey(rsiKey))
                        {
                            int maPeriod = p2 == 0 ? 9 : p2;
                            values = IndicatorAlgorithms.CalculateSMAOnArray(cache[rsiKey], maPeriod);
                        }
                    }
                    break;
                case "MACD_MAIN":
                case "MACD":
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
                        values = stoch.Item2.ToArray(); // Item2 = D%
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

        private double GetValue(string name, int p1, int p2, int p3, bool isPrice,
                       Dictionary<string, double[]> cache, List<Candle> candles,
                       int index, int dependencyPeriod = 0)
        {
            if (string.IsNullOrEmpty(name)) return double.NaN;
            if (index < 0 || index >= candles.Count) return double.NaN;

            string nameUpper = name.ToUpper();

            // PRICE HANDLING - egyszerűen visszaadjuk az index-en lévő értéket
            if (nameUpper == "CLOSE") return (double)candles[index].Close;
            if (nameUpper == "OPEN") return (double)candles[index].Open;
            if (nameUpper == "HIGH") return (double)candles[index].High;
            if (nameUpper == "LOW") return (double)candles[index].Low;

            // INDICATOR HANDLING - itt a cache-ből jön
            string key = dependencyPeriod > 0
                ? $"{name}_{p1}_{p2}_{p3}_dep{dependencyPeriod}"
                : $"{name}_{p1}_{p2}_{p3}";

            if (cache != null && cache.ContainsKey(key))
            {
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