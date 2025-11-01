using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProfitProphet.Models.Charting
{
    public sealed class ChartSettings
    {
        public string Symbol { get; set; } = "";
        public List<IndicatorInstance> Indicators { get; set; } = new();
    }
}

