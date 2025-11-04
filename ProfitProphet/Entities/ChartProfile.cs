using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProfitProphet.Entities
{
    public class ChartProfile
    {
        public int Id { get; set; }
        public string Symbol { get; set; } = null!;
        public string Interval { get; set; } = "D1";
        public string IndicatorsJson { get; set; } = "[]";
        public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
    }
}
