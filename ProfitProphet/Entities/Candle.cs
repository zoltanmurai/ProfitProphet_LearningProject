using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProfitProphet.Entities
{
    // Candle entity representing a single OHLCV bar
    public class Candle
    {
        // Primary key
        public int Id { get; set; }

        // Trading symbol (e.g., "AAPL")
        public string Symbol { get; set; } = null!;

        // UTC timestamp for the candle
        public DateTime TimestampUtc { get; set; }
        //public DateTime Date { get; set; }

        // Open price
        public decimal Open { get; set; }

        // High price
        public decimal High { get; set; }

        // Low price
        public decimal Low { get; set; }

        // Close price
        public decimal Close { get; set; }

        // Volume (nullable if not available)
        public long? Volume { get; set; }

        // Timeframe for the candle (enum)
        public Timeframe Timeframe { get; set; }

        //public int TickerId { get; set; }
        //public Ticker? Ticker { get; set; }
    }
}

