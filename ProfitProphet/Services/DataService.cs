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
            int correctionDays = 5,
            decimal valueTolerance = 0.5m,  // Növelve 0.5m-re esetleges rounding hibák miatt
            int suspiciousDeleteThreshold = 200)
        {
            var tf = IntervalToTf(interval);
            Debug.WriteLine($"[DEBUG] Starting GetRefreshReloadAsync for symbol: {symbol}, interval: {interval}, tf: {tf}");

            // 1. Lekérjük a lokális adatokat (NEM módosítjuk még az adatbázist!)
            var localCandles = await _context.Candles
                .Where(c => c.Symbol == symbol && c.Timeframe == tf)
                .OrderBy(c => c.TimestampUtc)
                .ToListAsync();

            bool hasLocal = localCandles.Count > 0;
            Debug.WriteLine($"[DEBUG] Local candles count: {localCandles.Count}. Has local data: {hasLocal}");

            DateTime? lastLocalDate = hasLocal ? localCandles.Last().TimestampUtc : null;
            Debug.WriteLine($"[DEBUG] Last local date: {lastLocalDate?.ToString("yyyy-MM-dd HH:mm:ss") ?? "None"}");

            // 2. Ellenőrizzük a frissességet (anélkül, hogy API-t hívnánk még)
            bool isOutdated = false;
            if (hasLocal && lastLocalDate.HasValue)
            {
                var age = (DateTime.UtcNow - lastLocalDate.Value).TotalDays;
                isOutdated = age > maxAgeDays;
                Debug.WriteLine($"[DEBUG] Data age in days: {age:F2}. Is outdated: {isOutdated}");
            }

            // 3. API lekérés (mindig, de optimalizáltan használjuk)
            Debug.WriteLine("[DEBUG] Fetching data from API.");
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

            if (apiCandles.Count == 0)
            {
                Debug.WriteLine("[DEBUG] API returned no data. Returning local data if any.");
                return localCandles;
            }

            Debug.WriteLine($"[DEBUG] API returned {apiCandles.Count} candles.");

            // 4. Ha NINCS lokális adat VAGY outdated → Corrective update az API-val
            if (!hasLocal || isOutdated)
            {
                Debug.WriteLine("[DEBUG] No local data or outdated → Performing corrective update from API.");
                await PerformCorrectiveUpdateAsync(symbol, tf, apiCandles, suspiciousDeleteThreshold);
                return await GetLocalDataAsync(symbol, interval);
            }

            // 5. Ha VAN lokális adat ÉS NEM outdated → Ellenőrizzük az adatminőséget
            Debug.WriteLine("[DEBUG] Has fresh local data → Checking data quality.");

            bool needsFullReload = false;
            int checkCount = Math.Min(3, Math.Min(localCandles.Count, apiCandles.Count));
            Debug.WriteLine($"[DEBUG] Checking first {checkCount} candles for quality.");

            for (int i = 0; i < checkCount; i++)
            {
                var local = localCandles[i];
                var api = apiCandles[i];

                // Symbol ellenőrzés
                if (!string.Equals(local.Symbol, api.Symbol, StringComparison.OrdinalIgnoreCase))
                {
                    needsFullReload = true;
                    Debug.WriteLine($"[DEBUG] Symbol mismatch at index {i}: Local '{local.Symbol}' vs API '{api.Symbol}'. Triggering corrective update.");
                    break;
                }

                // Timestamp ellenőrzés
                var timeDiff = Math.Abs((local.TimestampUtc - api.TimestampUtc).TotalDays);
                if (timeDiff > 1)
                {
                    needsFullReload = true;
                    Debug.WriteLine($"[DEBUG] Timestamp mismatch at index {i}: Local {local.TimestampUtc} vs API {api.TimestampUtc} (diff: {timeDiff:F2} days). Triggering corrective update.");
                    break;
                }

                // Érték ellenőrzés toleranciával
                if (Math.Abs(local.Open - api.Open) > valueTolerance ||
                    Math.Abs(local.High - api.High) > valueTolerance ||
                    Math.Abs(local.Low - api.Low) > valueTolerance ||
                    Math.Abs(local.Close - api.Close) > valueTolerance)
                {
                    needsFullReload = true;
                    Debug.WriteLine($"[DEBUG] Value mismatch at index {i}: Open {local.Open} vs {api.Open}, High {local.High} vs {api.High}, etc. (tolerance: {valueTolerance}). Triggering corrective update.");
                    break;
                }
            }

            if (needsFullReload)
            {
                Debug.WriteLine("[DEBUG] Quality check failed → Performing corrective update from API.");
                await PerformCorrectiveUpdateAsync(symbol, tf, apiCandles, suspiciousDeleteThreshold);
                return await GetLocalDataAsync(symbol, interval);
            }

            // 6. Ha minden OK → Részleges frissítés (csak a legutóbbi correctionDays nap)
            Debug.WriteLine("[DEBUG] Quality check passed → Performing partial update.");

            DateTime correctionCutoff = DateTime.UtcNow.AddDays(-correctionDays);
            var recentApi = apiCandles.Where(c => c.TimestampUtc >= correctionCutoff).ToList();
            Debug.WriteLine($"[DEBUG] Recent API candles for partial update: {recentApi.Count} (from {correctionCutoff:yyyy-MM-dd}).");

            int updatedCount = 0;
            int addedCount = 0;

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
            Debug.WriteLine($"[DEBUG] Partial update: Added {addedCount}, Updated {updatedCount} candles.");

            // Visszaadjuk a frissített lokális adatokat
            var updatedLocal = await GetLocalDataAsync(symbol, interval);
            Debug.WriteLine($"[DEBUG] Returning updated local candles: {updatedLocal.Count}");

            return updatedLocal;
        }

        private async Task PerformCorrectiveUpdateAsync(
            string symbol,
            Timeframe tf,
            List<Candle> apiCandles,
            int suspiciousDeleteThreshold)
        {
            int addedCount = 0;
            int updatedCount = 0;
            int deletedCount = 0;

            // Aktuális lokális adatok lekérése
            var currentLocal = await _context.Candles
                .Where(c => c.Symbol == symbol && c.Timeframe == tf)
                .ToListAsync();

            Debug.WriteLine($"[DEBUG] Current local candles for corrective update: {currentLocal.Count}");

            // API dict timestamp alapján
            var apiDict = apiCandles.ToDictionary(c => c.TimestampUtc, c => c);

            // Update vagy add az API alapján
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
                    existing.Symbol = apiC.Symbol;  // Biztonság kedvéért
                    updatedCount++;
                }
            }

            // Extra local-ok törlése, amik nincsenek API-ban
            var toDelete = currentLocal.Where(local => !apiDict.ContainsKey(local.TimestampUtc)).ToList();

            deletedCount = toDelete.Count;
            Debug.WriteLine($"[DEBUG] Found {deletedCount} extra local candles to potentially delete.");

            if (deletedCount > suspiciousDeleteThreshold)
            {
                Debug.WriteLine($"[WARNING] Suspicious large number of deletions ({deletedCount} > {suspiciousDeleteThreshold}) for symbol {symbol}. Skipping deletion to prevent data loss. Possible symbol mismatch or API issue.");
            }
            else
            {
                if (deletedCount > 0)
                {
                    _context.Candles.RemoveRange(toDelete);
                    Debug.WriteLine($"[DEBUG] Deleting {deletedCount} extra candles.");
                }
            }

            await _context.SaveChangesAsync();
            Debug.WriteLine($"[DEBUG] Corrective update completed: Added {addedCount}, Updated {updatedCount}, Deleted {deletedCount} (or skipped if suspicious).");
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
