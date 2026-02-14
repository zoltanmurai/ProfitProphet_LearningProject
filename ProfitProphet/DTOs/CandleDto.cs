using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProfitProphet.DTOs
{
    // Candle DTO representing a single OHLCV bar with UTC timestamp
    public record CandleDto(
        // Trading symbol (e.g., "AAPL")
        string Symbol,
        // Timestamp in UTC for the candle
        DateTime TimestampUtc,
        // Open price
        decimal Open,
        // High price
        decimal High,
        // Low price
        decimal Low,
        // Close price
        decimal Close,
        // Volume (nullable if not available)
        long? Volume
    );
}
