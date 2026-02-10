using ProfitProphet.Entities; // Vagy ahol a Candle osztályod van
using ProfitProphet.Models.Backtesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProfitProphet.Services
{
    public static class IndicatorAlgorithms
    {
        public static double[] CalculateSMA(List<Candle> candles, int period)
        {
            if (candles == null || candles.Count == 0) return new double[0];
            double[] result = new double[candles.Count];

            for (int i = 0; i < candles.Count; i++)
            {
                if (i < period - 1)
                {
                    result[i] = (double)candles[i].Close;
                    continue;
                }

                double sum = 0;
                for (int j = 0; j < period; j++)
                {
                    sum += (double)candles[i - j].Close;
                }
                result[i] = sum / period;
            }
            return result;
        }

        public static double[] CalculateEMA(List<Candle> candles, int period)
        {
            if (candles == null || candles.Count == 0) return new double[0];
            double[] result = new double[candles.Count];
            double multiplier = 2.0 / (period + 1);

            result[0] = (double)candles[0].Close;

            for (int i = 1; i < candles.Count; i++)
            {
                double close = (double)candles[i].Close;
                result[i] = ((close - result[i - 1]) * multiplier) + result[i - 1];
            }
            return result;
        }

        public static List<double> CalculateRSI(List<double> prices, int period)
        {
            var rsi = new List<double>();
            if (prices.Count < period + 1) return rsi;

            double gain = 0.0;
            double loss = 0.0;

            // Első átlagos nyereség/veszteség
            for (int i = 1; i <= period; i++)
            {
                double diff = prices[i] - prices[i - 1];
                if (diff > 0) gain += diff;
                else loss -= diff;
            }

            double avgGain = gain / period;
            double avgLoss = loss / period;

            // Feltöltjük az elejét 0-val vagy 50-nel, amíg nincs adat
            for (int i = 0; i < period; i++) rsi.Add(50);

            double rs = (avgLoss == 0) ? 100 : avgGain / avgLoss;
            rsi.Add(100.0 - (100.0 / (1.0 + rs)));

            // A többi gyertya (Wilder's Smoothing)
            for (int i = period + 1; i < prices.Count; i++)
            {
                double diff = prices[i] - prices[i - 1];
                double currentGain = (diff > 0) ? diff : 0;
                double currentLoss = (diff < 0) ? -diff : 0;

                avgGain = ((avgGain * (period - 1)) + currentGain) / period;
                avgLoss = ((avgLoss * (period - 1)) + currentLoss) / period;

                rs = (avgLoss == 0) ? 100 : avgGain / avgLoss;
                rsi.Add(100.0 - (100.0 / (1.0 + rs)));
            }

            // Kitöltjük a végét, hogy egyezzen a hossza a gyertyákkal (ha kell)
            while (rsi.Count < prices.Count) rsi.Insert(0, 50);

            return rsi;
        }

        public static double[] CalculateCMF(List<Candle> candles, int period)
        {
            if (candles == null || candles.Count == 0) return new double[0];
            double[] cmf = new double[candles.Count];

            for (int i = 0; i < candles.Count; i++)
            {
                if (i < period - 1)
                {
                    cmf[i] = 0;
                    continue;
                }

                double sumAd = 0;
                double sumVol = 0;

                for (int j = 0; j < period; j++)
                {
                    var c = candles[i - j];
                    double h = (double)c.High;
                    double l = (double)c.Low;
                    double cl = (double)c.Close;
                    double v = (double)c.Volume;

                    double mfm = (h == l) ? 0 : ((cl - l) - (h - cl)) / (h - l);
                    double mfv = mfm * v;

                    sumAd += mfv;
                    sumVol += v;
                }

                cmf[i] = (sumVol == 0) ? 0 : sumAd / sumVol;
            }
            return cmf;
        }

        public static (List<double>, List<double>, List<double>) CalculateMACD(List<double> prices, int fastPeriod, int slowPeriod, int signalPeriod)
        {
            var macdLine = new List<double>();
            var signalLine = new List<double>();
            var histogram = new List<double>();

            if (prices.Count < slowPeriod) return (macdLine, signalLine, histogram);

            // Segédfüggvényt használunk tömbre!
            double[] fastEma = CalculateEMAOnArray(prices.ToArray(), fastPeriod);
            double[] slowEma = CalculateEMAOnArray(prices.ToArray(), slowPeriod);

            double[] macdArray = new double[prices.Count];
            for (int i = 0; i < prices.Count; i++)
            {
                macdArray[i] = fastEma[i] - slowEma[i];
                macdLine.Add(macdArray[i]);
            }

            // Signal = MACD EMA-ja
            double[] signalArray = CalculateEMAOnArray(macdArray, signalPeriod);
            signalLine = signalArray.ToList();

            for (int i = 0; i < prices.Count; i++)
            {
                histogram.Add(macdArray[i] - signalArray[i]);
            }

            return (macdLine, signalLine, histogram);
        }

        // STOCHASTIC (Visszatér: %K, %D)
        public static (List<double>, List<double>) CalculateStoch(List<Candle> candles, int kPeriod, int dPeriod, int slowing)
        {
            var kLine = new List<double>();
            var dLine = new List<double>();

            if (candles == null || candles.Count < kPeriod)
                return (new List<double>(new double[candles?.Count ?? 0]), new List<double>(new double[candles?.Count ?? 0]));

            var rawK = new double[candles.Count];

            for (int i = 0; i < candles.Count; i++)
            {
                if (i < kPeriod - 1)
                {
                    rawK[i] = 50;
                    continue;
                }

                double highest = double.MinValue;
                double lowest = double.MaxValue;

                for (int j = 0; j < kPeriod; j++)
                {
                    var c = candles[i - j];
                    if ((double)c.High > highest) highest = (double)c.High;
                    if ((double)c.Low < lowest) lowest = (double)c.Low;
                }

                double currentClose = (double)candles[i].Close;
                if (highest == lowest) rawK[i] = 50;
                else rawK[i] = ((currentClose - lowest) / (highest - lowest)) * 100.0;
            }

            // Slowing (%K simítása)
            var finalK = CalculateSMAOnArray(rawK, slowing);
            kLine = finalK.ToList();

            // %D (%K simítása)
            var finalD = CalculateSMAOnArray(finalK, dPeriod);
            dLine = finalD.ToList();

            return (kLine, dLine);
        }

        // BOLLINGER (Visszatér: Middle, Upper, Lower)
        public static (List<double>, List<double>, List<double>) CalculateBollingerBands(List<double> prices, int period, double deviation)
        {
            var middle = new List<double>();
            var upper = new List<double>();
            var lower = new List<double>();

            if (prices.Count < period) return (middle, upper, lower);

            double[] sma = CalculateSMAOnArray(prices.ToArray(), period);
            middle = sma.ToList();

            for (int i = 0; i < prices.Count; i++)
            {
                if (i < period - 1)
                {
                    upper.Add(sma[i]);
                    lower.Add(sma[i]);
                    continue;
                }

                double sum = 0;
                for (int j = 0; j < period; j++)
                {
                    double val = prices[i - j];
                    sum += (val - sma[i]) * (val - sma[i]);
                }
                double stdDev = Math.Sqrt(sum / period);

                upper.Add(sma[i] + (stdDev * deviation));
                lower.Add(sma[i] - (stdDev * deviation));
            }

            return (middle, upper, lower);
        }

        public static double[] CalculateEMAOnArray(double[] values, int period)
        {
            if (values == null || values.Length == 0) return new double[0];

            double[] result = new double[values.Length];
            double multiplier = 2.0 / (period + 1);

            if (values.Length > 0) result[0] = values[0];

            for (int i = 1; i < values.Length; i++)
            {
                result[i] = ((values[i] - result[i - 1]) * multiplier) + result[i - 1];
            }
            return result;
        }

        public static double[] CalculateSMAOnArray(double[] values, int period)
        {
            if (values == null || values.Length == 0) return new double[0];

            double[] result = new double[values.Length];

            for (int i = 0; i < values.Length; i++)
            {
                if (i < period - 1)
                {
                    result[i] = values[i];
                    continue;
                }
                double sum = 0;
                for (int j = 0; j < period; j++)
                {
                    sum += values[i - j];
                }
                result[i] = sum / period;
            }
            return result;
        }
    }
}