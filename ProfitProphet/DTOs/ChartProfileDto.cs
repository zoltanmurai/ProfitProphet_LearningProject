using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProfitProphet.DTOs
{
    public class ChartProfileDto
    {
        public string Symbol { get; set; } = null!;
        public string Interval { get; set; } = "D1";
        public List<IndicatorConfigDto> Indicators { get; set; } = new();
    }
}
