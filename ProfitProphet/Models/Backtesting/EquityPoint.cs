using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProfitProphet.Models.Backtesting
{
    public class EquityPoint
    {
        public DateTime Time { get; set; }
        public double Equity { get; set; }
    }
}
