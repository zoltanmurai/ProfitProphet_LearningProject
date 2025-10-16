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
        public static Candle ToEntity(this CandleDto dto, Timeframe timeframe)
        {
            return new Candle
            {
                Symbol = dto.Symbol,
                TimestampUtc = dto.TimestampUtc,
                Open = dto.Open,
                High = dto.High,
                Low = dto.Low,
                Close = dto.Close,
                Volume = dto.Volume,
                Timeframe = timeframe
            };
        }
    }
}
