using OxyPlot;
using OxyPlot.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ProfitProphet.Services.ChartBuilder;

public static class ChartHelpers
{
    public static void DrawWeekendMarkers(PlotModel model, IReadOnlyList<CandleData> candles)
    {
        if (model is null || candles is null) return;

        // régiek törlése
        var old = model.Annotations.Where(a => (a.Tag as string) == "weekend").ToList();
        foreach (var a in old) model.Annotations.Remove(a);

        for (int i = 1; i < candles.Count; i++)
        {
            var prev = candles[i - 1].Timestamp;
            var cur = candles[i].Timestamp;

            if ((cur - prev).TotalDays >= 2.0 ||
                (prev.DayOfWeek == DayOfWeek.Friday && cur.DayOfWeek == DayOfWeek.Monday))
            {
                model.Annotations.Add(new LineAnnotation
                {
                    Type = LineAnnotationType.Vertical,
                    X = i,
                    Color = OxyColors.SteelBlue,
                    StrokeThickness = 1,
                    Tag = "weekend"
                });
            }
        }

        model.InvalidatePlot(false);
    }
}
