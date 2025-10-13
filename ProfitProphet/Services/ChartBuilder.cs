using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using ProfitProphet.Entities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProfitProphet.Services
{
    /// <summary>
    /// Csak a chart (PlotModel) felépítéséért felel.
    /// Nem kezel interakciót, nem tölt adatot.
    /// </summary>
    public class ChartBuilder
    {
        public PlotModel BuildModel(IEnumerable<CandleBase> candles, string symbol, string interval)
        {
            if (candles == null || !candles.Any())
                throw new ArgumentException("A candle lista üres.");

            var items = candles.ToHighLowItems();
            var ordered = candles.OrderBy(c => c.TimestampUtc).ToList();

            var model = new PlotModel
            {
                Title = $"{symbol} ({interval})",
                TextColor = OxyColors.White,
                Background = OxyColor.FromRgb(22, 27, 34),
                PlotAreaBackground = OxyColor.FromRgb(24, 28, 34),
                PlotAreaBorderThickness = new OxyThickness(0),
                PlotAreaBorderColor = OxyColor.FromRgb(40, 40, 40)
            };

            var xAxis = new CategoryAxis
            {
                Position = AxisPosition.Bottom,
                TextColor = OxyColors.White,
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot,
                GapWidth = 0,
                IntervalLength = 80,
                IsZoomEnabled = true,
                IsPanEnabled = true
            };
            model.Axes.Add(xAxis);

            var yAxis = new LinearAxis
            {
                Position = AxisPosition.Left,
                TextColor = OxyColors.White,
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot,
                AxislineColor = OxyColors.Gray,
                Title = "Price",
                IsZoomEnabled = true,
                IsPanEnabled = true
            };
            model.Axes.Add(yAxis);

            var series = new CandleStickSeries
            {
                IncreasingColor = OxyColor.FromRgb(34, 197, 94),
                DecreasingColor = OxyColor.FromRgb(239, 68, 68),
                CandleWidth = CalculateCandleWidth(interval),
                YAxisKey = yAxis.Key,
                TrackerFormatString = "{Category}\nO: {4:0.###}\nH: {1:0.###}\nL: {2:0.###}\nC: {3:0.###}"
            };
            series.Items.AddRange(items);
            model.Series.Add(series);

            // Címkék: ritkított dátumkijelzés
            xAxis.Labels.AddRange(ordered.Select(c => c.TimestampUtc.ToString("MM-dd")));

            return model;
        }

        private static double CalculateCandleWidth(string interval)
        {
            if (interval == null) return 0.6;
            interval = interval.ToLower();
            if (interval.EndsWith("m")) return 0.5;
            if (interval.EndsWith("h")) return 0.7;
            if (interval.EndsWith("d")) return 0.9;
            return 0.6;
        }
    }
}
