using OxyPlot.Axes;
using ProfitProphet.DTOs;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace ProfitProphet.Services.APIs
{
    public class AlphaVantageClient : IStockApiClient
    {
        private readonly HttpClient _http = new();
        private readonly string _apiKey;

        public AlphaVantageClient() : this(string.Empty) { }

        public AlphaVantageClient(string apiKey)
        {
            _apiKey = apiKey ?? string.Empty;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // IStockApiClient
        // ─────────────────────────────────────────────────────────────────────────
        public async Task<List<CandleDto>> GetHistoricalAsync(
            string symbol,
            string interval,
            DateTime? fromUtc = null,
            DateTime? toUtc = null)
        {
            if (string.IsNullOrWhiteSpace(symbol))
                return new List<CandleDto>();

            // Interval → AlphaVantage function + (intraday interval)
            string function;
            string? intraday = null;
            switch (interval)
            {
                case "1wk":
                case "1w":
                case "1week":
                    function = "TIME_SERIES_WEEKLY";
                    break;
                case "1mo":
                case "1mth":
                case "1month":
                    function = "TIME_SERIES_MONTHLY";
                    break;
                case "1m":
                case "2m":
                case "5m":
                case "15m":
                case "30m":
                case "60m":
                case "90m": 
                    function = "TIME_SERIES_INTRADAY";
                    intraday = interval == "90m" ? "60min" : interval.Replace("m", "min");
                    break;
                case "1d":
                case "1day":
                default:
                    function = "TIME_SERIES_DAILY"; // napi
                    break;
            }

            // URL összerakás
            var url = function == "TIME_SERIES_INTRADAY"
                ? $"https://www.alphavantage.co/query?function={function}&symbol={symbol}&interval={intraday}&outputsize=full&apikey={_apiKey}"
                : $"https://www.alphavantage.co/query?function={function}&symbol={symbol}&apikey={_apiKey}";

            var json = await _http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);

            // A megfelelő "Time Series ..." csomópont neve
            string seriesKey = function switch
            {
                "TIME_SERIES_INTRADAY" => $"Time Series ({intraday})",
                "TIME_SERIES_DAILY" => "Time Series (Daily)",
                "TIME_SERIES_WEEKLY" => "Weekly Time Series",
                "TIME_SERIES_MONTHLY" => "Monthly Time Series",
                _ => "Time Series (Daily)"
            };

            if (!doc.RootElement.TryGetProperty(seriesKey, out var series))
                return new List<CandleDto>();

            var list = new List<CandleDto>();
            foreach (var p in series.EnumerateObject())
            {
                // Alpha Vantage kulcs neve dátum string (local time)
                if (!DateTime.TryParse(p.Name, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dt))
                    continue;

                var node = p.Value;

                // Mezők kiolvasása
                decimal open = DecimalParse(node, "1. open");
                decimal high = DecimalParse(node, "2. high");
                decimal low = DecimalParse(node, "3. low");
                decimal close = DecimalParse(node, "4. close");

                long? volume = null;
                if (node.TryGetProperty("5. volume", out var vProp))
                {
                    if (long.TryParse(vProp.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var vol))
                        volume = vol;
                }

                // AV időbélyeg UTC
                var tsUtc = DateTime.SpecifyKind(dt, DateTimeKind.Utc);

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

            // Időrendbe (növekvő)
            list = list.OrderBy(x => x.TimestampUtc).ToList();

            if (fromUtc.HasValue) list = list.Where(x => x.TimestampUtc >= fromUtc.Value).ToList();
            if (toUtc.HasValue) list = list.Where(x => x.TimestampUtc <= toUtc.Value).ToList();

            return list;
        }

        public async Task<List<CandleDto>> GetLatestAsync(string symbol, string interval, int? count = null)
        {
            var data = await GetHistoricalAsync(symbol, interval);
            if (count is > 0)
                return data.TakeLast(count.Value).ToList();
            return data;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // (Opcionális) régi segéd
        // ─────────────────────────────────────────────────────────────────────────
        public async Task<List<OxyPlot.Series.HighLowItem>> GetIntradayDataLegacy(string ticker, string apiKey)
        {
            string url = $"https://www.alphavantage.co/query?function=TIME_SERIES_INTRADAY&symbol={ticker}&interval=5min&apikey={apiKey}&outputsize=compact";
            string json = await _http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("Time Series (5min)", out JsonElement timeSeries))
                return new List<OxyPlot.Series.HighLowItem>();

            var data = new List<OxyPlot.Series.HighLowItem>();
            foreach (var day in timeSeries.EnumerateObject())
            {
                if (DateTime.TryParse(day.Name, out DateTime date))
                {
                    double open = double.Parse(day.Value.GetProperty("1. open").GetString()!, CultureInfo.InvariantCulture);
                    double high = double.Parse(day.Value.GetProperty("2. high").GetString()!, CultureInfo.InvariantCulture);
                    double low = double.Parse(day.Value.GetProperty("3. low").GetString()!, CultureInfo.InvariantCulture);
                    double close = double.Parse(day.Value.GetProperty("4. close").GetString()!, CultureInfo.InvariantCulture);

                    data.Add(new OxyPlot.Series.HighLowItem(DateTimeAxis.ToDouble(date), high, low, open, close));
                }
            }

            return data.OrderBy(x => x.X).ToList();
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────────
        private static decimal DecimalParse(JsonElement node, string prop)
        {
            if (!node.TryGetProperty(prop, out var p)) return 0m;
            var s = p.GetString();
            if (string.IsNullOrWhiteSpace(s)) return 0m;
            if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                return d;
            return 0m;
        }
    }
}
