using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProfitProphet.Models.Strategies
{
    public enum ComparisonOperator
    {
        GreaterThan,        // Nagyobb mint (>)
        LessThan,           // Kisebb mint (<)
        Equals,             // Egyenlő (==)
        CrossesAbove,       // Keresztezi felfelé (kitörés)
        CrossesBelow        // Keresztezi lefelé (letörés)
    }

    // Mi van a jobb oldalon? Egy másik indikátor vagy egy fix szám?
    public enum DataSourceType
    {
        Indicator,  // Pl. Mozgóátlag
        Value       // Pl. 0 vagy 30 vagy 70
    }

    // Hol használjuk a szabályt?
    public enum RuleType
    {
        EntrySignal, // Belépés (Vétel)
        ExitSignal   // Kilépés (Eladás/Zárás)
    }
}
