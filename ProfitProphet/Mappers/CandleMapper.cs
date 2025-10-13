using ProfitProphet.DTOs;
using ProfitProphet.Entities;

namespace ProfitProphet.Mappers
{
    public static class CandleMapper
    {
        private static Timeframe ToTf(string interval) => interval switch
        {
            "1wk" => Timeframe.Week,
            "1mo" => Timeframe.Month,
            "1d" => Timeframe.Day,
            _ => Timeframe.Hour
        };

        public static Candle ToEntity(this CandleDto d, string interval) => new Candle
        {
            Symbol = d.Symbol,
            TimestampUtc = d.TimestampUtc,
            Open = d.Open,
            High = d.High,
            Low = d.Low,
            Close = d.Close,
            Volume = d.Volume,
            Timeframe = ToTf(interval)
        };
    }
}