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
    public sealed class BollingerIndicator : IIndicator
    {
        public string Id => "bb";
        public string DisplayName => "Bollinger Bands";

        // 2 paraméter: Period (int), Multiplier (double)
        private static readonly List<IndicatorParamDef> _params = new()
        {
            new IndicatorParamDef { Name = "Period", Type = typeof(int), DefaultValue = 20, Min = 1 },
            new IndicatorParamDef { Name = "Multiplier", Type = typeof(double), DefaultValue = 2.0, Min = 0.1 }
        };
        public IReadOnlyList<IndicatorParamDef> Params => _params;

        public IndicatorResult Compute(IReadOnlyList<OhlcPoint> candles, IndicatorParams values)
        {
            int period = Convert.ToInt32(values.TryGetValue("Period", out var p) ? p : 20);
            double mult = Convert.ToDouble(values.TryGetValue("Multiplier", out var m) ? m : 2.0);

            var prices = candles.Select(c => c.Close).ToList();
            var (mid, up, low) = IndicatorAlgorithms.CalculateBollingerBands(prices, period, mult);

            var r = new IndicatorResult();
            r.Series["upper"] = up.ToArray();
            r.Series["lower"] = low.ToArray();
            r.Series["middle"] = mid.ToArray();
            return r;
        }

        public void Render(PlotModel model, IndicatorResult result, Axis xAxis, Axis yAxis)
        {
            string xKey = xAxis?.Key;
            string yKey = yAxis?.Key;

            void AddLine(string key, OxyColor color, double thick)
            {
                if (result.Series.TryGetValue(key, out var obj) && obj is double[] arr)
                {
                    var ls = new LineSeries { Color = color, StrokeThickness = thick, XAxisKey = xKey, YAxisKey = yKey };
                    for (int i = 0; i < arr.Length; i++) if (!double.IsNaN(arr[i])) ls.Points.Add(new DataPoint(i, arr[i]));
                    model.Series.Add(ls);
                }
            }

            AddLine("upper", OxyColors.Yellow, 1.2);
            AddLine("lower", OxyColors.Yellow, 1.2);
            AddLine("middle", OxyColor.FromArgb(128, 255, 255, 0), 1.0); // Halványabb közép
        }
    }
}