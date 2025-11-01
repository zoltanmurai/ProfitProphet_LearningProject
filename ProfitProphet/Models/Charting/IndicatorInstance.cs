using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProfitProphet.Models.Charting
{
    public sealed class IndicatorInstance
    {
        public string IndicatorId { get; set; } = "";
        public IndicatorParams Params { get; set; } = new();
        public bool IsVisible { get; set; } = true;
        public string Pane { get; set; } = "main"; // később: "sub"
    }
}
