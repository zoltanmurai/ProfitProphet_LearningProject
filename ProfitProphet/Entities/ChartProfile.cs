using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProfitProphet.Entities
{
    // ChartProfile entity representing a saved chart configuration
    public class ChartProfile
    {
        // Primary key
        public int Id { get; set; }

        // Trading symbol (e.g., "AAPL")
        public string Symbol { get; set; } = null!;

        // Candle interval for the chart (e.g., "D1", "H1")
        public string Interval { get; set; } = "D1";

        // Serialized indicators configuration stored as JSON
        public string IndicatorsJson { get; set; } = "[]";

        // UTC timestamp of the last update to this profile
        public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
    }
}
