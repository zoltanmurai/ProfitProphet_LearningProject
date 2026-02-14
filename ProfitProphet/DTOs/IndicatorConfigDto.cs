using System;
using System.Collections.Generic;
using System.Linq;

namespace ProfitProphet.DTOs
{
    // Supported indicator types used throughout the app.
    public enum IndicatorType
    {
        SMA,
        EMA,
        Stochastic,
        CMF,
        RSI,
        MACD,
        Bollinger
    }

    public class IndicatorConfigDto
    {
        public IndicatorType Type { get; set; }
        public bool IsEnabled { get; set; } = true;

        // Parameter dictionary uses case-insensitive keys so "Period" == "period".
        public Dictionary<string, string> Parameters { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public string Color { get; set; } = "#FFFFFF";

        // DisplayLabel: build a short human-readable label from Type and Parameters
        public string DisplayLabel
        {
            get
            {
                // Indicator name as base label
                var name = Type.ToString();

                // If no parameters, return the name unchanged
                if (Parameters == null || Parameters.Count == 0) return name;

                // Helper: retrieve a parameter value by key ignoring case
                string? GetVal(string key) =>
                    Parameters.Keys.FirstOrDefault(k => k.Equals(key, StringComparison.OrdinalIgnoreCase)) is string realKey
                    ? Parameters[realKey]
                    : null;

                // 1. Check for common "Period" parameter (e.g. SMA/EMA/RSI)
                var p = GetVal("Period") ?? GetVal("period");
                if (!string.IsNullOrWhiteSpace(p))
                {
                    // If a multiplier exists (e.g. Bollinger), include it
                    var mult = GetVal("Multiplier") ?? GetVal("multiplier");
                    if (!string.IsNullOrWhiteSpace(mult))
                        return $"{name} ({p}, {mult})";

                    return $"{name} ({p})";
                }

                // 2. Stochastic: use kPeriod and dPeriod if both present
                if (Type == IndicatorType.Stochastic)
                {
                    var k = GetVal("kPeriod");
                    var d = GetVal("dPeriod");
                    if (!string.IsNullOrWhiteSpace(k) && !string.IsNullOrWhiteSpace(d))
                        return $"{name} ({k}, {d})";
                }

                // 3. MACD: use FastPeriod, SlowPeriod, SignalPeriod when available
                if (Type == IndicatorType.MACD)
                {
                    var f = GetVal("FastPeriod");
                    var s = GetVal("SlowPeriod");
                    var sig = GetVal("SignalPeriod");
                    if (!string.IsNullOrWhiteSpace(f) && !string.IsNullOrWhiteSpace(s) && !string.IsNullOrWhiteSpace(sig))
                        return $"{name} ({f},{s},{sig})";
                }

                // Fallback: return the indicator name
                return name;
            }
        }
    }
}