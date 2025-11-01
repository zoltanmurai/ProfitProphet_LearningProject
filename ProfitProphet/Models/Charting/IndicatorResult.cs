using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProfitProphet.Models.Charting
{
    public sealed class IndicatorResult
    {
        public Dictionary<string, double[]> Series { get; } = new();
    }
}

