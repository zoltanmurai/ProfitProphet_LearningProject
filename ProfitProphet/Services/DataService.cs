using Microsoft.EntityFrameworkCore;
using ProfitProphet.Data;
using ProfitProphet.DTOs;
using ProfitProphet.Entities;
using ProfitProphet.Mappers;
using ProfitProphet.Services.APIs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProfitProphet.Services
{
    public class DataService
    {
        private readonly StockContext _context;

        public DataService(StockContext context) => _context = context;

        public void SaveCandles(string symbol, string name, List<Candle> candles)
        {
            var ticker = _context.Tickers.FirstOrDefault(t => t.Symbol == symbol);
            if (ticker == null)
            {
                ticker = new Ticker { Symbol = symbol, Name = name };
                _context.Tickers.Add(ticker);
                _context.SaveChanges();
            }

            foreach (var c in candles)
            {
                var tf = c.Timeframe;
                //var existing = _context.Candles
                //    .FirstOrDefault(x => x.TickerId == ticker.Id && x.TimestampUtc == c.TimestampUtc);
                var existing = _context.Candles.FirstOrDefault(x =>
                    x.Symbol == c.Symbol &&
                    x.TimestampUtc == c.TimestampUtc &&
                    x.Timeframe == tf  //idősík
                );

                if (existing == null)
                {
                    c.Symbol = ticker.Symbol;
                    _context.Candles.Add(c);
                }
                else
                {
                    // Update fields in case data changed
                    existing.Open = c.Open;
                    existing.High = c.High;
                    existing.Low = c.Low;
                    existing.Close = c.Close;
                    existing.Volume = c.Volume;
                }
            }

            _context.SaveChanges();
        }

        public List<Candle> LoadRecentCandles(string symbol, Timeframe tf, int maxDays = 120)
        {
            var cutoff = DateTime.UtcNow.AddDays(-maxDays);

            return _context.Candles
                .AsNoTracking()
                .Where(c => c.Symbol == symbol &&
                            c.Timeframe == tf &&
                            c.TimestampUtc >= cutoff)
                .OrderBy(c => c.TimestampUtc)
                .ToList();
        }

        public async Task SaveCandlesAsync(string interval, List<CandleDto> dtos)
        {
            if (dtos == null || dtos.Count == 0) return;

            // DTO → Entity konverzió
            var entities = dtos.Select(d => d.ToEntity(interval)).ToList();

            await SaveCandlesAsync(interval, entities);
        }

        // Ezt használja a chart és a lokális adatok
        public async Task SaveCandlesAsync(string interval, List<Candle> candles)
        {
            if (candles == null || candles.Count == 0) return;

            foreach (var c in candles)
            {
                bool exists = await _context.Candles
                    .AnyAsync(x => x.Symbol == c.Symbol && x.TimestampUtc == c.TimestampUtc);

                if (!exists)
                    _context.Candles.Add(c);
            }

            await _context.SaveChangesAsync();
        }

        public async Task<List<Candle>> GetDataAsync(string symbol, string interval)
        {
            // Itt most egyszerűen a Yahoo API-t használjuk (alapértelmezett)
            var client = new YahooFinanceClient();
            var dtoList = await client.GetHistoricalAsync(symbol, interval);

            //var candles = await client.GetHistoricalAsync(symbol, interval);

            var candles = dtoList.Select(d => new Candle
            {
                Symbol = d.Symbol,
                TimestampUtc = d.TimestampUtc,
                Open = d.Open,
                High = d.High,
                Low = d.Low,
                Close = d.Close,
                Volume = d.Volume
            }).ToList();


            return candles;
        }
        public async Task<List<Candle>> GetLocalDataAsync(string symbol, string interval)
        {
            var tf = IntervalToTf(interval);
            return await _context.Candles
                .AsNoTracking()
                .Where(c => c.Symbol == symbol && c.Timeframe == tf)
                .OrderBy(c => c.TimestampUtc)
                .ToListAsync();
        }

        private static Timeframe IntervalToTf(string interval) => interval switch
        {
            "1wk" or "1w" => Timeframe.Week,
            "1mo" => Timeframe.Month,
            "1d" => Timeframe.Day,
            _ => Timeframe.Hour
        };
    }
}
