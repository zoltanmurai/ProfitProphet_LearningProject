using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using ProfitProphet.Indicators.Abstractions;
using ProfitProphet.Models.Charting;
using ProfitProphet.Services; // for IndicatorAlgorithms
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProfitProphet.Indicators.BuiltIn
{
    public sealed class MacdIndicator : IIndicator
    {
        public string Id => "macd";
        public string DisplayName => "MACD";

        // Parameter definitions (Fast, Slow, Signal)
        private static readonly List<IndicatorParamDef> _params = new()
        {
            new IndicatorParamDef { Name = "FastPeriod", Type = typeof(int), DefaultValue = 12, Min = 1 },
            new IndicatorParamDef { Name = "SlowPeriod", Type = typeof(int), DefaultValue = 26, Min = 1 },
            new IndicatorParamDef { Name = "SignalPeriod", Type = typeof(int), DefaultValue = 9, Min = 1 }
        };
        public IReadOnlyList<IndicatorParamDef> Params => _params;

        // Compute MACD, signal and histogram arrays from OHLC points
        public IndicatorResult Compute(IReadOnlyList<OhlcPoint> candles, IndicatorParams values)
        {
            // 1) Read parameters with safe defaults
            int fast = values.TryGetValue("FastPeriod", out var f) ? Convert.ToInt32(f) : 12;
            int slow = values.TryGetValue("SlowPeriod", out var s) ? Convert.ToInt32(s) : 26;
            int sig = values.TryGetValue("SignalPeriod", out var sg) ? Convert.ToInt32(sg) : 9;

            // 2) Extract close prices (explicit double cast required)
            var prices = candles.Select(c => (double)c.Close).ToList();

            // 3) Calculate MACD, signal and histogram (tuple return)
            var (macd, signal, hist) = IndicatorAlgorithms.CalculateMACD(prices, fast, slow, sig);

            // 4) Assemble result series
            var r = new IndicatorResult();
            r.Series["macd"] = macd.ToArray();
            r.Series["signal"] = signal.ToArray();
            r.Series["hist"] = hist.ToArray();

            return r;
        }

        // Render MACD series and histogram on the plot using provided axes
        public void Render(PlotModel model, IndicatorResult result, Axis xAxis, Axis yAxis)
        {
            string xKey = xAxis?.Key; // X axis key (may be null)
            string yKey = yAxis?.Key; // Y axis key

            // 1) Histogram (StemSeries - bar-style looks nicer than dotted line)
            if (result.Series.TryGetValue("hist", out var hObj) && hObj is double[] hist)
            {
                var stemSeries = new StemSeries
                {
                    Title = "Histogram",
                    Color = OxyColors.Gray,
                    StrokeThickness = 1,
                    XAxisKey = xKey,
                    YAxisKey = yKey,
                    TrackerFormatString = "{0}\n{1}: {2:0.###}\n{3}: {4:0.###}" // format displayed value nicely
                };

                // Add non-NaN histogram points
                for (int i = 0; i < hist.Length; i++)
                {
                    if (!double.IsNaN(hist[i]))
                    {
                        stemSeries.Points.Add(new DataPoint(i, hist[i]));
                    }
                }
                model.Series.Add(stemSeries);
            }

            // 2) MACD line (white)
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
                // Add non-NaN MACD points
                for (int i = 0; i < macd.Length; i++)
                {
                    if (!double.IsNaN(macd[i]))
                        ms.Points.Add(new DataPoint(i, macd[i]));
                }
                model.Series.Add(ms);
            }

            // 3) Signal line (red)
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
                // Add non-NaN signal points
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