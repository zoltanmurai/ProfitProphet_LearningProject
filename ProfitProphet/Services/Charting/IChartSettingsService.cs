using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProfitProphet.Services.Charting
{
    using ProfitProphet.Models.Charting;

    public interface IChartSettingsService
    {
        ChartSettings GetForSymbol(string symbol);
        void Save(ChartSettings settings);
    }
}
