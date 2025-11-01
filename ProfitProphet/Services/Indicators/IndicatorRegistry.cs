using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ProfitProphet.Indicators.Abstractions;
using ProfitProphet.Indicators.BuiltIn;

namespace ProfitProphet.Services.Indicators
{
    public sealed class IndicatorRegistry : IIndicatorRegistry
    {
        private readonly Dictionary<string, IIndicator> _map = new(StringComparer.OrdinalIgnoreCase);

        public IndicatorRegistry()
        {
            Register(new EmaIndicator());
            // Később: Register(new SmaIndicator()); Register(new RsiIndicator());
        }

        private void Register(IIndicator ind) => _map[ind.Id] = ind;
        public IIndicator? Get(string id) => _map.TryGetValue(id, out var ind) ? ind : null;
        public IEnumerable<IIndicator> GetAll() => _map.Values;
    }
}

