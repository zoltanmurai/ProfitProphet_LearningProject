using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;

namespace ProfitProphet.Entities
{
    public abstract class CandleBase
    {
        // Primary key
        public int Id { get; set; }

        // Trading symbol (e.g., "AAPL")
        public string Symbol { get; set; } = null!;

        // UTC timestamp for the candle
        public DateTime TimestampUtc { get; set; }

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

        // Timeframe for the candle (enum) - New
        public Timeframe Timeframe { get; set; }   // ÚJ
    }

    // Concrete timeframe-specific candle types
    public class HourlyCandle : CandleBase { }
    public class DailyCandle : CandleBase { }
    public class WeeklyCandle : CandleBase { }
    public class MonthlyCandle : CandleBase { }
}
