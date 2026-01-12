using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProfitProphet.Models.Backtesting
{
    public class TradeRecord
    {
        public DateTime EntryDate { get; set; }
        public decimal EntryPrice { get; set; }
        public DateTime ExitDate { get; set; }
        public decimal ExitPrice { get; set; }
        public decimal Profit { get; set; }
        public decimal ProfitPercent { get; set; }
        public string Type { get; set; } = "Long";
    }
}
