using ProfitProphet.DTOs;
using ProfitProphet.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProfitProphet.Mappers
{
    public static class ChartProfileMappings
    {
        public static ChartProfileDto ToDto(this ChartProfile e) =>
            new()
            {
                Symbol = e.Symbol,
                Interval = e.Interval,
                Indicators = System.Text.Json.JsonSerializer
                    .Deserialize<List<IndicatorConfigDto>>(e.IndicatorsJson) ?? new()
            };

        public static void UpdateFromDto(this ChartProfile e, ChartProfileDto dto)
        {
            e.Symbol = dto.Symbol;
            e.Interval = dto.Interval;
            e.IndicatorsJson = System.Text.Json.JsonSerializer.Serialize(dto.Indicators);
            e.LastUpdatedUtc = DateTime.UtcNow;
        }
    }

}
