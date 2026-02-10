using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using ProfitProphet.Indicators.Abstractions;
using ProfitProphet.Models.Charting;
using ProfitProphet.Services; // Itt van az IndicatorAlgorithms
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProfitProphet.Indicators.BuiltIn
{
    public sealed class MacdIndicator : IIndicator
    {
        public string Id => "macd";
        public string DisplayName => "MACD";

        // Paraméterek definíciója (Fast, Slow, Signal)
        private static readonly List<IndicatorParamDef> _params = new()
        {
            new IndicatorParamDef { Name = "FastPeriod", Type = typeof(int), DefaultValue = 12, Min = 1 },
            new IndicatorParamDef { Name = "SlowPeriod", Type = typeof(int), DefaultValue = 26, Min = 1 },
            new IndicatorParamDef { Name = "SignalPeriod", Type = typeof(int), DefaultValue = 9, Min = 1 }
        };
        public IReadOnlyList<IndicatorParamDef> Params => _params;

        public IndicatorResult Compute(IReadOnlyList<OhlcPoint> candles, IndicatorParams values)
        {
            // 1. Paraméterek kiolvasása biztonságosan
            int fast = values.TryGetValue("FastPeriod", out var f) ? Convert.ToInt32(f) : 12;
            int slow = values.TryGetValue("SlowPeriod", out var s) ? Convert.ToInt32(s) : 26;
            int sig = values.TryGetValue("SignalPeriod", out var sg) ? Convert.ToInt32(sg) : 9;

            // 2. Árak kinyerése
            // Fontos: explicit double konverzió
            var prices = candles.Select(c => (double)c.Close).ToList();

            // 3. Számítás az új algoritmussal (Tuple visszatérés)
            var (macd, signal, hist) = IndicatorAlgorithms.CalculateMACD(prices, fast, slow, sig);

            // 4. Eredmény összeállítása
            var r = new IndicatorResult();
            r.Series["macd"] = macd.ToArray();
            r.Series["signal"] = signal.ToArray();
            r.Series["hist"] = hist.ToArray();

            return r;
        }

        public void Render(PlotModel model, IndicatorResult result, Axis xAxis, Axis yAxis)
        {
            string xKey = xAxis?.Key;
            string yKey = yAxis?.Key;

            // 1. Hisztogram (StemSeries - Pálcika diagram szebb, mint a pöttyös vonal)
            if (result.Series.TryGetValue("hist", out var hObj) && hObj is double[] hist)
            {
                // Ha inkább a régi pöttyös vonal kell, cseréld vissza LineSeries-re!
                var stemSeries = new StemSeries
                {
                    Title = "Histogram",
                    Color = OxyColors.Gray,
                    StrokeThickness = 1,
                    XAxisKey = xKey,
                    YAxisKey = yKey,
                    TrackerFormatString = "{0}\n{1}: {2:0.###}\n{3}: {4:0.###}" // Hogy szépen írja ki az értéket
                };

                for (int i = 0; i < hist.Length; i++)
                {
                    // Csak akkor rajzoljuk, ha nem NaN (bár az algo 0-t ad vissza az elején)
                    if (!double.IsNaN(hist[i]))
                    {
                        stemSeries.Points.Add(new DataPoint(i, hist[i]));
                    }
                }
                model.Series.Add(stemSeries);
            }

            // 2. MACD vonal (Fehér)
            if (result.Series.TryGetValue("macd", out var mObj) && mObj is double[] macd)
            {
                var ms = new LineSeries
                {
                    Title = "MACD",
                    Color = OxyColors.White,
                    StrokeThickness = 1.5,
                    XAxisKey = xKey,
                    YAxisKey = yKey
                };
                for (int i = 0; i < macd.Length; i++)
                {
                    if (!double.IsNaN(macd[i]))
                        ms.Points.Add(new DataPoint(i, macd[i]));
                }
                model.Series.Add(ms);
            }

            // 3. Signal vonal (Piros)
            if (result.Series.TryGetValue("signal", out var sObj) && sObj is double[] signal)
            {
                var ss = new LineSeries
                {
                    Title = "Signal",
                    Color = OxyColors.Red,
                    StrokeThickness = 1.5,
                    XAxisKey = xKey,
                    YAxisKey = yKey
                };
                for (int i = 0; i < signal.Length; i++)
                {
                    if (!double.IsNaN(signal[i]))
                        ss.Points.Add(new DataPoint(i, signal[i]));
                }
                model.Series.Add(ss);
            }
        }
    }
}