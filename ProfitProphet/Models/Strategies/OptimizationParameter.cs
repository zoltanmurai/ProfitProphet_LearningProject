using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProfitProphet.Models.Strategies
{
    public class OptimizationParameter
    {
        public StrategyRule Rule { get; set; }
        public bool IsLeftSide { get; set; } // Igaz, ha a LeftPeriod-ot állítjuk, hamis ha a Right-ot
        public int MinValue { get; set; }
        public int MaxValue { get; set; }
        public string Name => $"{Rule.LeftIndicatorName} ({(IsLeftSide ? "L" : "R")})";
    }
}
