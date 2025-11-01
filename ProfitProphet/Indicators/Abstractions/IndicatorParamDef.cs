using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProfitProphet.Indicators.Abstractions
{
    public sealed class IndicatorParamDef
    {
        public string Name { get; init; } = "";
        public Type Type { get; init; } = typeof(double);
        public object? DefaultValue { get; init; }
        public object? Min { get; init; }
        public object? Max { get; init; }
    }
}

