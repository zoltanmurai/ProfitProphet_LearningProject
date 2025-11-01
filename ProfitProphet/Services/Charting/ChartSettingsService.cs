using ProfitProphet.Models.Charting;
using ProfitProphet.Services.Charting;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProfitProphet.Services.Charting
{
    public sealed class ChartSettingsService : IChartSettingsService
    {
        private readonly ConcurrentDictionary<string, ChartSettings> _cache =
            new(StringComparer.OrdinalIgnoreCase);

        public ChartSettings GetForSymbol(string symbol)
        {
            if (string.IsNullOrWhiteSpace(symbol)) symbol = "?";
            return _cache.GetOrAdd(symbol, s => new ChartSettings { Symbol = s });
        }

        public void Save(ChartSettings settings)
        {
            if (settings == null || string.IsNullOrWhiteSpace(settings.Symbol)) return;
            _cache[settings.Symbol] = settings;
        }
    }
}

