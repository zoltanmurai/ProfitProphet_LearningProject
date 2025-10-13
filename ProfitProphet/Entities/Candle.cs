using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProfitProphet.Entities
{
    public class Candle
    {
        public int Id { get; set; }
        public string Symbol { get; set; } = null!;
        public DateTime TimestampUtc { get; set; }
        //public DateTime Date { get; set; }

        public decimal Open { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Close { get; set; }
        public long? Volume { get; set; }
        public Timeframe Timeframe { get; set; }

        //public int TickerId { get; set; }
        //public Ticker? Ticker { get; set; }
    }
}

