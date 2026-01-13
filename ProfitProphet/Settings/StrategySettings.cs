using ProfitProphet.Models.Strategies;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProfitProphet.Settings
{
    public class StrategySettings
    {
        // Ez tárolja az összes mentett stratégiát
        public List<StrategyProfile> Profiles { get; set; } = new List<StrategyProfile>();
    }
}
