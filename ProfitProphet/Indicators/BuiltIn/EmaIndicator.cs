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

        //  Define parameters: Period (int) and Source (string) with defaults and constraints
        private static readonly List<IndicatorParamDef> _params = new()
        {
            new IndicatorParamDef { Name = "Period", Type = typeof(int),    DefaultValue = 20, Min = 1, Max = 1000 },
            new IndicatorParamDef { Name = "Source", Type = typeof(string), DefaultValue = "Close" }
        };
        public IReadOnlyList<IndicatorParamDef> Params => _params;

        // Compute: calculate EMA series from OHLC points and parameter values
        public IndicatorResult Compute(IReadOnlyList<OhlcPoint> candles, IndicatorParams values)
        {
            // Read period parameter with default
            int period = Convert.ToInt32(values.TryGetValue("Period", out var p) ? p : 20);
            // Read source parameter (e.g., "Close") with default
            string src = (values.TryGetValue("Source", out var s) ? Convert.ToString(s) : "Close") ?? "Close";

            // Select numeric series from OhlcPoint list based on source selection
            var source = SelectSource(candles, src);
            // Compute EMA on the selected numeric series
            var ema = ComputeEma(source, period);

            // Populate result
            var r = new IndicatorResult();
            r.Series["ema"] = ema;
            return r;
        }

        // Render: draw EMA line on provided PlotModel using given axes
        public void Render(PlotModel model, IndicatorResult result, Axis xAxis, Axis yAxis)
        {
            if (!result.Series.TryGetValue("ema", out var ema)) return;

            // Create LineSeries for EMA with styling and axis keys
            var ls = new LineSeries
            {
                Title = "EMA",
                StrokeThickness = 1.5,
                LineStyle = LineStyle.Solid,
                XAxisKey = xAxis?.Key,
                YAxisKey = yAxis?.Key
            };

            // Add points skipping NaN; X uses index-based values for CategoryAxis
            for (int i = 0; i < ema.Length; i++)
                if (!double.IsNaN(ema[i]))
                    ls.Points.Add(new DataPoint(i, ema[i])); // index-based X for CategoryAxis

            model.Series.Add(ls);
        }

        // ---- SelectSource: pick numeric source array from OhlcPoint list (not Candle) ----
        private static double[] SelectSource(IReadOnlyList<OhlcPoint> cs, string src)
        {
            var arr = new double[cs.Count];
            for (int i = 0; i < cs.Count; i++)
            {
                // Map named source to numeric value (default = Close)
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

        // ComputeEma: simple EMA implementation returning array aligned with input length
        private static double[] ComputeEma(double[] src, int period)
        {
            var output = new double[src.Length];
            if (period < 1) period = 1;
            double k = 2.0 / (period + 1.0);
            double ema = double.NaN;

            // Loop through source values to calculate EMA; first value initializes EMA, then apply smoothing
            for (int i = 0; i < src.Length; i++)
            {
                // Initialize EMA with first value, then apply smoothing formula
                if (double.IsNaN(ema)) ema = src[i];
                else ema = ema + (src[i] - ema) * k;

                // Mark values before warmup as NaN
                output[i] = (i < period - 1) ? double.NaN : ema;
            }
            return output;
        }
    }
}
