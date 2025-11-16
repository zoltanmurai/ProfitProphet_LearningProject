using System;
using System.Collections.Generic;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using ProfitProphet.Indicators.Abstractions;
using ProfitProphet.Models.Charting;

namespace ProfitProphet.Indicators.BuiltIn
{
    public sealed class SmaIndicator : IIndicator
    {
        public string Id => "sma";
        public string DisplayName => "SMA";

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

            var source = SelectSource(candles, src);      // OhlcPoint -> double[]
            var sma = ComputeSma(source, period);

            var r = new IndicatorResult();
            r.Series["sma"] = sma;
            return r;
        }

        public void Render(PlotModel model, IndicatorResult result, Axis xAxis, Axis yAxis)
        {
            if (!result.Series.TryGetValue("sma", out var sma))
                return;

            var ls = new LineSeries
            {
                Title = "SMA",
                StrokeThickness = 1.5,
                LineStyle = LineStyle.Dash,    // kicsit eltér az EMA-tól
                XAxisKey = xAxis?.Key,
                YAxisKey = yAxis?.Key
            };

            for (int i = 0; i < sma.Length; i++)
            {
                if (!double.IsNaN(sma[i]))
                    ls.Points.Add(new DataPoint(i, sma[i]));   // index alapú X (CategoryAxis)
            }

            model.Series.Add(ls);
        }

        // ---- forrás kiválasztása OhlcPoint-ból (ugyanaz, mint EMA-nál) ----
        private static double[] SelectSource(IReadOnlyList<OhlcPoint> cs, string src)
        {
            var arr = new double[cs.Count];
            for (int i = 0; i < cs.Count; i++)
            {
                arr[i] = src switch
                {
                    "Open" => cs[i].Open,
                    "High" => cs[i].High,
                    "Low" => cs[i].Low,
                    "HL2" => (cs[i].High + cs[i].Low) / 2.0,
                    "HLC3" => (cs[i].High + cs[i].Low + cs[i].Close) / 3.0,
                    "HLCC4" => (cs[i].High + cs[i].Low + 2.0 * cs[i].Close) / 4.0,
                    _ => cs[i].Close
                };
            }
            return arr;
        }

        private static double[] ComputeSma(double[] src, int period)
        {
            var output = new double[src.Length];
            if (period < 1) period = 1;

            double sum = 0.0;

            for (int i = 0; i < src.Length; i++)
            {
                sum += src[i];

                if (i >= period)
                    sum -= src[i - period];

                if (i >= period - 1)
                    output[i] = sum / period;
                else
                    output[i] = double.NaN;
            }

            return output;
        }
    }
}
