using System;
using System.Collections.Generic;
using System.Linq;
using ProfitProphet.DTOs;
using ProfitProphet.Indicators.Abstractions;
using ProfitProphet.Indicators.BuiltIn; // where EmaIndicator/SmaIndicator/etc. live

namespace ProfitProphet.Services.Indicators
{
    public sealed class IndicatorRegistry : IIndicatorRegistry
    {
        private readonly Dictionary<string, IIndicator> _byId =
            new(StringComparer.OrdinalIgnoreCase);

        // Map logical enum -> registered string Id
        private readonly Dictionary<IndicatorType, string> _typeToId =
            new()
            {
                { IndicatorType.EMA, "ema" },
                { IndicatorType.SMA, "sma" },
                { IndicatorType.Stochastic, "stoch" },
                { IndicatorType.CMF, "cmf" },
                { IndicatorType.RSI, "rsi" },
                { IndicatorType.MACD, "macd" },
                { IndicatorType.Bollinger, "bb" },
            };

        public IndicatorRegistry()
        {
            // Register available indicators here
            Register(new EmaIndicator());     // must have Id == "ema"
            Register(new SmaIndicator());   // Id == "sma"
            Register(new StochasticIndicator()); // Id == "stoch"
            Register(new CmfIndicator());        // Id == "cmf"
            Register(new RsiIndicator());
            Register(new MacdIndicator());
            Register(new BollingerIndicator());
        }

        public IIndicator? Get(string id) =>
            _byId.TryGetValue(id, out var ind) ? ind : null;

        public IEnumerable<IIndicator> GetAll() => _byId.Values;

        public IIndicator Resolve(IndicatorType type)
        {
            if (!_typeToId.TryGetValue(type, out var id))
                throw new KeyNotFoundException($"No indicator id mapping for enum '{type}'.");

            var ind = Get(id);
            if (ind is null)
                throw new KeyNotFoundException($"Indicator id '{id}' not registered.");
            return ind;
        }

        private void Register(IIndicator indicator)
        {
            if (string.IsNullOrWhiteSpace(indicator.Id))
                throw new ArgumentException("Indicator Id must be non-empty.", nameof(indicator));

            _byId[indicator.Id] = indicator;
        }
    }
}
