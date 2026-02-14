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
    // Indicator abstraction for computing values and rendering on a plot
    public interface IIndicator
    {
        // Unique identifier for the indicator
        string Id { get; }

        // Human-readable display name
        string DisplayName { get; }

        // Parameter definitions for the indicator
        IReadOnlyList<IndicatorParamDef> Params { get; }

        // Alternate signature using Candle entities (kept for reference)
        //IndicatorResult Compute(IReadOnlyList<Candle> candles, IndicatorParams values);

        // Compute indicator result from OHLC points and parameter values
        IndicatorResult Compute(IReadOnlyList<OhlcPoint> candles, IndicatorParams values);

        // Alternate render signature (kept for reference)
        //void Render(PlotModel model, IndicatorResult result, Axis xAxis, Axis yAxis);

        // Render the computed result onto the provided PlotModel using axes
        void Render(PlotModel model, IndicatorResult result, Axis xAxis, Axis yAxis);
    }
}

