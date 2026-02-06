using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProfitProphet.Models.Strategies
{
    public class OptimizationResult
    {
        public int[] Values { get; set; }
        public double Score { get; set; }
        public double Profit { get; set; }
        public double Drawdown { get; set; }
        public int TradeCount { get; set; }
        public bool IsRobust { get; set; }
        public double NeighborAvgScore { get; set; }
        public string ParameterSummary { get; internal set; }
    }
}
