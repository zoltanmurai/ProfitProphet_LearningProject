using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProfitProphet.Models.Strategies
{
    public class StrategyProfile
    {
        public int Id { get; set; } // DB azonosító
        public string Name { get; set; } = "Új Stratégia";
        public string Symbol { get; set; } // Melyik részvényhez tartozik (pl. "MSFT")

        // Mikor lépjünk be? (ÉS kapcsolat: minden szabálynak teljesülnie kell)
        public List<StrategyRule> EntryRules { get; set; } = new List<StrategyRule>();

        // Mikor lépjünk ki? (ÉS kapcsolat vagy VAGY kapcsolat - ezt majd a motor dönti el)
        public List<StrategyRule> ExitRules { get; set; } = new List<StrategyRule>();

        // Utolsó optimalizálás eredménye (hogy tudd, mennyire volt jó ez a beállítás)
        public double BestScore { get; set; }
        public double WinRate { get; set; }
    }
}
