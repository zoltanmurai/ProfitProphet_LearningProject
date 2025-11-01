using OxyPlot;
using OxyPlot.Axes;
using ProfitProphet.Entities;
using ProfitProphet.Models.Charting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProfitProphet.Indicators.Abstractions
{
    public interface IIndicator
    {
        string Id { get; }
        string DisplayName { get; }
        IReadOnlyList<IndicatorParamDef> Params { get; }

        //IndicatorResult Compute(IReadOnlyList<Candle> candles, IndicatorParams values);
        IndicatorResult Compute(IReadOnlyList<OhlcPoint> candles, IndicatorParams values);
        //void Render(PlotModel model, IndicatorResult result, Axis xAxis, Axis yAxis);
        void Render(PlotModel model, IndicatorResult result, Axis xAxis, Axis yAxis);
    }
}

