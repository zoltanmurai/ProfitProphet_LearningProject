using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProfitProphet.Indicators.Abstractions
{
    // Definition for a single indicator parameter (name, type and optional limits)
    public sealed class IndicatorParamDef
    {
        // Parameter name (e.g., "Period")
        public string Name { get; init; } = "";

        // CLR type of the parameter value (default: double)
        public Type Type { get; init; } = typeof(double);

        // Optional default value for the parameter
        public object? DefaultValue { get; init; }

        // Optional minimum allowed value
        public object? Min { get; init; }

        // Optional maximum allowed value
        public object? Max { get; init; }
    }
}

