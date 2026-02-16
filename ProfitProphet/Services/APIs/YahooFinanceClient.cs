using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using ProfitProphet.DTOs;
using OxyPlot.Axes; // Ha ezek kellenek a HighLowItem-hez
using OxyPlot.Series;

namespace ProfitProphet.Services.APIs
{
    public class YahooFinanceClient : IStockApiClient
    {
        private static readonly HttpClient _http = CreateHttpClient();

        public YahooFinanceClient()
        {
            System.Diagnostics.Debug.WriteLine(">>> API BETÖLTVE: YahooFinanceClient");
        }

        private static HttpClient CreateHttpClient()
        {
            var h = new HttpClient();
            // Fontos: User-Agent beállítása, különben a Yahoo 403-at dobhat
            h.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
            h.Timeout = TimeSpan.FromSeconds(20);
            return h;
        }

        // ───────────────── IStockApiClient ─────────────────

        public async Task<List<CandleDto>> GetHistoricalAsync(
            string symbol,
            string interval,
            DateTime? fromUtc = null,
            DateTime? toUtc = null)
        {
            // 1. LÉPÉS: Normalizálás (BRK.B -> BRK-B)
            // A programodban tárold nyugodtan BRK.B-ként, itt átváltjuk Yahoo kompatibilisre.
            string yahooSymbol = NormalizeSymbolForYahoo(symbol);

            System.Diagnostics.Debug.WriteLine($"[YahooClient] Kérés indítása: {symbol} -> Yahoo formátum: {yahooSymbol}");

            if (string.IsNullOrWhiteSpace(yahooSymbol))
                return new List<CandleDto>();

            // URL összeállítás
            string url;
            string mappedInterval = MapInterval(interval);

            if (fromUtc.HasValue && toUtc.HasValue)
            {
                long p1 = new DateTimeOffset(DateTime.SpecifyKind(fromUtc.Value, DateTimeKind.Utc)).ToUnixTimeSeconds();
                long p2 = new DateTimeOffset(DateTime.SpecifyKind(toUtc.Value, DateTimeKind.Utc)).ToUnixTimeSeconds();
                url = $"https://query1.finance.yahoo.com/v8/finance/chart/{yahooSymbol}?period1={p1}&period2={p2}&interval={mappedInterval}";
            }
            else
            {
                var range = SuggestRange(interval);
                url = $"https://query1.finance.yahoo.com/v8/finance/chart/{yahooSymbol}?interval={mappedInterval}&range={range}";
            }

            // Letöltés és hibakezelés (Symbol + URL átadása a hibakezelőnek)
            string json = await HttpGetWithRetryAsync(url, yahooSymbol);

            // Feldolgozás
            return ParseYahooJson(json, symbol); // Visszaadjuk az eredeti (Standard) symbol nevet a DTO-ban!
        }

        public async Task<List<CandleDto>> GetLatestAsync(string symbol, string interval, int? count = null)
        {
            var all = await GetHistoricalAsync(symbol, interval);
            return (count is > 0) ? all.TakeLast(count.Value).ToList() : all;
        }

        // ───────────────── Helpers ─────────────────

        private List<CandleDto> ParseYahooJson(string json, string originalSymbol)
        {
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("chart", out var chart)) return new List<CandleDto>();
            if (chart.TryGetProperty("error", out var err) && err.ValueKind != JsonValueKind.Null)
            {
                // Itt lehetne logolni a Yahoo specifikus hibaüzenetet
                return new List<CandleDto>();
            }
            if (!chart.TryGetProperty("result", out var resultArr) || resultArr.ValueKind != JsonValueKind.Array || resultArr.GetArrayLength() == 0)
                return new List<CandleDto>();

            var r0 = resultArr[0];

            if (!r0.TryGetProperty("timestamp", out var tsArr) || tsArr.ValueKind != JsonValueKind.Array)
                return new List<CandleDto>();

            if (!r0.TryGetProperty("indicators", out var indicators) ||
                !indicators.TryGetProperty("quote", out var quoteArr) ||
                quoteArr.ValueKind != JsonValueKind.Array || quoteArr.GetArrayLength() == 0)
                return new List<CandleDto>();

            var q0 = quoteArr[0];

            var opens = GetArray(q0, "open");
            var highs = GetArray(q0, "high");
            var lows = GetArray(q0, "low");
            var closes = GetArray(q0, "close");
            var volumes = GetArray(q0, "volume");

            int n = new[] { tsArr.GetArrayLength(), opens.Count, highs.Count, lows.Count, closes.Count }.Min();
            var list = new List<CandleDto>(n);

            int i = 0;
            foreach (var tsEl in tsArr.EnumerateArray())
            {
                if (i >= n) break;

                // Hibás adatok kiszűrése (null értékek)
                if (IsNull(opens[i]) || IsNull(highs[i]) || IsNull(lows[i]) || IsNull(closes[i]))
                {
                    i++;
                    continue;
                }

                long unix = tsEl.GetInt64();
                DateTime tsUtc = DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime;

                decimal o = (decimal)opens[i].GetDouble();
                decimal h = (decimal)highs[i].GetDouble();
                decimal l = (decimal)lows[i].GetDouble();
                decimal c = (decimal)closes[i].GetDouble();

                long? vol = null;
                if (i < volumes.Count && !IsNull(volumes[i]))
                {
                    vol = (long)volumes[i].GetDouble();
                }

                // FONTOS: Az eredeti (standard) symbol nevet adjuk vissza a DTO-ban, 
                // nem a Yahoo-félét (BRK.B maradjon, ne BRK-B), hogy a program többi része ne zavarodjon meg.
                list.Add(new CandleDto(originalSymbol, tsUtc, o, h, l, c, vol));
                i++;
            }

            return list.OrderBy(x => x.TimestampUtc).ToList();
        }

        private static string MapInterval(string interval) => interval switch
        {
            "1m" => "1m",
            "2m" => "2m",
            "5m" => "5m",
            "15m" => "15m",
            "30m" => "30m",
            "60m" => "60m",
            "90m" => "90m",
            "1h" => "60m",
            "1d" => "1d",
            "5d" => "5d",
            "1wk" or "1w" => "1wk",
            "1mo" => "1mo",
            "3mo" => "3mo",
            _ => "1d"
        };

        private static string SuggestRange(string interval) => interval switch
        {
            "1m" or "2m" or "5m" => "5d",
            "15m" or "30m" or "60m" or "90m" or "1h" => "1mo",
            "1d" => "1y",    // Növeltem, hogy több adat legyen alapból
            "1wk" => "5y",
            "1mo" => "10y",
            _ => "1y"
        };

        private static List<JsonElement> GetArray(JsonElement parent, string prop)
            => parent.TryGetProperty(prop, out var p) && p.ValueKind == JsonValueKind.Array
                ? p.EnumerateArray().ToList()
                : new List<JsonElement>();

        private static bool IsNull(JsonElement el) => el.ValueKind == JsonValueKind.Null;

        private static async Task<string> HttpGetWithRetryAsync(string url, string symbolInfo, int maxAttempts = 3, int baseDelayMs = 1000)
        {
            Exception? lastEx = null;
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    using var req = new HttpRequestMessage(HttpMethod.Get, url);
                    var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);

                    if (resp.IsSuccessStatusCode)
                        return await resp.Content.ReadAsStringAsync();

                    int code = (int)resp.StatusCode;

                    // 404 hiba (Nincs ilyen részvény) - Ezt azonnal dobjuk, felesleges újrapróbálni
                    if (code == 404)
                    {
                        throw new HttpRequestException($"[Yahoo 404] A '{symbolInfo}' részvény nem található a Yahoo rendszerében. Ellenőrizd a nevet!");
                    }

                    // Rate limit vagy Server error esetén várunk
                    if (code == 429 || (code >= 500 && code <= 599))
                    {
                        await Task.Delay(baseDelayMs * attempt); // Növekvő várakozás
                        continue;
                    }

                    resp.EnsureSuccessStatusCode();
                }
                catch (Exception ex)
                {
                    lastEx = ex;
                    // Ha már a saját 404-es hibánk, ne nyeljük el
                    if (ex.Message.Contains("[Yahoo 404]")) throw;

                    await Task.Delay(baseDelayMs * attempt);
                }
            }

            throw new HttpRequestException(
                $"Yahoo Finance lekérés sikertelen {maxAttempts} próbálkozás után.\n" +
                $"Cél: {symbolInfo}\n" +
                $"Hiba: {lastEx?.Message}", lastEx);
        }

        /// <summary>
        /// A "Standard" formátumot (BRK.B) átalakítja Yahoo formátumra (BRK-B).
        /// De a tőzsdei kiterjesztéseket (OTP.BD) békén hagyja.
        /// </summary>
        private string NormalizeSymbolForYahoo(string symbol)
        {
            if (string.IsNullOrWhiteSpace(symbol)) return "";

            string normalized = symbol.Trim().ToUpperInvariant();

            // 1. Ismert kivételek kezelése
            if (normalized == "BRK.B") return "BRK-B";
            if (normalized == "BF.B") return "BF-B";

            // 2. Automatikus detektálás
            // Ha van benne pont, megvizsgáljuk, mi van utána.
            if (normalized.Contains("."))
            {
                var parts = normalized.Split('.');

                // Ha a pont után pontosan 1 karakter van (pl. "XYZ.A", "XYZ.B"), 
                // az általában "Share Class"-t jelöl, amit a Yahoo kötőjellel vár.
                if (parts.Length == 2 && parts[1].Length == 1)
                {
                    return $"{parts[0]}-{parts[1]}";
                }

                // Ha a pont után 2 vagy több karakter van (pl. "OTP.BD", "SIE.DE", "VOW3.DE"),
                // az Tőzsdei régiót jelöl, amit a Yahoo is ponttal használ -> NEM kell cserélni.
            }

            return normalized;
        }

        // Helper grafikonhoz (opcionális, ha használod)
        public static List<HighLowItem> ToHighLowItems(IEnumerable<CandleDto> bars)
            => bars.Select(d => new HighLowItem(
                    DateTimeAxis.ToDouble(d.TimestampUtc),
                    (double)d.High,
                    (double)d.Low,
                    (double)d.Open,
                    (double)d.Close))
                   .ToList();
    }
}