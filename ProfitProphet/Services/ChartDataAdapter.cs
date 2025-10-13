using OxyPlot.Series;
using ProfitProphet.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProfitProphet.Services
{
    public static class ChartDataAdapter
    {
        /// <summary>
        /// Konvertálja az entitáslistát OxyPlot HighLowItem listává.
        /// </summary>
        public static List<HighLowItem> ToHighLowItems(this IEnumerable<CandleBase> candles)
        {
            if (candles == null) return new();
            return candles
                .OrderBy(c => c.TimestampUtc)
                .Select((c, i) => new HighLowItem(i,
                    high: (double)c.High,
                    low: (double)c.Low,
                    open: (double)c.Open,
                    close: (double)c.Close))
                .ToList();
        }
    }
}
