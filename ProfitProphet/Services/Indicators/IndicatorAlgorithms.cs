using ProfitProphet.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProfitProphet.Services.Indicators
{
    // Statikus osztály: bárki hívhatja (Chart vagy Backtest), nem kell példányosítani.
    public static class IndicatorAlgorithms
    {
        // 1. Sima Mozgóátlag (Gyertyákra)
        public static double[] CalculateSMA(List<Candle> candles, int period)
        {
            if (candles == null || candles.Count == 0) return new double[0];

            double[] result = new double[candles.Count];
            for (int i = 0; i < candles.Count; i++)
            {
                if (i < period - 1)
                {
                    result[i] = 0;
                    continue;
                }
                double sum = 0;
                for (int j = 0; j < period; j++) sum += (double)candles[i - j].Close;
                result[i] = sum / period;
            }
            return result;
        }

        // 2. Mozgóátlag Tömbre
        public static double[] CalculateSMAOnArray(double[] input, int period)
        {
            if (input == null || input.Length == 0) return new double[0];

            double[] result = new double[input.Length];
            for (int i = 0; i < input.Length; i++)
            {
                if (i < period - 1)
                {
                    result[i] = 0;
                    continue;
                }
                double sum = 0;
                for (int j = 0; j < period; j++) sum += input[i - j];
                result[i] = sum / period;
            }
            return result;
        }

        // 3. Chaikin Money Flow (CMF)
        public static double[] CalculateCMF(List<Candle> candles, int period)
        {
            if (candles == null || candles.Count == 0) return new double[0];

            double[] result = new double[candles.Count];
            double[] mfVolume = new double[candles.Count];

            // A) Előkészítés: Money Flow Volume kiszámolása minden gyertyára
            for (int i = 0; i < candles.Count; i++)
            {
                var c = candles[i];
                double high = (double)c.High;
                double low = (double)c.Low;
                double close = (double)c.Close;
                double volume = (double)c.Volume;

                double multiplier = 0;
                if (high - low != 0)
                {
                    multiplier = ((close - low) - (high - close)) / (high - low);
                }
                mfVolume[i] = multiplier * volume;
            }

            // B) Összegzés: Az elmúlt N nap összegeinek hányadosa
            for (int i = 0; i < candles.Count; i++)
            {
                if (i < period - 1)
                {
                    result[i] = 0;
                    continue;
                }

                double sumMfVol = 0;
                double sumVol = 0;

                for (int j = 0; j < period; j++)
                {
                    sumMfVol += mfVolume[i - j];
                    sumVol += (double)candles[i - j].Volume;
                }

                if (sumVol == 0) result[i] = 0;
                else result[i] = sumMfVol / sumVol;
            }

            return result;
        }
    }
}
