using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProfitProphet.Entities
{
    // Ticker entity representing a tradable instrument
    public class Ticker
    {
        // Primary key
        public int Id { get; set; }

        // Trading symbol (e.g., "MSFT")
        public string Symbol { get; set; } = "";   // e.g., MSFT

        // Human-readable name (e.g., "Microsoft")
        public string Name { get; set; } = "";     // e.g., Microsoft

        // Navigation property: related candle records for this ticker
        public ICollection<Candle> Candles { get; set; } = new List<Candle>();
    }
}

