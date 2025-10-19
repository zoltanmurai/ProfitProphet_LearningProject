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
                c.Symbol = (c.Symbol ?? string.Empty).Trim().ToUpperInvariant();
                c.TimestampUtc = NormalizeTimestamp(c.TimestampUtc, tf);

                var existing = _context.Candles.FirstOrDefault(x =>
                    x.Symbol == c.Symbol &&
                    x.TimestampUtc == c.TimestampUtc &&
                    x.Timeframe == tf);

                if (existing == null)
                {
                    c.Symbol = ticker.Symbol;
                    _context.Candles.Add(c);
                }
                else
                {
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

        // Mentés: DTO-kat vagy kész entitásokat fogad
        public async Task SaveCandlesAsync(string interval, IEnumerable<object> rawCandles)
        {
            if (rawCandles == null) return;

            // DTO -> Entity konverzió. Ha nálad más a ToEntity aláírás, igazítsd.
            List<Candle> candles = rawCandles switch
            {
                IEnumerable<CandleDto> dtoList => dtoList.Select(d => d.ToEntity(interval)).ToList(),
                IEnumerable<Candle> entityList => entityList.ToList(),
                _ => throw new ArgumentException("Unsupported candle type collection.")
            };
            if (candles.Count == 0) return;

            // Interval -> Timeframe (írj ide a saját megfeleltetésedet)
            Timeframe tf = IntervalToTf(interval);

            // Normalizálás és egységesítés
            foreach (var c in candles)
            {
                c.Symbol = (c.Symbol ?? string.Empty).Trim().ToUpperInvariant();
                c.Timeframe = tf;
                c.TimestampUtc = NormalizeTimestamp(c.TimestampUtc, tf);
            }

            // Egy lekérdezéssel kérdezzük le a meglévő időpontokat az adott symbol+tf-re
            // Ha egyszerre több symbold jön, célszerűbb symbolonként csoportosítani (itt a legegyszerűbb verzió van)
            foreach (var group in candles.GroupBy(c => new { c.Symbol, c.Timeframe }))
            {
                var existing = await _context.Candles
                    .Where(x => x.Symbol == group.Key.Symbol && x.Timeframe == group.Key.Timeframe)
                    .Select(x => x.TimestampUtc)
                    .ToListAsync();

                var toInsert = group
                    .Where(c => !existing.Contains(c.TimestampUtc))
                    .ToList();

                if (toInsert.Count > 0)
                    await _context.Candles.AddRangeAsync(toInsert);
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
            var data = await _context.Candles
                .AsNoTracking()
                .Where(c => c.Symbol == symbol && c.Timeframe == tf)
                .OrderBy(c => c.TimestampUtc)
                .ToListAsync();

            // Biztonsági deduplikálás: ha valaha került be duplikátum, itt levágjuk
            var dedup = data
                .GroupBy(c => new { c.Symbol, c.Timeframe, c.TimestampUtc })
                .Select(g => g.First())
                .ToList();

            return dedup;
        }

        private static Timeframe IntervalToTf(string interval) => interval switch
        {
            "1wk" or "1w" => Timeframe.Week,
            "1mo" => Timeframe.Month,
            "1d" => Timeframe.Day,
            _ => Timeframe.Hour
        };

        // Timeframe normalizálás – a te enumod: Hour, Day, Week, Month
        private static DateTime NormalizeTimestamp(DateTime ts, Timeframe tf, DayOfWeek weekStart = DayOfWeek.Monday)
        {
            // Stabil összehasonlítás: kényszerítsük UTC-re
            if (ts.Kind == DateTimeKind.Local) ts = ts.ToUniversalTime();
            else if (ts.Kind == DateTimeKind.Unspecified) ts = DateTime.SpecifyKind(ts, DateTimeKind.Utc);

            switch (tf)
            {
                case Timeframe.Hour:
                    return new DateTime(ts.Year, ts.Month, ts.Day, ts.Hour, 0, 0, DateTimeKind.Utc);
                case Timeframe.Day:
                    return new DateTime(ts.Year, ts.Month, ts.Day, 0, 0, 0, DateTimeKind.Utc);
                case Timeframe.Week:
                    // Hét kezdete weekStart szerint (alap: hétfő)
                    int diff = (7 + (ts.DayOfWeek - weekStart)) % 7;
                    var start = ts.Date.AddDays(-diff);
                    return new DateTime(start.Year, start.Month, start.Day, 0, 0, 0, DateTimeKind.Utc);
                case Timeframe.Month:
                    return new DateTime(ts.Year, ts.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                default:
                    // Ha más timeframe is van az enumodban, bővítsd ide
                    return DateTime.SpecifyKind(ts, DateTimeKind.Utc);
            }
        }

        private static DateTime LastTradingDayOpenUtc(DateTime utcNow)
        {
            var d = utcNow.Date;
            while (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday)
                d = d.AddDays(-1);
            return new DateTime(d.Year, d.Month, d.Day, 0, 0, 0, DateTimeKind.Utc);
        }

        private static DateTime LastCompleteHourUtc(DateTime utcNow)
        {
            var h = new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, utcNow.Hour, 0, 0, DateTimeKind.Utc);
            if (utcNow.Minute == 0 && utcNow.Second == 0) h = h.AddHours(-1);
            while (h.DayOfWeek == DayOfWeek.Saturday || h.DayOfWeek == DayOfWeek.Sunday)
                h = h.AddHours(-1);
            return h;
        }

        private static DateTime GetMarketNowOpenUtc(Timeframe tf, DateTime utcNow)
        {
            return tf switch
            {
                Timeframe.Day => LastTradingDayOpenUtc(utcNow),
                Timeframe.Week => NormalizeTimestamp(utcNow, Timeframe.Week),
                Timeframe.Month => NormalizeTimestamp(utcNow, Timeframe.Month),
                Timeframe.Hour => NormalizeTimestamp(LastCompleteHourUtc(utcNow), Timeframe.Hour),
                _ => NormalizeTimestamp(utcNow, tf)
            };
        }

        public async Task<bool> HasLocalDataAsync(string symbol, string interval)
        {
            var tf = IntervalToTf(interval);
            return await _context.Candles
                .AsNoTracking()
                .AnyAsync(c => c.Symbol == symbol && c.Timeframe == tf);
        }

        // 
        public async Task DownloadLookbackAsync(string symbol, string interval, int lookbackDays)
        {
            var tf = IntervalToTf(interval);
            var toUtc = GetMarketNowOpenUtc(tf, DateTime.UtcNow);              // lezárt gyertya „most”
            var fromUtc = toUtc.AddDays(-lookbackDays);

            var client = new YahooFinanceClient(); // támogatja a from/to-t
            var dtoList = await client.GetHistoricalAsync(symbol, interval, fromUtc, toUtc); // :contentReference[oaicite:0]{index=0}

            // SaveCandlesAsync elvégzi a normalizálást + deduplikálást
            await SaveCandlesAsync(interval, dtoList);
        }


        //itt kezdődik a probléma.....

        public async Task<List<Candle>> GetRefreshReloadAsync(
            string symbol,
            string interval,
            int maxAgeDays = 2,
            int correctionDays = 5,
            decimal valueTolerance = 0.5m,
            int suspiciousDeleteThreshold = 200)
        {
            var tf = IntervalToTf(interval);
            Debug.WriteLine($"[DEBUG] GetRefreshReloadAsync: {symbol}, {interval}, tf={tf}");

            // 1) Lokális adatok
            var localCandles = await _context.Candles
                .Where(c => c.Symbol == symbol && c.Timeframe == tf)
                .OrderBy(c => c.TimestampUtc)
                .ToListAsync();

            bool hasLocal = localCandles.Count > 0;
            var lastLocalOpen = hasLocal ? NormalizeTimestamp(localCandles[^1].TimestampUtc, tf) : (DateTime?)null;

            // 2) Lezárt "most" (weekend fix)
            var marketNowOpen = GetMarketNowOpenUtc(tf, DateTime.UtcNow);

            bool isOutdated = hasLocal && lastLocalOpen.HasValue
                ? (marketNowOpen - lastLocalOpen.Value).TotalDays > maxAgeDays
                : !hasLocal;

            // 3) API lekérés (egyelőre teljes sáv, később szűkítünk)
            var client = new YahooFinanceClient();
            var dtoList = await client.GetHistoricalAsync(symbol, interval);

            var apiCandles = dtoList
                .Select(d => new Candle
                {
                    Symbol = (d.Symbol ?? string.Empty).Trim().ToUpperInvariant(),
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

            if (apiCandles.Count == 0)
                return localCandles;

            // 3/a) Normalizálás azonnal
            for (int i = 0; i < apiCandles.Count; i++)
                apiCandles[i].TimestampUtc = NormalizeTimestamp(apiCandles[i].TimestampUtc, tf);

            // 4) Ha nincs lokális vagy elavult → teljes korrekció, utána DB-ből térünk vissza
            if (!hasLocal || isOutdated)
            {
                await PerformCorrectiveUpdateAsync(symbol, tf, apiCandles, suspiciousDeleteThreshold);

                var updated = await _context.Candles
                    .AsNoTracking()
                    .Where(c => c.Symbol == symbol && c.Timeframe == tf)
                    .OrderBy(c => c.TimestampUtc)
                    .ToListAsync();

                return updated
                    .GroupBy(c => new { c.Symbol, c.Timeframe, c.TimestampUtc })
                    .Select(g => g.First())
                    .ToList();
            }

            // 5) Quality check a legfrissebb elemeken
            int checkCount = Math.Min(3, Math.Min(localCandles.Count, apiCandles.Count));
            var localTail = localCandles.TakeLast(checkCount).ToArray();
            var apiTail = apiCandles.TakeLast(checkCount).ToArray();

            bool needsFullReload = false;
            for (int i = 0; i < checkCount; i++)
            {
                var local = localTail[i];
                var api = apiTail[i];

                if (!string.Equals(local.Symbol, api.Symbol, StringComparison.OrdinalIgnoreCase))
                {
                    needsFullReload = true; break;
                }

                if (local.TimestampUtc != api.TimestampUtc)
                {
                    needsFullReload = true; break;
                }

                if (Math.Abs(local.Open - api.Open) > valueTolerance ||
                    Math.Abs(local.High - api.High) > valueTolerance ||
                    Math.Abs(local.Low - api.Low) > valueTolerance ||
                    Math.Abs(local.Close - api.Close) > valueTolerance)
                {
                    needsFullReload = true; break;
                }
            }

            if (needsFullReload)
            {
                await PerformCorrectiveUpdateAsync(symbol, tf, apiCandles, suspiciousDeleteThreshold);

                var updated = await _context.Candles
                    .AsNoTracking()
                    .Where(c => c.Symbol == symbol && c.Timeframe == tf)
                    .OrderBy(c => c.TimestampUtc)
                    .ToListAsync();

                return updated
                    .GroupBy(c => new { c.Symbol, c.Timeframe, c.TimestampUtc })
                    .Select(g => g.First())
                    .ToList();
            }

            // 6) Partial update: csak a correctionDays ablak, normalizált cutoff-hoz képest
            var rawCutoff = DateTime.UtcNow.AddDays(-correctionDays);
            var cutoffNorm = NormalizeTimestamp(rawCutoff, tf);
            var recentApi = apiCandles.Where(c => c.TimestampUtc >= cutoffNorm).ToList();

            int updatedCount = 0, addedCount = 0;
            foreach (var c in recentApi)
            {
                var existing = await _context.Candles.FirstOrDefaultAsync(x =>
                    x.Symbol == c.Symbol &&
                    x.TimestampUtc == c.TimestampUtc &&
                    x.Timeframe == tf);

                if (existing == null)
                {
                    _context.Candles.Add(c);
                    addedCount++;
                }
                else
                {
                    existing.Open = c.Open;
                    existing.High = c.High;
                    existing.Low = c.Low;
                    existing.Close = c.Close;
                    existing.Volume = c.Volume;
                    updatedCount++;
                }
            }
            await _context.SaveChangesAsync();

            var result = await _context.Candles
                .AsNoTracking()
                .Where(c => c.Symbol == symbol && c.Timeframe == tf)
                .OrderBy(c => c.TimestampUtc)
                .ToListAsync();

            return result
                .GroupBy(c => new { c.Symbol, c.Timeframe, c.TimestampUtc })
                .Select(g => g.First())
                .ToList();
        }

        private async Task PerformCorrectiveUpdateAsync(
    string symbol,
    Timeframe tf,
    List<Candle> apiCandles,
    int suspiciousDeleteThreshold)
        {
            // Lokális adatok ehhez a symbol+tf-hez
            var currentLocal = await _context.Candles
                .Where(c => c.Symbol == symbol && c.Timeframe == tf)
                .ToListAsync();

            // API: kulcs a normalizált timestamp
            var apiDict = apiCandles.ToDictionary(c => c.TimestampUtc, c => c);

            int addedCount = 0, updatedCount = 0, deletedCount = 0;

            // Upsert
            foreach (var apiC in apiCandles)
            {
                var existing = currentLocal.FirstOrDefault(x => x.TimestampUtc == apiC.TimestampUtc);

                if (existing == null)
                {
                    _context.Candles.Add(apiC);
                    addedCount++;
                }
                else
                {
                    existing.Open = apiC.Open;
                    existing.High = apiC.High;
                    existing.Low = apiC.Low;
                    existing.Close = apiC.Close;
                    existing.Volume = apiC.Volume;
                    existing.Symbol = apiC.Symbol;
                    updatedCount++;
                }
            }

            // Törlés: csak az adott symbol+tf-ben nem szereplő időpontok
            var toDelete = currentLocal.Where(local => !apiDict.ContainsKey(local.TimestampUtc)).ToList();

            deletedCount = toDelete.Count;
            if (deletedCount > 0 && deletedCount <= suspiciousDeleteThreshold)
                _context.Candles.RemoveRange(toDelete);

            await _context.SaveChangesAsync();

            Debug.WriteLine($"[DEBUG] Corrective: add={addedCount}, upd={updatedCount}, del={deletedCount}");
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
