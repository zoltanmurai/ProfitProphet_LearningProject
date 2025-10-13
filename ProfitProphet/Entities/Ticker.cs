using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProfitProphet.Entities
{
    public class Ticker
    {
        public int Id { get; set; }
        public string Symbol { get; set; } = "";   // pl. MSFT
        public string Name { get; set; } = "";     // pl. Microsoft

        // Reláció
        public ICollection<Candle> Candles { get; set; } = new List<Candle>();
    }
}

