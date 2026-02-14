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
    public sealed class BollingerIndicator : IIndicator
    {
        public string Id => "bb";
        public string DisplayName => "Bollinger Bands";

        // Two parameters: Period (int), Multiplier (double)
        private static readonly List<IndicatorParamDef> _params = new()
        {
            new IndicatorParamDef { Name = "Period", Type = typeof(int), DefaultValue = 20, Min = 1 },
            new IndicatorParamDef { Name = "Multiplier", Type = typeof(double), DefaultValue = 2.0, Min = 0.1 }
        };
        public IReadOnlyList<IndicatorParamDef> Params => _params;

        // Compute indicator values from OHLC points and provided parameters
        public IndicatorResult Compute(IReadOnlyList<OhlcPoint> candles, IndicatorParams values)
        {
            // Read parameters with defaults
            int period = values.TryGetValue("Period", out var p) ? Convert.ToInt32(p) : 20;
            double mult = values.TryGetValue("Multiplier", out var m) ? Convert.ToDouble(m) : 2.0;

            // Convert close prices to double (important cast)
            var prices = candles.Select(c => (double)c.Close).ToList();

            // Calculate bands (returns order: middle, upper, lower)
            var (mid, up, low) = IndicatorAlgorithms.CalculateBollingerBands(prices, period, mult);

            // Populate result series
            var r = new IndicatorResult();
            r.Series["upper"] = up.ToArray();
            r.Series["lower"] = low.ToArray();
            r.Series["middle"] = mid.ToArray();
            return r;
        }

        // Render computed series on the provided PlotModel using given axes
        public void Render(PlotModel model, IndicatorResult result, Axis xAxis, Axis yAxis)
        {
            string xKey = xAxis?.Key;
            string yKey = yAxis?.Key;

            // Helper: add a line series for a named series in the result
            void AddLineSeries(string key, string title, OxyColor color, double thickness, LineStyle style = LineStyle.Solid)
            {
                if (result.Series.TryGetValue(key, out var obj) && obj is double[] arr)
                {
                    var ls = new LineSeries
                    {
                        Title = title,
                        Color = color,
                        StrokeThickness = thickness,
                        LineStyle = style,
                        XAxisKey = xKey,
                        YAxisKey = yKey
                    };

                    // Add points, skip NaN entries
                    for (int i = 0; i < arr.Length; i++)
                    {
                        if (!double.IsNaN(arr[i]))
                        {
                            ls.Points.Add(new DataPoint(i, arr[i]));
                        }
                    }
                    model.Series.Add(ls);
                }
            }

            // Upper band (gold)
            AddLineSeries("upper", "Upper Band", OxyColors.Gold, 1.2);

            // Lower band (gold)
            AddLineSeries("lower", "Lower Band", OxyColors.Gold, 1.2);

            // Middle band (SMA) - lighter/thinner dashed line
            AddLineSeries("middle", "Middle Band", OxyColor.FromArgb(150, 255, 215, 0), 1.0, LineStyle.Dash);
        }
    }
}