using ProfitProphet.Entities;
using ProfitProphet.Models.Backtesting;
using ProfitProphet.Models.Strategies;
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

            // 1. ADATOK ELŐKÉSZÍTÉSE (PRE-CALCULATION)
            // Kigyűjtjük az összes szükséges adatsort (pl. "CMF_20", "SMA_50") egy szótárba
            var indicatorCache = PrecalculateIndicators(candles, profile);

            // Egyenleg és állapot változók
            double cash = initialCash;
            int holdings = 0;
            bool inPosition = false;

            // Kezdőpont a grafikonhoz
            result.EquityCurve.Add(new EquityPoint { Time = candles[0].TimestampUtc, Equity = cash });

            // 2. FUTTATÁS (A CIKLUS)
            // Kezdjük az indexet kicsit beljebb, hogy a mozgóátlagoknak legyen adata (pl. 50. gyertya)
            int startIndex = 50;

            for (int i = startIndex; i < candles.Count; i++)
            {
                var currentCandle = candles[i];

                // --- VÉTELI SZABÁLYOK ELLENŐRZÉSE ---
                // Ha NEM vagyunk pozícióban, és MINDEN vételi szabály igaz
                if (!inPosition && EvaluateRules(profile.EntryRules, indicatorCache, candles, i))
                {
                    // VÉTEL
                    double price = (double)currentCandle.Close;
                    int quantity = (int)(cash / price); // Egyszerűsített "All-in"

                    if (quantity > 0)
                    {
                        double cost = quantity * price;
                        double fee = cost * 0.001; // 0.1% jutalék (példa)

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
                // --- ELADÁSI SZABÁLYOK ELLENŐRZÉSE ---
                // Ha pozícióban vagyunk, és BÁRMELYIK kilépési szabály igaz (vagy minden - stratégia függő)
                // Most feltételezzük, hogy "ÉS" kapcsolat van a szabályok között, de kilépésnél gyakran "VAGY" kell (pl. StopLoss VAGY TakeProfit).
                // Egyelőre maradjunk az "ÉS"-nél, vagy ha a lista üres, akkor sose lép ki (csak StopLoss-ra kéne).
                else if (inPosition && EvaluateRules(profile.ExitRules, indicatorCache, candles, i))
                {
                    // ELADÁS
                    double price = (double)currentCandle.Close;
                    double revenue = holdings * price;
                    double fee = revenue * 0.001;

                    cash += (revenue - fee);

                    // Utolsó trade frissítése
                    var lastTrade = result.Trades.Last();
                    lastTrade.ExitDate = currentCandle.TimestampUtc;
                    lastTrade.ExitPrice = (decimal)price;
                    lastTrade.Profit = (decimal)((revenue - fee) - ((double)lastTrade.EntryPrice * holdings)); // (Pontosabb számítás kéne a fee-vel, de most egyszerűsítünk)

                    holdings = 0;
                    inPosition = false;

                    // Egyenleg mentése a grafikonhoz
                    result.EquityCurve.Add(new EquityPoint { Time = currentCandle.TimestampUtc, Equity = cash });
                }
            }

            // Pozíciók lezárása a végén (hogy ne lógjon a levegőben)
            if (inPosition)
            {
                double price = (double)candles.Last().Close;
                cash += holdings * price;
                var lastTrade = result.Trades.Last();
                lastTrade.ExitDate = candles.Last().TimestampUtc;
                lastTrade.ExitPrice = (decimal)price;
            }

            // Eredmények összegzése
            result.TotalProfitLoss = cash - initialCash;
            result.TradeCount = result.Trades.Count;
            result.WinRate = result.Trades.Count > 0
                ? (double)result.Trades.Count(t => t.Profit > 0) / result.Trades.Count
                : 0;

            return result;
        }

        // Segédszámítások
        private Dictionary<string, double[]> PrecalculateIndicators(List<Candle> candles, StrategyProfile profile)
        {
            var cache = new Dictionary<string, double[]>();
            var allRules = profile.EntryRules.Concat(profile.ExitRules);

            foreach (var rule in allRules)
            {
                // Bal oldal feldolgozása
                EnsureIndicatorCalculated(rule.LeftIndicatorName, rule.LeftPeriod, candles, cache);

                // Jobb oldal feldolgozása (ha indikátor)
                if (rule.RightSourceType == DataSourceType.Indicator)
                {
                    EnsureIndicatorCalculated(rule.RightIndicatorName, rule.RightPeriod, candles, cache);
                }
            }
            return cache;
        }

        // Biztosítja, hogy az adott indikátor (pl. "SMA_50") benne legyen a cache-ben
        private void EnsureIndicatorCalculated(string name, int period, List<Candle> candles, Dictionary<string, double[]> cache)
        {
            string key = $"{name}_{period}";
            if (cache.ContainsKey(key)) return; // Már megvan

            double[] values = new double[candles.Count];

            // ITT KELL BŐVÍTENI, ha új indikátort adsz hozzá!
            switch (name.ToUpper())
            {
                case "SMA": // Simple Moving Average
                    values = CalculateSMA(candles, period);
                    break;
                case "EMA": // Exponential Moving Average (ha van ilyen logikád)
                            // values = CalculateEMA(candles, period);
                    break;
                case "CMF": // Chaikin Money Flow
                    values = CalculateCMF(candles, period);
                    break;
                case "CLOSE": // Záróár (mint indikátor)
                    values = candles.Select(c => (double)c.Close).ToArray();
                    break;
                default:
                    // Ha nem ismerjük, nullákkal töltjük fel (vagy dobhatunk hibát)
                    break;
            }

            cache[key] = values;
        }

        // 2. Szabályok kiértékelése (Evaluator)
        private bool EvaluateRules(List<StrategyRule> rules, Dictionary<string, double[]> cache, List<Candle> candles, int index)
        {
            if (rules.Count == 0) return false; // Ha nincs szabály, nem csinálunk semmit

            foreach (var rule in rules)
            {
                // Bal érték lekérése
                double leftValue = GetValue(rule.LeftIndicatorName, rule.LeftPeriod, rule.LeftIndicatorName == "Close", cache, candles, index);

                // Jobb érték lekérése
                double rightValue = 0;
                if (rule.RightSourceType == DataSourceType.Value)
                {
                    rightValue = rule.RightValue;
                }
                else
                {
                    rightValue = GetValue(rule.RightIndicatorName, rule.RightPeriod, rule.RightIndicatorName == "Close", cache, candles, index);
                }

                // Összehasonlítás
                bool conditionMet = false;

                // Előző értékek (keresztezés vizsgálathoz)
                double leftPrev = GetValue(rule.LeftIndicatorName, rule.LeftPeriod, false, cache, candles, index - 1);
                double rightPrev = rule.RightSourceType == DataSourceType.Value ? rule.RightValue : GetValue(rule.RightIndicatorName, rule.RightPeriod, false, cache, candles, index - 1);

                switch (rule.Operator)
                {
                    case ComparisonOperator.GreaterThan:
                        conditionMet = leftValue > rightValue;
                        break;
                    case ComparisonOperator.LessThan:
                        conditionMet = leftValue < rightValue;
                        break;
                    case ComparisonOperator.Equals:
                        conditionMet = Math.Abs(leftValue - rightValue) < 0.0001; // Lebegőpontos egyenlőség
                        break;
                    case ComparisonOperator.CrossesAbove:
                        // Most nagyobb, de előtte kisebb vagy egyenlő volt
                        conditionMet = (leftValue > rightValue) && (leftPrev <= rightPrev);
                        break;
                    case ComparisonOperator.CrossesBelow:
                        // Most kisebb, de előtte nagyobb vagy egyenlő volt
                        conditionMet = (leftValue < rightValue) && (leftPrev >= rightPrev);
                        break;
                }

                if (!conditionMet) return false; // Ha EGY szabály is bukik, akkor az egész bukik (ÉS kapcsolat)
            }

            return true; // Minden szabály átment
        }

        private double GetValue(string name, int period, bool isPrice, Dictionary<string, double[]> cache, List<Candle> candles, int index)
        {
            if (index < 0) return 0;

            // Ha Árfolyam, azt közvetlenül is lekérhetjük (bár a cache-be is betettük "CLOSE" néven)
            if (name.ToUpper() == "CLOSE") return (double)candles[index].Close;

            string key = $"{name}_{period}";
            if (cache.ContainsKey(key))
            {
                return cache[key][index];
            }
            return 0;
        }

        // --- MATEMATIKAI IMPLEMENTÁCIÓK (Ide másold be a CMF és SMA logikát a régi kódodból) ---

        private double[] CalculateSMA(List<Candle> candles, int period)
        {
            double[] result = new double[candles.Count];
            for (int i = period - 1; i < candles.Count; i++)
            {
                double sum = 0;
                for (int j = 0; j < period; j++) sum += (double)candles[i - j].Close;
                result[i] = sum / period;
            }
            return result;
        }

        // A CMF számítást már ismered, azt is implementálni kell itt tömb visszatéréssel
        private double[] CalculateCMF(List<Candle> candles, int period)
        {
            // ... (Ide jön a CMF logika, ami visszaad egy double[] tömböt) ...
            // Ha kell, megírom ezt is teljes egészében, de valószínűleg át tudod emelni.
            // Csak a struktúra kedvéért most üresen hagyom vagy egyszerűsítem:
            double[] result = new double[candles.Count];
            // (Implementáció helye)
            return result;
        }

        //private double[] CalculateCmf(double[] high, double[] low, double[] close, double[] volume, int period)
        //{
        //    int n = high.Length;
        //    var cmf = new double[n];
        //    var mfv = new double[n];

        //    for (int i = 0; i < n; i++)
        //    {
        //        double range = high[i] - low[i];
        //        double multiplier = range == 0 ? 0 : ((close[i] - low[i]) - (high[i] - close[i])) / range;
        //        double vol = volume[i];
        //        mfv[i] = multiplier * vol;

        //        if (i >= period - 1)
        //        {
        //            double sumMfv = 0;
        //            double sumVol = 0;
        //            for (int j = i - period + 1; j <= i; j++)
        //            {
        //                sumMfv += mfv[j];
        //                sumVol += volume[j];
        //            }
        //            cmf[i] = sumVol == 0 ? 0 : sumMfv / sumVol;
        //        }
        //        else cmf[i] = double.NaN;
        //    }
        //    return cmf;
        //}

    }
}