using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProfitProphet.Models.Strategies
{
    public class StrategyRule
    {
        // --- BAL OLDAL (Mindig egy indikátor vagy árfolyam adat) ---
        public string LeftIndicatorName { get; set; } // Pl. "CMF", "Close", "RSI"
        public int LeftPeriod { get; set; }           // Pl. 20

        // --- KÖZÉP (Operátor) ---
        public ComparisonOperator Operator { get; set; } // Pl. CrossesAbove

        // --- JOBB OLDAL (Lehet indikátor VAGY fix szám) ---
        public DataSourceType RightSourceType { get; set; } = DataSourceType.Value;

        // Ha indikátor van a jobb oldalon:
        public string RightIndicatorName { get; set; } // Pl. "SMA"
        public int RightPeriod { get; set; }           // Pl. 50

        // Ha fix szám van a jobb oldalon:
        public double RightValue { get; set; }         // Pl. 0

        // Megjelenítéshez (hogy olvasható legyen a listában)
        public override string ToString()
        {
            string left = $"{LeftIndicatorName}({LeftPeriod})";
            string op = Operator switch
            {
                ComparisonOperator.GreaterThan => ">",
                ComparisonOperator.LessThan => "<",
                ComparisonOperator.Equals => "=",
                ComparisonOperator.CrossesAbove => "Keresztezi Felfelé",
                ComparisonOperator.CrossesBelow => "Keresztezi Lefelé",
                _ => "?"
            };
            string right = RightSourceType == DataSourceType.Indicator
                ? $"{RightIndicatorName}({RightPeriod})"
                : $"{RightValue}";

            return $"{left} {op} {right}";
        }
    }
}
