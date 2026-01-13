using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProfitProphet.Models.Strategies
{
    public class StrategyProfile
    {
        public int Id { get; set; }
        public string Name { get; set; } = "Új Stratégia";
        public string Symbol { get; set; }

        // VÉTEL: Csoportok listája (VAGY kapcsolat a csoportok között)
        // Pl. (Trendkövető Csoport) VAGY (Fordulós Csoport)
        public List<StrategyGroup> EntryGroups { get; set; } = new List<StrategyGroup>();

        // ELADÁS: Csoportok listája (VAGY kapcsolat)
        // Pl. (StopLoss Csoport) VAGY (TakeProfit Csoport) VAGY (Indikátor Jelzés)
        public List<StrategyGroup> ExitGroups { get; set; } = new List<StrategyGroup>();

        public double BestScore { get; set; }
        public double WinRate { get; set; }
    }
}
