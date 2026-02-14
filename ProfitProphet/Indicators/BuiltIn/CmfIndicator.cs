using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using OxyPlot.Annotations;
using ProfitProphet.Indicators.Abstractions;
using ProfitProphet.Models.Charting;
using System;
using System.Collections.Generic;

namespace ProfitProphet.Indicators.BuiltIn
{
    public sealed class CmfIndicator : IIndicator
    {
        public string Id => "cmf";
        public string DisplayName => "Chaikin Money Flow";

        // Parameter definitions: Period and optional moving-average period
        private static readonly List<IndicatorParamDef> _params = new()
        {
            new IndicatorParamDef { Name = "Period", Type = typeof(int), DefaultValue = 20, Min = 1, Max = 100 },
            new IndicatorParamDef { Name = "MaPeriod", Type = typeof(int), DefaultValue = 0, Min = 0, Max = 100 }
        };
        public IReadOnlyList<IndicatorParamDef> Params => _params;

        // Compute CMF values and optional signal MA from OHLC points
        public IndicatorResult Compute(IReadOnlyList<OhlcPoint> candles, IndicatorParams values)
        {
            // Read parameters with defaults
            int period = Convert.ToInt32(values.TryGetValue("Period", out var p) ? p : 20);
            int maPeriod = Convert.ToInt32(values.TryGetValue("MaPeriod", out var mp) ? mp : 0);

            // Calculate CMF series
            var cmf = ComputeCmf(candles, period);

            // DEBUG: print last few values to output window for verification
            int count = cmf.Length;
            System.Diagnostics.Debug.WriteLine($"[CMF CHECK] Total candles: {count}");

            if (count >= 3)
            {
                for (int i = count - 3; i < count; i++)
                {
                    double vol = candles[i].Volume;
                    double val = cmf[i];
                    System.Diagnostics.Debug.WriteLine($"[CMF CHECK] Index: {i} | Volume input: {vol} | CMF Calculated: {val}");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[CMF CHECK] Not enough data to calculate.");
            }

            // Populate result series
            var r = new IndicatorResult();
            r.Series["cmf"] = cmf;

            // If MA period specified, compute and add signal series
            if (maPeriod > 0)
            {
                r.Series["signal"] = ComputeSma(cmf, maPeriod);
            }

            return r;
        }

        // Render CMF and optional signal on provided PlotModel using the given Y-axis
        public void Render(
                        PlotModel model,
                        IndicatorResult result,
                        Axis xAxis,      // intentionally not used
                        Axis yAxis       // provided by ChartBuilder (CMF sub-axis)
                    )
        {
            if (model == null || yAxis == null) return;
            if (!result.Series.TryGetValue("cmf", out var cmfVals)) return;

            string yKey = yAxis.Key;

            // 1) Zero reference line (CMF = 0)
            var zeroLine = new LineAnnotation
            {
                Type = LineAnnotationType.Horizontal,
                Y = 0,
                Color = OxyColors.Gray,
                LineStyle = LineStyle.Dash,
                StrokeThickness = 1,
                YAxisKey = yKey,
                Layer = AnnotationLayer.BelowSeries
            };
            model.Annotations.Add(zeroLine);

            // 2) Main CMF line
            var cmfSeries = new LineSeries
            {
                Title = "CMF",
                Color = OxyColor.FromRgb(0, 255, 128),
                StrokeThickness = 1.5,
                YAxisKey = yKey
            };

            // Add CMF points, skipping NaN values
            for (int i = 0; i < cmfVals.Length; i++)
            {
                double v = cmfVals[i];
                if (!double.IsNaN(v))
                    cmfSeries.Points.Add(new DataPoint(i, v));
            }

            model.Series.Add(cmfSeries);

            // 3) Optional signal MA (dashed orange line)
            if (result.Series.TryGetValue("signal", out var sigVals))
            {
                var sigSeries = new LineSeries
                {
                    Title = "CMF Signal",
                    Color = OxyColors.Orange,
                    StrokeThickness = 1.0,
                    LineStyle = LineStyle.Dash,
                    YAxisKey = yKey
                };

                // Add signal points, skipping NaN values
                for (int i = 0; i < sigVals.Length; i++)
                {
                    double v = sigVals[i];
                    if (!double.IsNaN(v))
                        sigSeries.Points.Add(new DataPoint(i, v));
                }

                model.Series.Add(sigSeries);
            }
        }


        // Compute Chaikin Money Flow series for given OHLC points and period
        private static double[] ComputeCmf(IReadOnlyList<OhlcPoint> cs, int period)
        {
            int n = cs.Count;
            var cmf = new double[n];
            var mfv = new double[n];
            var vol = new double[n];

            for (int i = 0; i < n; i++)
            {
                double high = cs[i].High;
                double low = cs[i].Low;
                double close = cs[i].Close;
                double volume = cs[i].Volume;

                double range = high - low;
                double multiplier = range == 0 ? 0 : ((close - low) - (high - close)) / range;

                mfv[i] = multiplier * volume;
                vol[i] = volume;

                if (i >= period - 1)
                {
                    double sumMfv = 0;
                    double sumVol = 0;
                    for (int j = i - period + 1; j <= i; j++)
                    {
                        sumMfv += mfv[j];
                        sumVol += vol[j];
                    }
                    cmf[i] = sumVol == 0 ? 0 : sumMfv / sumVol;
                }
                else
                {
                    cmf[i] = double.NaN;
                }
            }
            return cmf;
        }

        // Compute simple moving average for the provided source array
        private static double[] ComputeSma(double[] src, int period)
        {
            var outArr = new double[src.Length];
            double sum = 0;
            for (int i = 0; i < src.Length; i++)
            {
                double v = double.IsNaN(src[i]) ? 0 : src[i];
                sum += v;
                if (i >= period) sum -= double.IsNaN(src[i - period]) ? 0 : src[i - period];

                if (i >= period - 1) outArr[i] = sum / period;
                else outArr[i] = double.NaN;
            }
            return outArr;
        }
    }
}