using System;
using System.Collections.Generic;
using System.Linq;

namespace ProfitProphet.DTOs
{
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

        // Fontos: Alapból case-insensitive-re állítjuk, hogy a "Period" == "period"
        public Dictionary<string, string> Parameters { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public string Color { get; set; } = "#FFFFFF";

        public string DisplayLabel
        {
            get
            {
                var name = Type.ToString();
                if (Parameters == null || Parameters.Count == 0) return name;

                // Segédfüggvény: Érték keresése bárhogyan (Period, period, PERIOD)
                string? GetVal(string key) =>
                    Parameters.Keys.FirstOrDefault(k => k.Equals(key, StringComparison.OrdinalIgnoreCase)) is string realKey
                    ? Parameters[realKey]
                    : null;

                // 1. EMA / SMA / RSI / CMF / Bollinger -> "Period"
                var p = GetVal("Period") ?? GetVal("period");
                if (!string.IsNullOrWhiteSpace(p))
                {
                    // Ha van második paraméter (pl. Bollinger Multiplier), azt is tegyük hozzá
                    var mult = GetVal("Multiplier") ?? GetVal("multiplier");
                    if (!string.IsNullOrWhiteSpace(mult))
                        return $"{name} ({p}, {mult})";

                    return $"{name} ({p})";
                }

                // 2. Stochastic -> kPeriod, dPeriod
                if (Type == IndicatorType.Stochastic)
                {
                    var k = GetVal("kPeriod");
                    var d = GetVal("dPeriod");
                    if (!string.IsNullOrWhiteSpace(k) && !string.IsNullOrWhiteSpace(d))
                        return $"{name} ({k}, {d})";
                }

                // 3. MACD -> Fast, Slow, Signal
                if (Type == IndicatorType.MACD)
                {
                    var f = GetVal("FastPeriod");
                    var s = GetVal("SlowPeriod");
                    var sig = GetVal("SignalPeriod");
                    if (!string.IsNullOrWhiteSpace(f) && !string.IsNullOrWhiteSpace(s) && !string.IsNullOrWhiteSpace(sig))
                        return $"{name} ({f},{s},{sig})";
                }

                return name;
            }
        }
    }
}