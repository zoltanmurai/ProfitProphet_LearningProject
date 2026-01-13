using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProfitProphet.Models.Strategies
{
    public class OptimizationRange
    {
        public string ParameterName { get; set; } // Pl: "CMF Period"
        public int StartValue { get; set; }       // 10
        public int EndValue { get; set; }         // 60
        public int Step { get; set; }             // 2

        // Referencia a szabályra, amit éppen tekerünk
        public StrategyRule TargetRule { get; set; }
        public bool IsLeftSide { get; set; }      // A bal vagy a jobb oldali periódust állítjuk?
    }
}
