using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Series;
using ProfitProphet.Indicators.Abstractions;
using ProfitProphet.Models.Charting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProfitProphet.Indicators.BuiltIn
{
    public sealed class StochasticIndicator : IIndicator
    {
        public string Id => "stoch";
        public string DisplayName => "Stochastic";

        private static readonly List<IndicatorParamDef> _params = new()
        {
            new IndicatorParamDef { Name = "kPeriod",  Type = typeof(int),  DefaultValue = 14, Min = 1, Max = 1000 },
            new IndicatorParamDef { Name = "dPeriod",  Type = typeof(int),  DefaultValue = 3,  Min = 1, Max = 1000 },
            new IndicatorParamDef { Name = "outputD", Type = typeof(bool), DefaultValue = false }
        };
        public IReadOnlyList<IndicatorParamDef> Params => _params;

        public IndicatorResult Compute(IReadOnlyList<OhlcPoint> candles, IndicatorParams values)
        {
            int kPeriod = Convert.ToInt32(values.TryGetValue("kPeriod", out var kp) ? kp : 14);
            int dPeriod = Convert.ToInt32(values.TryGetValue("dPeriod", out var dp) ? dp : 3);
            bool outputD = values.TryGetValue("outputD", out var od) && Convert.ToBoolean(od);

            var k = ComputeK(candles, kPeriod);
            var d = ComputeSma(k, dPeriod);

            var r = new IndicatorResult();
            r.Series["k"] = k;          // %K

            if (outputD)
                r.Series["d"] = d;      // %D (only if requested)

            return r;
        }

        // IMPORTANT: We do NOT create axes here. We use the xAxis and yAxis provided by the ChartBuilder.
        public void Render(PlotModel model, IndicatorResult result, Axis xAxis, Axis yAxis)
        {
            if (!result.Series.TryGetValue("k", out var kVals))
                return;

            // Use the key of the provided axis (ChartBuilder manages the layout)
            string axisKey = yAxis.Key;

            // 1. Draw the background zones (Overbought/Oversold)
            // Note: We use RectangleAnnotation for background zoning
            var stochZone = new RectangleAnnotation
            {
                XAxisKey = xAxis?.Key,
                YAxisKey = axisKey,
                MinimumX = -0.5,
                MaximumX = kVals.Length - 0.5,
                MinimumY = 20, // Oversold zone
                MaximumY = 80, // Overbought zone
                Fill = OxyColor.FromArgb(10, 0, 255, 0), // Very light green hint
                Layer = AnnotationLayer.BelowSeries
            };
            model.Annotations.Add(stochZone);

            // 2. Draw %K Line
            var kSeries = new LineSeries
            {
                Title = $"%K",
                StrokeThickness = 1.5,
                Color = OxyColors.Cyan,
                LineStyle = LineStyle.Solid,
                XAxisKey = xAxis?.Key,
                YAxisKey = axisKey
            };

            for (int i = 0; i < kVals.Length; i++)
            {
                if (!double.IsNaN(kVals[i]))
                    kSeries.Points.Add(new DataPoint(i, kVals[i]));
            }

            model.Series.Add(kSeries);

            // 3. Draw %D Line (if exists)
            if (result.Series.TryGetValue("d", out var dVals))
            {
                var dSeries = new LineSeries
                {
                    Title = $"%D",
                    StrokeThickness = 1.2,
                    Color = OxyColors.Orange,
                    LineStyle = LineStyle.Dash,
                    XAxisKey = xAxis?.Key,
                    YAxisKey = axisKey
                };

                for (int i = 0; i < dVals.Length; i++)
                {
                    if (!double.IsNaN(dVals[i]))
                        dSeries.Points.Add(new DataPoint(i, dVals[i]));
                }

                model.Series.Add(dSeries);
            }
        }

        // --- Internal Calculations ---

        private static double[] ComputeK(IReadOnlyList<OhlcPoint> cs, int period)
        {
            int n = cs.Count;
            var k = new double[n];
            if (period < 1) period = 1;

            for (int i = 0; i < n; i++)
            {
                if (i < period - 1)
                {
                    k[i] = double.NaN;
                    continue;
                }

                int start = i - period + 1;
                double highestHigh = double.MinValue;
                double lowestLow = double.MaxValue;

                for (int j = start; j <= i; j++)
                {
                    if (cs[j].High > highestHigh) highestHigh = cs[j].High;
                    if (cs[j].Low < lowestLow) lowestLow = cs[j].Low;
                }

                double range = highestHigh - lowestLow;
                if (range <= 0)
                {
                    k[i] = 50.0;
                }
                else
                {
                    k[i] = (cs[i].Close - lowestLow) / range * 100.0;
                }
            }

            return k;
        }

        private static double[] ComputeSma(double[] src, int period)
        {
            int n = src.Length;
            var output = new double[n];
            if (period < 1) period = 1;

            double sum = 0.0;
            for (int i = 0; i < n; i++)
            {
                double v = double.IsNaN(src[i]) ? 0.0 : src[i];
                sum += v;

                if (i >= period)
                {
                    double old = double.IsNaN(src[i - period]) ? 0.0 : src[i - period];
                    sum -= old;
                }

                if (i >= period - 1)
                    output[i] = sum / period;
                else
                    output[i] = double.NaN;
            }
            return output;
        }
    }
}