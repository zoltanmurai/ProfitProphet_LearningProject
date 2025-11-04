using Microsoft.EntityFrameworkCore;
using ProfitProphet.Data;
using ProfitProphet.DTOs;
using ProfitProphet.Entities;
using ProfitProphet.Mappers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProfitProphet.Services
{
    public sealed class ChartProfileService : IChartProfileService
    {
        private readonly StockContext _db;
        public ChartProfileService(StockContext db) => _db = db;

        public async Task<ChartProfileDto> GetOrCreateAsync(string symbol, string interval)
        {
            var e = await _db.ChartProfiles
                .FirstOrDefaultAsync(x => x.Symbol == symbol && x.Interval == interval);

            if (e == null)
            {
                e = new ChartProfile { Symbol = symbol, Interval = interval };
                _db.ChartProfiles.Add(e);
                await _db.SaveChangesAsync();
            }
            return e.ToDto();
        }

        public async Task SaveAsync(ChartProfileDto dto)
        {
            var e = await _db.ChartProfiles
                .FirstAsync(x => x.Symbol == dto.Symbol && x.Interval == dto.Interval);
            e.UpdateFromDto(dto);
            await _db.SaveChangesAsync();
        }
    }
}
