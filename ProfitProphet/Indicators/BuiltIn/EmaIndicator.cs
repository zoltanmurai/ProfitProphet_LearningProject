using System;
using System.Collections.Generic;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using ProfitProphet.Indicators.Abstractions;
using ProfitProphet.Models.Charting;

namespace ProfitProphet.Indicators.BuiltIn
{
    public sealed class EmaIndicator : IIndicator
    {
        public string Id => "ema";
        public string DisplayName => "EMA";

        private static readonly List<IndicatorParamDef> _params = new()
        {
            new IndicatorParamDef { Name = "Period", Type = typeof(int),    DefaultValue = 20, Min = 1, Max = 1000 },
            new IndicatorParamDef { Name = "Source", Type = typeof(string), DefaultValue = "Close" }
        };
        public IReadOnlyList<IndicatorParamDef> Params => _params;

        public IndicatorResult Compute(IReadOnlyList<OhlcPoint> candles, IndicatorParams values)
        {
            int period = Convert.ToInt32(values.TryGetValue("Period", out var p) ? p : 20);
            string src = (values.TryGetValue("Source", out var s) ? Convert.ToString(s) : "Close") ?? "Close";

            var source = SelectSource(candles, src);      // ← OhlcPoint lista megy be
            var ema = ComputeEma(source, period);

            var r = new IndicatorResult();
            r.Series["ema"] = ema;
            return r;
        }

        public void Render(PlotModel model, IndicatorResult result, Axis xAxis, Axis yAxis)
        {
            if (!result.Series.TryGetValue("ema", out var ema)) return;

            var ls = new LineSeries
            {
                Title = "EMA",
                StrokeThickness = 1.5,
                LineStyle = LineStyle.Solid,
                XAxisKey = xAxis?.Key,
                YAxisKey = yAxis?.Key
            };

            for (int i = 0; i < ema.Length; i++)
                if (!double.IsNaN(ema[i]))
                    ls.Points.Add(new DataPoint(i, ema[i])); // index alapú X a CategoryAxis-hez

            model.Series.Add(ls);
        }

        // ---- OhlcPoint, nem Candle ----
        private static double[] SelectSource(IReadOnlyList<OhlcPoint> cs, string src)
        {
            var arr = new double[cs.Count];
            for (int i = 0; i < cs.Count; i++)
            {
                arr[i] = src switch
                {
                    "Open"  => cs[i].Open,
                    "High"  => cs[i].High,
                    "Low"   => cs[i].Low,
                    "HL2"   => (cs[i].High + cs[i].Low) / 2.0,
                    "HLC3"  => (cs[i].High + cs[i].Low + cs[i].Close) / 3.0,
                    "HLCC4" => (cs[i].High + cs[i].Low + 2.0 * cs[i].Close) / 4.0,
                    _       => cs[i].Close
                };
            }
            return arr;
        }

        private static double[] ComputeEma(double[] src, int period)
        {
            var output = new double[src.Length];
            if (period < 1) period = 1;
            double k = 2.0 / (period + 1.0);
            double ema = double.NaN;

            for (int i = 0; i < src.Length; i++)
            {
                if (double.IsNaN(ema)) ema = src[i];
                else ema = ema + (src[i] - ema) * k;

                output[i] = (i < period - 1) ? double.NaN : ema;
            }
            return output;
        }
    }
}
