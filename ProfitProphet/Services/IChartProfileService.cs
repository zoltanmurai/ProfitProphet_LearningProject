using ProfitProphet.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProfitProphet.Services
{
    internal interface IChartProfileService
    {
        Task<ChartProfileDto> GetOrCreateAsync(string symbol, string interval);
        Task SaveAsync(ChartProfileDto dto);
    }
}
