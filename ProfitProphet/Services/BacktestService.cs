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

            // Indikátorok előszámolása
            var indicatorCache = PrecalculateIndicators(candles, profile);

            double cash = initialCash;
            int holdings = 0;           // Jelenlegi részvény darabszám
            double avgEntryPrice = 0;   // Súlyozott átlagár (a rávásárlások miatt)

            result.EquityCurve.Add(new EquityPoint { Time = candles[0].TimestampUtc, Equity = cash });

            // FUTTATÁS
            int startIndex = 50;

            for (int i = startIndex; i < candles.Count; i++)
            {
                var currentCandle = candles[i];
                double price = (double)currentCandle.Close;

                // -----------------------------
                // 1. VÉTELI LOGIKA (ENTRY)
                // -----------------------------
                // Vásárolhatunk, ha: Nincs pozíció VAGY (Van, de engedélyezett a rávásárlás)
                bool canBuy = (holdings == 0) || profile.AllowPyramiding;

                if (canBuy && EvaluateGroups(profile.EntryGroups, indicatorCache, candles, i))
                {
                    // Mennyit vegyünk?
                    int quantityToBuy = CalculatePositionSize(profile, cash, price);

                    if (quantityToBuy > 0)
                    {
                        double tradeValue = quantityToBuy * price;
                        double fee = CalculateFee(tradeValue, profile); // NetBroker díj

                        // Van elég pénzünk a vételre + díjra?
                        if (cash >= tradeValue + fee)
                        {
                            // Átlagár frissítése (Súlyozott átlag)
                            double totalValueBefore = holdings * avgEntryPrice;
                            double totalValueNew = tradeValue; // Díj nélkül számoljuk az átlagárat a tiszta matekhoz
                            avgEntryPrice = (totalValueBefore + totalValueNew) / (holdings + quantityToBuy);

                            // Tranzakció
                            cash -= (tradeValue + fee);
                            holdings += quantityToBuy;

                            result.Trades.Add(new TradeRecord
                            {
                                EntryDate = currentCandle.TimestampUtc,
                                EntryPrice = (decimal)price,
                                Quantity = quantityToBuy, // Ezt is érdemes lenne tárolni a TradeRecordban
                                Type = "Long"
                            });
                        }
                    }
                }

                // -----------------------------
                // 2. ELADÁSI LOGIKA (EXIT)
                // -----------------------------
                // Eladhatunk, ha van mit
                else if (holdings > 0 && EvaluateGroups(profile.ExitGroups, indicatorCache, candles, i))
                {
                    bool shouldSell = true;

                    // Ha be van kapcsolva a "Csak profitban" opció
                    if (profile.OnlySellInProfit)
                    {
                        // Kiszámoljuk, mennyi lenne a bevétel
                        double potentialRevenue = holdings * price;
                        double potentialFee = CalculateFee(potentialRevenue, profile);
                        double netRevenue = potentialRevenue - potentialFee;

                        // Mennyibe került mindez? (Átlagár * darab) + a VÉTELI jutalékok (ez már levonódott a cash-ből)
                        // A legegyszerűbb profit számítás: Jelenlegi vagyon vs. "Mennyibe került volna akkor"
                        // De itt most a trade szintű profitot nézzük:
                        // Break-even ár: (Átlagár * Darab + VételiDíjak + EladásiDíj) / Darab
                        // Egyszerűsítve: Ha a nettó bevétel több mint amennyit költöttünk a részvényekre (Átlagár * db)

                        if (netRevenue <= (holdings * avgEntryPrice))
                        {
                            shouldSell = false; // Nem adjuk el, mert bukóban vagyunk
                        }
                    }

                    if (shouldSell)
                    {
                        double revenue = holdings * price;
                        double fee = CalculateFee(revenue, profile); // NetBroker díj

                        cash += (revenue - fee);

                        // Eredmény rögzítése (az utolsó nyitott trade-et lezárjuk, 
                        // de rávásárlásnál ez bonyolultabb, egyszerűsítve az utolsó trade-hez írjuk a zárót)
                        // PROFI MEGOLDÁS: A TradeRecord-nak kezelnie kéne a részleges zárást, 
                        // de most egyszerűsítve az összes nyitott trade-et lezárjuk a listában.

                        foreach (var trade in result.Trades.Where(t => t.ExitDate == DateTime.MinValue))
                        {
                            trade.ExitDate = currentCandle.TimestampUtc;
                            trade.ExitPrice = (decimal)price;

                            // A profit számítása itt trükkös rávásárlásnál. 
                            // Egyszerűsítve: (Eladási ár - Vételi ár) * mennyiség - (arányos költség)
                            // Most a BacktestResult TotalProfitLoss-a pontos lesz a cash miatt, 
                            // de az egyes trade-ek profitja becslés lesz.
                            trade.Profit = (decimal)((double)(trade.ExitPrice - trade.EntryPrice) * 1); // Placeholder
                        }

                        // Reset
                        holdings = 0;
                        avgEntryPrice = 0;

                        result.EquityCurve.Add(new EquityPoint { Time = currentCandle.TimestampUtc, Equity = cash });
                    }
                }
            }

            // Kényszerített zárás a végén
            if (holdings > 0)
            {
                double price = (double)candles.Last().Close;
                double revenue = holdings * price;
                double fee = CalculateFee(revenue, profile);
                cash += (revenue - fee);
            }

            result.TotalProfitLoss = cash - initialCash;
            result.TradeCount = result.Trades.Count;
            // WinRate számításnál csak a lezártakat nézzük
            var closedTrades = result.Trades.Where(t => t.ExitDate != DateTime.MinValue).ToList();
            result.WinRate = closedTrades.Count > 0
                ? (double)closedTrades.Count(t => t.ExitPrice > t.EntryPrice) / closedTrades.Count
                : 0;

            return result;
        }

        // --- SEGÉDMETÓDUSOK ---

        private double CalculateFee(double tradeValue, StrategyProfile profile)
        {
            // NetBroker logika: 0.45%, de minimum 7 USD
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
                    // Hagyunk 2% tartalékot a költségekre, a többit befektetjük
                    amountToInvest = currentCash * 0.98;
                    break;

                case TradeAmountType.FixedCash:
                    amountToInvest = profile.TradeAmount;
                    // Ha nincs annyi pénzünk, amennyit fixen akarunk, akkor csak a maradékot költjük
                    if (amountToInvest > currentCash) amountToInvest = currentCash;
                    break;

                case TradeAmountType.FixedShareCount:
                    // ITT A VÁLASZ A KÉRDÉSEDRE: Igen, ez a fix LOT / Darabszám.
                    // Ellenőrizzük, van-e rá elég pénz
                    double requiredCash = profile.TradeAmount * price;
                    if (requiredCash > currentCash)
                    {
                        // Ha nincs rá pénz, annyit veszünk, amennyi kijön (vagy 0-t, stratégia függő)
                        // Most vesszük a maximumot, ami kijön a pénzből
                        return (int)(currentCash / price);
                    }
                    return (int)profile.TradeAmount;

                // --- EZ AZ ÚJ RÉSZ ---
                case TradeAmountType.PercentageOfEquity:
                    // A TradeAmount itt %-ot jelent (pl. 10 = 10%)
                    double percent = Math.Max(0, Math.Min(100, profile.TradeAmount)); // 0 és 100 közé szorítjuk
                    amountToInvest = currentCash * (percent / 100.0);
                    break;
            }

            // Kiszámoljuk, hány darab jön ki a pénzből
            return (int)(amountToInvest / price);
        }

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