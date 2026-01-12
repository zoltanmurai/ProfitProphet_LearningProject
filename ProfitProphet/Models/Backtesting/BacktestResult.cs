using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProfitProphet.Models.Backtesting
{
    public class BacktestResult
    {
        public string Symbol { get; set; } = string.Empty;
        public double TotalProfitLoss { get; set; }
        public int TradeCount { get; set; }
        public double WinRate { get; set; }
        public double MaxDrawdown { get; set; }
        public double Score { get; set; } // VBA logika: PL - (0.5 * DD)
        public List<TradeRecord> Trades { get; set; } = new(); 
        public List<EquityPoint> EquityCurve { get; set; } = new();
    }
}
