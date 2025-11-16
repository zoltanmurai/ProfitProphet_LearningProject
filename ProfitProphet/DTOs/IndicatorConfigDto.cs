using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProfitProphet.DTOs
{
    public enum IndicatorType { SMA, EMA, Stochastic, CMF }

    public class IndicatorConfigDto
    {
        public IndicatorType Type { get; set; }
        public bool IsEnabled { get; set; } = true;
        public Dictionary<string, string> Parameters { get; set; } = new();

        //only getter! No setter no troubles
        public string DisplayLabel
        {
            get
            {
                var name = Type.ToString();   // "EMA", "SMA", stb.
                if (Parameters == null) return name;

                // EMA / SMA / CMF -> period
                if (Type == IndicatorType.EMA || Type == IndicatorType.SMA || Type == IndicatorType.CMF)
                {
                    if (Parameters.TryGetValue("period", out var p) && !string.IsNullOrWhiteSpace(p))
                        return $"{name} ({p})";
                    return name;
                }

                // Stochastic -> kPeriod, dPeriod
                if (Type == IndicatorType.Stochastic)
                {
                    Parameters.TryGetValue("kPeriod", out var k);
                    Parameters.TryGetValue("dPeriod", out var d);

                    k = k?.Trim();
                    d = d?.Trim();

                    if (!string.IsNullOrEmpty(k) && !string.IsNullOrEmpty(d))
                        return $"{name} ({k},{d})";
                    if (!string.IsNullOrEmpty(k))
                        return $"{name} ({k})";
                    if (!string.IsNullOrEmpty(d))
                        return $"{name} ({d})";

                    return name;
                }

                return name;
            }
        }
    }
}
