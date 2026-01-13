using System;
using System.Collections.Generic;
using ProfitProphet.Entities;

namespace ProfitProphet.Services.Indicators
{
    public static class IndicatorAlgorithms
    {
        public static double[] CalculateSMA(List<Candle> candles, int period)
        {
            if (candles == null || candles.Count == 0) return new double[0];
            double[] result = new double[candles.Count];
            for (int i = 0; i < candles.Count; i++)
            {
                if (i < period - 1) { result[i] = 0; continue; }
                double sum = 0;
                for (int j = 0; j < period; j++) sum += (double)candles[i - j].Close;
                result[i] = sum / period;
            }
            return result;
        }

        public static double[] CalculateEMA(List<Candle> candles, int period)
        {
            if (candles == null || candles.Count == 0) return new double[0];

            double[] result = new double[candles.Count];
            double multiplier = 2.0 / (period + 1);

            double sum = 0;
            for (int i = 0; i < period && i < candles.Count; i++)
            {
                sum += (double)candles[i].Close;
            }
            if (period <= candles.Count) result[period - 1] = sum / period;

            for (int i = period; i < candles.Count; i++)
            {
                double close = (double)candles[i].Close;
                double prevEma = result[i - 1];

                result[i] = (close - prevEma) * multiplier + prevEma;
            }

            for (int i = 0; i < period - 1; i++) result[i] = 0;

            return result;
        }

        public static double[] CalculateSMAOnArray(double[] input, int period)
        {
            if (input == null || input.Length == 0) return new double[0];
            double[] result = new double[input.Length];
            for (int i = 0; i < input.Length; i++)
            {
                if (i < period - 1) { result[i] = 0; continue; }
                double sum = 0;
                for (int j = 0; j < period; j++) sum += input[i - j];
                result[i] = sum / period;
            }
            return result;
        }

        public static double[] CalculateCMF(List<Candle> candles, int period)
        {
            if (candles == null || candles.Count == 0) return new double[0];
            double[] result = new double[candles.Count];
            double[] mfVolume = new double[candles.Count];

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

            for (int i = 0; i < candles.Count; i++)
            {
                if (i < period - 1) { result[i] = 0; continue; }
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
    }
}