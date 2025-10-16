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

        public async Task<List<Candle>> GetRefreshReloadAsync(
            string symbol,
            string interval,
            int maxAgeDays = 2,
            int correctionDays = 5)
        {
            var tf = IntervalToTf(interval);

            // 1️⃣ Lekérjük a helyi adatokat
            var localCandles = await _context.Candles
                .Where(c => c.Symbol == symbol && c.Timeframe == tf)
                .OrderBy(c => c.TimestampUtc)
                .ToListAsync();

            DateTime? lastLocalDate = localCandles.LastOrDefault()?.TimestampUtc;
            bool needsFullReload = false;

            // 2️⃣ Lekérjük az API-ból az utolsó néhány napot (mindig)
            var client = new YahooFinanceClient();
            var dtoList = await client.GetHistoricalAsync(symbol, interval);
            var apiCandles = dtoList.Select(d => new Candle
            {
                Symbol = d.Symbol,
                TimestampUtc = d.TimestampUtc,
                Open = d.Open,
                High = d.High,
                Low = d.Low,
                Close = d.Close,
                Volume = d.Volume,
                Timeframe = tf
            }).OrderBy(c => c.TimestampUtc).ToList();

            // 3️⃣ Ha nincs helyi adat → teljes betöltés
            if (localCandles.Count == 0)
            {
                await SaveCandlesAsync(interval, apiCandles);
                return apiCandles;
            }

            // 4️⃣ Megnézzük, mennyire frissek a helyi adatok
            bool isOutdated = (DateTime.UtcNow - lastLocalDate!.Value).TotalDays > maxAgeDays;

            // 5️⃣ Ellenőrizzük az első néhány gyertyát (pl. 3 nap)
            int checkCount = Math.Min(3, Math.Min(localCandles.Count, apiCandles.Count));
            for (int i = 0; i < checkCount; i++)
            {
                var local = localCandles[i];
                var api = apiCandles[i];

                if (decimal.Abs(local.Open - api.Open) > 0.0001m ||
                    decimal.Abs(local.Close - api.Close) > 0.0001m ||
                    decimal.Abs(local.High - api.High) > 0.0001m ||
                    decimal.Abs(local.Low - api.Low) > 0.0001m)
                {
                    needsFullReload = true;
                    break;
                }

            }

            if (needsFullReload || isOutdated)
            {
                // 🔁 teljes újratöltés, ha régi vagy eltér
                var old = _context.Candles.Where(c => c.Symbol == symbol && c.Timeframe == tf);
                _context.Candles.RemoveRange(old);
                await _context.SaveChangesAsync();

                await SaveCandlesAsync(interval, apiCandles);
                return apiCandles;
            }

            // 6️⃣ Részleges frissítés: utolsó néhány nap API-ból
            DateTime correctionCutoff = DateTime.UtcNow.AddDays(-correctionDays);
            var recentApi = apiCandles.Where(c => c.TimestampUtc >= correctionCutoff);

            foreach (var c in recentApi)
            {
                var existing = _context.Candles.FirstOrDefault(x =>
                    x.Symbol == c.Symbol &&
                    x.TimestampUtc == c.TimestampUtc &&
                    x.Timeframe == tf);

                if (existing == null)
                    _context.Candles.Add(c);
                else
                {
                    existing.Open = c.Open;
                    existing.High = c.High;
                    existing.Low = c.Low;
                    existing.Close = c.Close;
                    existing.Volume = c.Volume;
                }
            }

            await _context.SaveChangesAsync();

            // Visszatérés a frissített helyi adatokkal
            return await GetLocalDataAsync(symbol, interval);
        }
        public async Task RemoveSymbolAndCandlesAsync(string symbol)
        {
            var ticker = await _context.Tickers.FirstOrDefaultAsync(t => t.Symbol == symbol);
            if (ticker != null)
            {
                var candles = _context.Candles.Where(c => c.Symbol == symbol);
                _context.Candles.RemoveRange(candles);

                _context.Tickers.Remove(ticker);
                await _context.SaveChangesAsync();
            }
        }
    }
}
