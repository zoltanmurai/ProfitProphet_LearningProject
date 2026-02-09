using ProfitProphet.Entities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProfitProphet.Services.Indicators
{
    public static class IndicatorAlgorithms
    {
        // --- SMA (Simple Moving Average) ---

        public static double[] CalculateSMA(List<Candle> candles, int period)
        {
            var prices = candles.Select(c => (double)c.Close).ToArray();
            return CalculateSMA(prices, period);
        }

        public static double[] CalculateSMA(double[] prices, int period)
        {
            var result = new double[prices.Length];
            // Alapból töltsük fel NaN-nal
            for (int i = 0; i < result.Length; i++) result[i] = double.NaN;

            if (prices.Length < period) return result;

            // 1. Keressük meg az első VALÓS adatot (kihagyjuk az elején lévő NaN-okat)
            int firstValidIndex = -1;
            for (int i = 0; i < prices.Length; i++)
            {
                if (!double.IsNaN(prices[i]))
                {
                    firstValidIndex = i;
                    break;
                }
            }

            // Ha nincs elég adat a NaN-ok után, visszatérünk
            if (firstValidIndex == -1 || (prices.Length - firstValidIndex) < period)
                return result;

            // 2. Az első SMA érték kiszámítása
            double sum = 0;
            for (int i = 0; i < period; i++)
            {
                sum += prices[firstValidIndex + i];
            }

            int seedIndex = firstValidIndex + period - 1;
            result[seedIndex] = sum / period;

            // 3. Rolling SMA (csúszó ablak)
            for (int i = seedIndex + 1; i < prices.Length; i++)
            {
                // Kivonjuk a kiesőt és hozzáadjuk az újat
                double newVal = prices[i];
                double oldVal = prices[i - period];

                if (double.IsNaN(newVal))
                {
                    result[i] = double.NaN;
                    // Ha lyuk van az adatokban, a sum érvénytelenné válik
                    sum = double.NaN;
                }
                else if (double.IsNaN(sum))
                {
                    // Ha a sum már érvénytelen, nem tudunk tovább számolni egyszerűen
                    result[i] = double.NaN;
                }
                else
                {
                    sum += newVal - oldVal;
                    result[i] = sum / period;
                }
            }
            return result;
        }

        // --- CalculateSMAOnArray (VISSZAKERÜLT!) ---
        // Ez a metódus kell a CMF_MA és egyéb számításokhoz.
        // Mivel a fenti CalculateSMA már kezeli a double[] tömböket és a NaN-okat,
        // egyszerűen meghívjuk azt.
        public static double[] CalculateSMAOnArray(double[] input, int period)
        {
            return CalculateSMA(input, period);
        }

        // --- EMA (Exponential Moving Average) ---

        public static double[] CalculateEMA(List<Candle> candles, int period)
        {
            var prices = candles.Select(c => (double)c.Close).ToArray();
            return CalculateEMA(prices, period);
        }

        public static double[] CalculateEMA(double[] prices, int period)
        {
            var result = new double[prices.Length];
            for (int i = 0; i < result.Length; i++) result[i] = double.NaN;

            if (prices.Length < period) return result;

            double k = 2.0 / (period + 1);

            // 1. Keressük meg az első VALÓS adatot
            int firstValidIndex = -1;
            for (int i = 0; i < prices.Length; i++)
            {
                if (!double.IsNaN(prices[i]))
                {
                    firstValidIndex = i;
                    break;
                }
            }

            if (firstValidIndex == -1 || (prices.Length - firstValidIndex) < period)
                return result;

            // 2. Az első EMA érték egy SMA (seed)
            double sum = 0;
            for (int i = 0; i < period; i++)
            {
                sum += prices[firstValidIndex + i];
            }

            int seedIndex = firstValidIndex + period - 1;
            result[seedIndex] = sum / period;

            // 3. EMA számítás
            for (int i = seedIndex + 1; i < prices.Length; i++)
            {
                double price = prices[i];
                double prevEma = result[i - 1];

                if (double.IsNaN(price) || double.IsNaN(prevEma))
                {
                    result[i] = double.NaN;
                }
                else
                {
                    result[i] = (price - prevEma) * k + prevEma;
                }
            }

            return result;
        }

        // --- MACD ---

        public static (List<double>, List<double>, List<double>) CalculateMACD(
            List<double> pricesList, int fastPeriod = 12, int slowPeriod = 26, int signalPeriod = 9)
        {
            double[] prices = pricesList.ToArray();

            // Itt már a javított EMA hívódik meg, ami kezeli a NaN-okat
            var fastEma = CalculateEMA(prices, fastPeriod);
            var slowEma = CalculateEMA(prices, slowPeriod);

            var macdLine = new double[prices.Length];
            for (int i = 0; i < prices.Length; i++)
            {
                if (double.IsNaN(fastEma[i]) || double.IsNaN(slowEma[i]))
                    macdLine[i] = double.NaN;
                else
                    macdLine[i] = fastEma[i] - slowEma[i];
            }

            // A signalLine számítása a macdLine-ból történik.
            // Mivel a CalculateEMA most már megkeresi az első nem-NaN értéket a tömbben,
            // helyesen fogja számolni a signált a "lyukas" macdLine-ból is.
            var signalLine = CalculateEMA(macdLine, signalPeriod);

            var histogram = new List<double>();
            for (int i = 0; i < prices.Length; i++)
            {
                if (double.IsNaN(macdLine[i]) || double.IsNaN(signalLine[i]))
                    histogram.Add(double.NaN);
                else
                    histogram.Add(macdLine[i] - signalLine[i]);
            }

            return (macdLine.ToList(), signalLine.ToList(), histogram);
        }

        // --- CMF (Chaikin Money Flow) ---

        public static double[] CalculateCMF(List<Candle> candles, int period)
        {
            if (candles == null || candles.Count == 0) return new double[0];

            double[] result = new double[candles.Count];
            double[] mfVolume = new double[candles.Count];

            // 1. Money Flow Volume számítása
            for (int i = 0; i < candles.Count; i++)
            {
                var c = candles[i];
                double high = (double)c.High;
                double low = (double)c.Low;
                double close = (double)c.Close;
                double volume = (double)c.Volume;

                double multiplier = (high - low == 0) ? 0 : ((close - low) - (high - close)) / (high - low);
                mfVolume[i] = multiplier * volume;
            }

            // 2. Rolling Sum
            for (int i = 0; i < candles.Count; i++)
            {
                if (i < period - 1)
                {
                    result[i] = double.NaN;
                    continue;
                }

                double sumMfVol = 0;
                double sumVol = 0;
                for (int j = 0; j < period; j++)
                {
                    sumMfVol += mfVolume[i - j];
                    sumVol += (double)candles[i - j].Volume;
                }

                result[i] = (sumVol == 0) ? 0 : sumMfVol / sumVol;
            }
            return result;
        }

        // --- RSI ---

        public static List<double> CalculateRSI(List<double> prices, int period)
        {
            var rsi = new double[prices.Count];
            for (int i = 0; i < rsi.Length; i++) rsi[i] = double.NaN;

            if (prices.Count < period + 1) return rsi.ToList();

            double gain = 0.0;
            double loss = 0.0;

            // Első átlag
            for (int i = 1; i <= period; i++)
            {
                double diff = prices[i] - prices[i - 1];
                if (diff > 0) gain += diff;
                else loss -= diff;
            }

            double avgGain = gain / period;
            double avgLoss = loss / period;

            rsi[period] = 100.0 - (100.0 / (1.0 + (avgLoss == 0 ? 9999 : avgGain / avgLoss)));

            // Smoothed számítás
            for (int i = period + 1; i < prices.Count; i++)
            {
                double diff = prices[i] - prices[i - 1];
                if (diff > 0)
                {
                    avgGain = (avgGain * (period - 1) + diff) / period;
                    avgLoss = (avgLoss * (period - 1)) / period;
                }
                else
                {
                    avgGain = (avgGain * (period - 1)) / period;
                    avgLoss = (avgLoss * (period - 1) - diff) / period;
                }

                double rs = (avgLoss == 0) ? 9999 : (avgGain / avgLoss);
                rsi[i] = 100.0 - (100.0 / (1.0 + rs));
            }

            return rsi.ToList();
        }

        // --- BOLLINGER BANDS ---

        public static (List<double>, List<double>, List<double>) CalculateBollingerBands(
            List<double> pricesList, int period, double multiplier = 2.0)
        {
            double[] prices = pricesList.ToArray();

            // Használjuk a javított SMA-t
            var sma = CalculateSMA(prices, period);

            var upper = new List<double>(new double[prices.Length]);
            var lower = new List<double>(new double[prices.Length]);

            for (int i = 0; i < prices.Length; i++)
            {
                if (double.IsNaN(sma[i]))
                {
                    upper[i] = double.NaN;
                    lower[i] = double.NaN;
                    continue;
                }

                double sum = 0.0;
                for (int j = 0; j < period; j++)
                {
                    sum += Math.Pow(prices[i - j] - sma[i], 2);
                }
                double stdDev = Math.Sqrt(sum / period);

                upper[i] = sma[i] + (stdDev * multiplier);
                lower[i] = sma[i] - (stdDev * multiplier);
            }

            return (sma.ToList(), upper, lower);
        }
    }
}