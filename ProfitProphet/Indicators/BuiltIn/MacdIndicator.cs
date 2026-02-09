using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using ProfitProphet.Indicators.Abstractions;
using ProfitProphet.Models.Charting;
using ProfitProphet.Services.Indicators;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProfitProphet.Indicators.BuiltIn
{
    public sealed class MacdIndicator : IIndicator
    {
        public string Id => "macd";
        public string DisplayName => "MACD";

        private static readonly List<IndicatorParamDef> _params = new()
        {
            new IndicatorParamDef { Name = "FastPeriod", Type = typeof(int), DefaultValue = 12, Min = 1 },
            new IndicatorParamDef { Name = "SlowPeriod", Type = typeof(int), DefaultValue = 26, Min = 1 },
            new IndicatorParamDef { Name = "SignalPeriod", Type = typeof(int), DefaultValue = 9, Min = 1 }
        };
        public IReadOnlyList<IndicatorParamDef> Params => _params;

        public IndicatorResult Compute(IReadOnlyList<OhlcPoint> candles, IndicatorParams values)
        {
            int fast = Convert.ToInt32(values.TryGetValue("FastPeriod", out var f) ? f : 12);
            int slow = Convert.ToInt32(values.TryGetValue("SlowPeriod", out var s) ? s : 26);
            int sig = Convert.ToInt32(values.TryGetValue("SignalPeriod", out var sg) ? sg : 9);

            var prices = candles.Select(c => c.Close).ToList();
            var (macd, signal, hist) = IndicatorAlgorithms.CalculateMACD(prices, fast, slow, sig);

            // ✅ Az algoritmus már helyesen kezeli a NaN értékeket, 
            //    nem kell felülírni semmit!

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

            // 1. Hisztogram (LineSeries)
            if (result.Series.TryGetValue("hist", out var hObj) && hObj is double[] hist)
            {
                var hs = new LineSeries
                {
                    Title = "Histogram",
                    Color = OxyColors.Gray,
                    StrokeThickness = 1,
                    LineStyle = LineStyle.Dot,
                    XAxisKey = xKey,
                    YAxisKey = yKey
                };
                for (int i = 0; i < hist.Length; i++)
                    if (!double.IsNaN(hist[i]))
                        hs.Points.Add(new DataPoint(i, hist[i]));
                model.Series.Add(hs);
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
                    if (!double.IsNaN(macd[i]))
                        ms.Points.Add(new DataPoint(i, macd[i]));
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
                    if (!double.IsNaN(signal[i]))
                        ss.Points.Add(new DataPoint(i, signal[i]));
                model.Series.Add(ss);
            }
        }
    }
}