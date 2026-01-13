using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProfitProphet.Models.Strategies
{
    public class OptimizationResult
    {
        public int[] Values { get; set; } // A paraméterek értékei (pl. 20, 14, 50)
        public double Score { get; set; }
        public double Profit { get; set; }
        public double MaxDrawdown { get; set; }
        public int TradeCount { get; set; }
    }
}
