using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using ProfitProphet.DTOs;

namespace ProfitProphet.Services.APIs
{
    public class TwelveDataClient : IStockApiClient
    {
        private readonly HttpClient _http = new();
        private readonly string _apiKey;

        public TwelveDataClient() : this(string.Empty) { }
        public TwelveDataClient(string apiKey) 
        { 
            _apiKey = apiKey ?? string.Empty; 
            System.Diagnostics.Debug.WriteLine("Beolvasott SelectedApi: TwelveDataClient");
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

            // Interval mapping Twelve Data-hoz
            var tdInterval = MapInterval(interval);

            // outputsize
            var url =
                $"https://api.twelvedata.com/time_series?symbol={symbol}&interval={tdInterval}&outputsize=5000&timezone=UTC&format=JSON&apikey={_apiKey}";

            var json = await _http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);

            // hiba eset / meta
            if (!doc.RootElement.TryGetProperty("values", out var values) || values.ValueKind != JsonValueKind.Array)
                return new List<CandleDto>();

            var list = new List<CandleDto>();
            foreach (var item in values.EnumerateArray())
            {
                // datetime
                if (!item.TryGetProperty("datetime", out var dtProp)) continue;
                var dtStr = dtProp.GetString();
                if (string.IsNullOrWhiteSpace(dtStr)) continue;

                // TwelveData UTC ISO formátum, pl. "2024-10-01 15:30:00"
                if (!DateTime.TryParse(dtStr, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dt))
                    continue;
                var tsUtc = DateTime.SpecifyKind(dt, DateTimeKind.Utc);

                // árak
                decimal open = DecimalParse(item, "open");
                decimal high = DecimalParse(item, "high");
                decimal low = DecimalParse(item, "low");
                decimal close = DecimalParse(item, "close");

                // volume (ha nincs, marad null)
                long? volume = null;
                if (item.TryGetProperty("volume", out var v))
                {
                    var vs = v.GetString();
                    if (long.TryParse(vs, NumberStyles.Any, CultureInfo.InvariantCulture, out var vol))
                        volume = vol;
                }

                list.Add(new CandleDto(
                    Symbol: symbol,
                    TimestampUtc: tsUtc,
                    Open: open,
                    High: high,
                    Low: low,
                    Close: close,
                    Volume: volume
                ));
            }

            // növekvő időrend
            list = list.OrderBy(x => x.TimestampUtc).ToList();

            // opcionális from/to szűkítés
            if (fromUtc.HasValue) list = list.Where(x => x.TimestampUtc >= fromUtc.Value).ToList();
            if (toUtc.HasValue) list = list.Where(x => x.TimestampUtc <= toUtc.Value).ToList();

            return list;
        }

        public async Task<List<CandleDto>> GetLatestAsync(string symbol, string interval, int? count = null)
        {
            var data = await GetHistoricalAsync(symbol, interval);
            return (count is > 0) ? data.TakeLast(count.Value).ToList() : data;
        }

        // ───────────────── Helpers ─────────────────

        private static string MapInterval(string interval) => interval switch
        {
            // percek → TwelveData: 1min,5min,15min,30min
            "1m" => "1min",
            "2m" => "1min",  
            "5m" => "5min",
            "15m" => "15min",
            "30m" => "30min",
            "60m" or "1h" => "1h",
            "90m" => "1h",    
            // nap/hét/hónap
            "1d" => "1day",
            "1wk" or "1w" => "1week",
            "1mo" => "1month",
            _ => "1day"
        };

        private static decimal DecimalParse(JsonElement e, string prop)
        {
            if (!e.TryGetProperty(prop, out var p)) return 0m;
            var s = p.GetString();
            if (string.IsNullOrWhiteSpace(s)) return 0m;
            return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0m;
        }
    }
}
