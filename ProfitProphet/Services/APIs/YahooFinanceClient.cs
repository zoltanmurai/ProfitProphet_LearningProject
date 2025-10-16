using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using ProfitProphet.DTOs;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace ProfitProphet.Services.APIs
{
    public class YahooFinanceClient : IStockApiClient
    {
        private static readonly HttpClient _http = CreateHttpClient();

        private static HttpClient CreateHttpClient()
        {
            var h = new HttpClient();
            h.DefaultRequestHeaders.UserAgent.ParseAdd(
                "ProfitProphet/1.0 (+https://example.local; contact: student@example.local)");
            h.Timeout = TimeSpan.FromSeconds(15);
            return h;
        }

        // ───────────────── IStockApiClient ─────────────────

        public async Task<List<CandleDto>> GetHistoricalAsync(
            string symbol,
            string interval,
            DateTime? fromUtc = null,
            DateTime? toUtc = null)
        {
            if (string.IsNullOrWhiteSpace(symbol))
                return new List<CandleDto>();

            // Ha from/to  period1/period2
            string url;
            if (fromUtc.HasValue && toUtc.HasValue)
            {
                long p1 = new DateTimeOffset(DateTime.SpecifyKind(fromUtc.Value, DateTimeKind.Utc)).ToUnixTimeSeconds();
                long p2 = new DateTimeOffset(DateTime.SpecifyKind(toUtc.Value, DateTimeKind.Utc)).ToUnixTimeSeconds();
                url = $"https://query1.finance.yahoo.com/v8/finance/chart/{symbol}?period1={p1}&period2={p2}&interval={MapInterval(interval)}";
            }
            else
            {
                var range = SuggestRange(interval);
                url = $"https://query1.finance.yahoo.com/v8/finance/chart/{symbol}?interval={MapInterval(interval)}&range={range}";
            }

            string json = await HttpGetWithRetryAsync(url);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("chart", out var chart)) return new List<CandleDto>();
            if (chart.TryGetProperty("error", out var err) && err.ValueKind != JsonValueKind.Null) return new List<CandleDto>();
            if (!chart.TryGetProperty("result", out var resultArr) || resultArr.ValueKind != JsonValueKind.Array || resultArr.GetArrayLength() == 0)
                return new List<CandleDto>();

            var r0 = resultArr[0];

            // timestamps
            if (!r0.TryGetProperty("timestamp", out var tsArr) || tsArr.ValueKind != JsonValueKind.Array || tsArr.GetArrayLength() == 0)
                return new List<CandleDto>();

            // indicators.quote[0] (open, high, low, close, volume)
            if (!r0.TryGetProperty("indicators", out var indicators) ||
                !indicators.TryGetProperty("quote", out var quoteArr) ||
                quoteArr.ValueKind != JsonValueKind.Array || quoteArr.GetArrayLength() == 0)
                return new List<CandleDto>();

            var q0 = quoteArr[0];

            var opens   = GetArray(q0, "open");
            var highs   = GetArray(q0, "high");
            var lows    = GetArray(q0, "low");
            var closes  = GetArray(q0, "close");
            var volumes = GetArray(q0, "volume");

            int n = new[] { tsArr.GetArrayLength(), opens.Count, highs.Count, lows.Count, closes.Count }.Min();
            var list = new List<CandleDto>(n);

            int i = 0;
            foreach (var tsEl in tsArr.EnumerateArray())
            {
                if (i >= n) break;

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
                    // Yahoo volume JSON-ben double. long-nak castoljuk
                    vol = (long)volumes[i].GetDouble();
                }

                list.Add(new CandleDto(symbol, tsUtc, o, h, l, c, vol));
                i++;
            }

            return list.OrderBy(x => x.TimestampUtc).ToList();
        }

        public async Task<List<CandleDto>> GetLatestAsync(string symbol, string interval, int? count = null)
        {
            var all = await GetHistoricalAsync(symbol, interval);
            return (count is > 0) ? all.TakeLast(count.Value).ToList() : all;
        }

        // ───────────────── Helpers ─────────────────

        private static string MapInterval(string interval) => interval switch
        {
            // Yahoo: 1m,2m,5m,15m,30m,60m,90m,1h,1d,5d,1wk,1mo,3mo
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
            "1d" => "6mo",
            "1wk" => "5y",
            "1mo" => "10y",
            _ => "1y"
        };

        private static List<JsonElement> GetArray(JsonElement parent, string prop)
            => parent.TryGetProperty(prop, out var p) && p.ValueKind == JsonValueKind.Array
               ? p.EnumerateArray().ToList()
               : new List<JsonElement>();

        private static bool IsNull(JsonElement el) => el.ValueKind == JsonValueKind.Null;

        private static async Task<string> HttpGetWithRetryAsync(string url, int maxAttempts = 3, int baseDelayMs = 600)
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
                    if (code == 429 || (code >= 500 && code <= 599))
                    {
                        await Task.Delay(baseDelayMs * (int)Math.Pow(2, attempt - 1));
                        continue;
                    }
                    resp.EnsureSuccessStatusCode();
                }
                catch (Exception ex)
                {
                    lastEx = ex;
                    await Task.Delay(baseDelayMs * (int)Math.Pow(2, attempt - 1));
                }
            }
            throw new HttpRequestException("Yahoo Finance lekérés sikertelen többszöri próbálkozás után.", lastEx);
        }

        // Opcionális segéd
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
