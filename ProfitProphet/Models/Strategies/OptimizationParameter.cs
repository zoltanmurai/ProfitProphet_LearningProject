using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProfitProphet.Models.Strategies
{
    public class OptimizationParameter
    {
        public StrategyRule Rule { get; set; }
        public bool IsEntrySide { get; set; } // true = Entry, false = Exit
        public string ParameterName { get; set; }
        public double MinValue { get; set; }
        public double MaxValue { get; set; }
    }
}
