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
        public int Id { get; set; }
        public string Symbol { get; set; } = null!;
        public DateTime TimestampUtc { get; set; }
        public decimal Open { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Close { get; set; }
        public long? Volume { get; set; }
        public Timeframe Timeframe { get; set; }   // ÚJ
    }

    public class HourlyCandle : CandleBase { }
    public class DailyCandle : CandleBase { }
    public class WeeklyCandle : CandleBase { }
    public class MonthlyCandle : CandleBase { }
}
