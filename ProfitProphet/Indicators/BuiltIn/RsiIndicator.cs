using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using OxyPlot.Annotations;
using ProfitProphet.Indicators.Abstractions;
using ProfitProphet.Models.Charting;
using ProfitProphet.Services.Indicators;
using System;
using System.Collections.Generic;
using System.Linq;
using ProfitProphet.Services;

namespace ProfitProphet.Indicators.BuiltIn
{
    public sealed class RsiIndicator : IIndicator
    {
        public string Id => "rsi";
        public string DisplayName => "RSI";

        // 1 paraméter: Period
        private static readonly List<IndicatorParamDef> _params = new()
        {
            new IndicatorParamDef { Name = "Period", Type = typeof(int), DefaultValue = 14, Min = 1, Max = 1000 }
        };
        public IReadOnlyList<IndicatorParamDef> Params => _params;

        public IndicatorResult Compute(IReadOnlyList<OhlcPoint> candles, IndicatorParams values)
        {
            int period = Convert.ToInt32(values.TryGetValue("Period", out var p) ? p : 14);

            // Adatok kinyerése és számítás
            var prices = candles.Select(c => c.Close).ToList();
            var rsiData = IndicatorAlgorithms.CalculateRSI(prices, period); // List<double>

            var r = new IndicatorResult();
            r.Series["rsi"] = rsiData.ToArray(); // Konvertáljuk tömbbé
            return r;
        }

        public void Render(PlotModel model, IndicatorResult result, Axis xAxis, Axis yAxis)
        {
            if (!result.Series.TryGetValue("rsi", out var valObj) || valObj is not double[] rsi) return;

            // Szintek (30/70)
            model.Annotations.Add(new LineAnnotation { Y = 30, Color = OxyColors.Gray, LineStyle = LineStyle.Dash, YAxisKey = yAxis.Key, Layer = AnnotationLayer.BelowSeries });
            model.Annotations.Add(new LineAnnotation { Y = 70, Color = OxyColors.Gray, LineStyle = LineStyle.Dash, YAxisKey = yAxis.Key, Layer = AnnotationLayer.BelowSeries });

            var ls = new LineSeries
            {
                Title = "RSI",
                Color = OxyColors.Cyan,
                StrokeThickness = 1.5,
                XAxisKey = xAxis?.Key,
                YAxisKey = yAxis.Key
            };

            for (int i = 0; i < rsi.Length; i++)
                if (!double.IsNaN(rsi[i])) ls.Points.Add(new DataPoint(i, rsi[i]));

            model.Series.Add(ls);
        }
    }
}