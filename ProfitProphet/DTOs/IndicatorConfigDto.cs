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
    }
}
