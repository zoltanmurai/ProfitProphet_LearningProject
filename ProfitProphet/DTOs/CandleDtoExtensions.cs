using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ProfitProphet.Entities;

namespace ProfitProphet.DTOs
{
    public static class CandleDtoExtensions
    {
        // Convert CandleDto to Candle entity and apply the provided timeframe
        public static Candle ToEntity(this CandleDto dto, Timeframe timeframe)
        {
            // Create a new Candle entity populated from the DTO
            return new Candle
            {
                // Trading symbol
                Symbol = dto.Symbol,
                // UTC timestamp for the candle
                TimestampUtc = dto.TimestampUtc,
                // Open price
                Open = dto.Open,
                // High price
                High = dto.High,
                // Low price
                Low = dto.Low,
                // Close price
                Close = dto.Close,
                // Volume (nullable)
                Volume = dto.Volume,
                // Timeframe provided by caller
                Timeframe = timeframe
            };
        }
    }
}
