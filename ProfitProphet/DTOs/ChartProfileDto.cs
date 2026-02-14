using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProfitProphet.DTOs
{
    // ChartProfile DTO representing a saved chart configuration
    public class ChartProfileDto
    {
        // Trading symbol for the chart (e.g., "AAPL")
        public string Symbol { get; set; } = null!;

        // Time interval for candles (e.g., "D1", "H1")
        public string Interval { get; set; } = "D1";

        // List of configured indicators for this chart profile
        public List<IndicatorConfigDto> Indicators { get; set; } = new();
    }
}
