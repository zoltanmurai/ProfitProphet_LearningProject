using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProfitProphet.Models.Strategies
{
    public enum ComparisonOperator
    {
        [Description(">")]
        GreaterThan,        // Nagyobb mint (>)
        [Description(">=")]
        GreaterThanOrEqual, // Nagyobb vagy egyenlő (>=)
        [Description("<")]
        LessThan,           // Kisebb mint (<)
        [Description("<=")]
        LessThanOrEqual,    // Kisebb vagy egyenlő (<=)
        [Description("==")]
        Equals,             // Egyenlő (==)
        [Description("Cross Up")]
        CrossesAbove,       // Keresztezi felfelé (kitörés)
        [Description("Cross Down")]
        CrossesBelow        // Keresztezi lefelé (letörés)
    }

    // Mi van a jobb oldalon? Egy másik indikátor vagy egy fix szám?
    public enum DataSourceType
    {
        [Description("Indikátor")]
        Indicator,  // Pl. Mozgóátlag
        [Description("Fix érték")]
        Value       // Pl. 0 vagy 30 vagy 70
    }

    // Hol használjuk a szabályt?
    public enum RuleType
    {
        EntrySignal, // Belépés (Vétel)
        ExitSignal   // Kilépés (Eladás/Zárás)
    }
}
