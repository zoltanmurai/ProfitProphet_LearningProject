using Microsoft.EntityFrameworkCore;
using ProfitProphet.Data;
using ProfitProphet.DTOs;
using ProfitProphet.Entities;
using ProfitProphet.Mappers;
using ProfitProphet.Services.APIs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

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

            // 1️⃣ Lekérjük a lokális adatokat
            var localCandles = await _context.Candles
                .Where(c => c.Symbol == symbol && c.Timeframe == tf)
                .OrderBy(c => c.TimestampUtc)
                .ToListAsync();

            // Ha nincs semmi lokális adat, akkor majd újratöltjük API-ból
            bool hasLocal = localCandles.Count > 0;
            DateTime? lastLocalDate = hasLocal ? localCandles.Last().TimestampUtc : null;

            // 2️⃣ Ellenőrizzük, mennyire frissek a helyi adatok
            bool isOutdated = false;
            if (hasLocal && lastLocalDate.HasValue)
            {
                var age = (DateTime.UtcNow - lastLocalDate.Value).TotalDays;
                isOutdated = age > maxAgeDays;
            }

            bool needsFullReload = false;

            // 3️⃣ Ha VAN lokális adat, csak akkor ellenőrizzük az adatminőséget
            if (hasLocal)
            {
                // API-t csak most hívjuk, miután megnéztük a DB-t
                var client = new YahooFinanceClient();
                var dtoList = await client.GetHistoricalAsync(symbol, interval);
                var apiCandles = dtoList
                    .Select(d => new Candle
                    {
                        Symbol = d.Symbol,
                        TimestampUtc = d.TimestampUtc,
                        Open = d.Open,
                        High = d.High,
                        Low = d.Low,
                        Close = d.Close,
                        Volume = d.Volume,
                        Timeframe = tf
                    })
                    .OrderBy(c => c.TimestampUtc)
                    .ToList();

                // ⚠️ Ha az API nem adott vissza semmit → maradunk a régiekkel
                if (apiCandles.Count == 0)
                    return localCandles;

                // Ellenőrzés – csak a közös dátumokra (nem indexre!)
                int checkCount = Math.Min(3, Math.Min(localCandles.Count, apiCandles.Count));
                for (int i = 0; i < checkCount; i++)
                {
                    var local = localCandles[i];
                    var api = apiCandles[i];

                    // Ha a Symbol eltér, az biztos jel arra, hogy rossz táblázatot nézünk
                    if (!string.Equals(local.Symbol, api.Symbol, StringComparison.OrdinalIgnoreCase))
                    {
                        needsFullReload = true;
                        break;
                    }

                    // Érték-eltérés – nagyobb tűrés, hogy ne triggereljen minden apróságra
                    if (Math.Abs(local.Open - api.Open) > 0.5m ||
                        Math.Abs(local.Close - api.Close) > 0.5m ||
                        Math.Abs(local.High - api.High) > 0.5m ||
                        Math.Abs(local.Low - api.Low) > 0.5m)
                    {
                        needsFullReload = true;
                        break;
                    }
                }

                // 4️⃣ Döntés: teljes újratöltés vagy részleges frissítés
                if (needsFullReload || isOutdated)
                {
                    // Teljes újratöltés – csak ha tényleg szükséges
                    var oldCandles = await _context.Candles
                        .Where(c => c.Symbol == symbol && c.Timeframe == tf)
                        .ToListAsync();

                    if (oldCandles.Count > 0)
                    {
                        _context.Candles.RemoveRange(oldCandles);
                        await _context.SaveChangesAsync();
                    }

                    await SaveCandlesAsync(interval, apiCandles);
                    return apiCandles;
                }
                else
                {
                    // Részleges frissítés – az utolsó pár nap kiegészítése
                    DateTime correctionCutoff = DateTime.UtcNow.AddDays(-correctionDays);
                    var recentApi = apiCandles.Where(c => c.TimestampUtc >= correctionCutoff).ToList();

                    foreach (var c in recentApi)
                    {
                        var existing = await _context.Candles.FirstOrDefaultAsync(x =>
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

                    // Lokális + frissített gyertyák visszaadása
                    return await GetLocalDataAsync(symbol, interval);
                }
            }
            else
            {
                // 5️⃣ Ha nincs semmilyen lokális adat, most hívjuk az API-t
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

                if (apiCandles.Count > 0)
                {
                    await SaveCandlesAsync(interval, apiCandles);
                }

                return apiCandles;
            }
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
