using ProfitProphet.Entities;
using ProfitProphet.Models.Backtesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProfitProphet.Services
{
    public class BacktestService
    {
        public BacktestResult RunBacktest(
            List<Candle> candles,
            int cmfPeriod,
            int cmfMaPeriod,
            int priceMaPeriod,
            double initialCash = 10000,
            decimal commissionRate = 0.002m)
        {
            var result = new BacktestResult { Symbol = candles.FirstOrDefault()?.Symbol ?? "?" };
            if (candles.Count < 100) return result;

            // 1. Adatok tömbösítése a gyorsasághoz
            var closes = candles.Select(c => (double)c.Close).ToArray();
            var highs = candles.Select(c => (double)c.High).ToArray();
            var lows = candles.Select(c => (double)c.Low).ToArray();
            var volumes = candles.Select(c => (double)(c.Volume ?? 0)).ToArray();

            // 2. Indikátorok kiszámítása
            var cmf = CalculateCmf(highs, lows, closes, volumes, cmfPeriod);
            var cmfMa = CalculateSma(cmf, cmfMaPeriod);
            var priceMa = CalculateSma(closes, priceMaPeriod);

            // 3. Szimuláció
            bool inPosition = false;
            decimal entryPrice = 0;
            DateTime entryDate = DateTime.MinValue;
            double cash = initialCash;
            double holdings = 0;

            // Drawdown & Score változók
            double peakEquity = initialCash;
            double maxDrawdownVal = 0; // abszolút értékben

            result.EquityCurve.Add(new EquityPoint { Time = candles[0].TimestampUtc, Equity = initialCash });

            for (int i = 1; i < candles.Count; i++)
            {
                double cmfPrev = cmf[i - 1];
                double cmfCurr = cmf[i];
                double cmfMaPrev = cmfMa[i - 1];
                double cmfMaCurr = cmfMa[i];
                double close = closes[i];
                double ma = priceMa[i];

                if (double.IsNaN(cmfPrev) || double.IsNaN(cmfMaPrev) || double.IsNaN(ma)) continue;

                // --- VBA LOGIKA ---
                // VÉTEL: CMF MA alulról metszi a CMF-et (CMF emelkedik) ÉS Ár > ÁrMA
                bool buySignal = (cmfMaPrev >= cmfPrev && cmfMaCurr <= cmfCurr) && (close >= ma);

                // ELADÁS: Ár beesik az ÁrMA alá
                bool sellSignal = (close < ma);

                // Végrehajtás
                if (!inPosition && buySignal)
                {
                    decimal price = candles[i].Close;
                    decimal effectiveCash = (decimal)cash * (1 - commissionRate);
                    decimal quantity = Math.Floor(effectiveCash / price);

                    if (quantity > 0)
                    {
                        entryPrice = price;
                        entryDate = candles[i].TimestampUtc;
                        holdings = (double)quantity;
                        cash -= (double)(quantity * price * (1 + commissionRate));
                        inPosition = true;
                    }
                }
                else if (inPosition && sellSignal)
                {
                    decimal price = candles[i].Close;
                    decimal revenue = (decimal)holdings * price;
                    decimal effectiveRevenue = revenue * (1 - commissionRate);
                    decimal costOfPosition = (decimal)holdings * entryPrice * (1 + commissionRate);

                    cash += (double)effectiveRevenue;

                    var trade = new TradeRecord
                    {
                        EntryDate = entryDate,
                        EntryPrice = entryPrice,
                        ExitDate = candles[i].TimestampUtc,
                        ExitPrice = price,
                        Profit = effectiveRevenue - costOfPosition,
                        Type = "Long"
                    };

                    if (trade.EntryPrice != 0)
                        trade.ProfitPercent = (trade.ExitPrice - trade.EntryPrice) / trade.EntryPrice * 100;

                    result.Trades.Add(trade);

                    result.EquityCurve.Add(new EquityPoint
                    {
                        Time = candles[i].TimestampUtc,
                        Equity = cash
                    });

                    inPosition = false;
                    holdings = 0;
                }

                // Drawdown követés
                double currentEquity = cash + (inPosition ? holdings * closes[i] : 0);
                if (currentEquity > peakEquity) peakEquity = currentEquity;

                double dd = peakEquity - currentEquity; // Abszolút drawdown összeg
                if (dd > maxDrawdownVal) maxDrawdownVal = dd;
            }

            // Eredmények
            double finalEquity = cash + (inPosition ? holdings * closes[candles.Count - 1] : 0);
            result.TotalProfitLoss = finalEquity - initialCash;
            result.TradeCount = result.Trades.Count;

            // Százalékos Max DD
            result.MaxDrawdown = initialCash > 0 ? (maxDrawdownVal / initialCash) * 100 : 0;

            result.WinRate = result.TradeCount > 0
                ? (double)result.Trades.Count(t => t.Profit > 0) / result.TradeCount * 100
                : 0;

            // Score számítás a VBA alapján: Profit - (0.5 * Abs(DD))
            // A VBA-ban: scoreRaw = PLAmount - K_DD * Abs(highestDD)
            // Itt K_DD = 0.5
            result.Score = result.TotalProfitLoss - (0.5 * maxDrawdownVal);

            return result;
        }

        // Segédszámítások
        private double[] CalculateCmf(double[] high, double[] low, double[] close, double[] volume, int period)
        {
            int n = high.Length;
            var cmf = new double[n];
            var mfv = new double[n];

            for (int i = 0; i < n; i++)
            {
                double range = high[i] - low[i];
                double multiplier = range == 0 ? 0 : ((close[i] - low[i]) - (high[i] - close[i])) / range;
                double vol = volume[i];
                mfv[i] = multiplier * vol;

                if (i >= period - 1)
                {
                    double sumMfv = 0;
                    double sumVol = 0;
                    for (int j = i - period + 1; j <= i; j++)
                    {
                        sumMfv += mfv[j];
                        sumVol += volume[j];
                    }
                    cmf[i] = sumVol == 0 ? 0 : sumMfv / sumVol;
                }
                else cmf[i] = double.NaN;
            }
            return cmf;
        }

        private double[] CalculateSma(double[] data, int period)
        {
            var sma = new double[data.Length];
            double sum = 0;
            for (int i = 0; i < data.Length; i++)
            {
                sum += double.IsNaN(data[i]) ? 0 : data[i];
                if (i >= period) sum -= double.IsNaN(data[i - period]) ? 0 : data[i - period];
                if (i >= period - 1) sma[i] = sum / period;
                else sma[i] = double.NaN;
            }
            return sma;
        }
    }
}