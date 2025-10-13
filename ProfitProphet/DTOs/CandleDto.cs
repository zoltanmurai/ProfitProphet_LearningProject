using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProfitProphet.DTOs
{
    public record CandleDto(
        string Symbol,
        DateTime TimestampUtc,
        decimal Open,
        decimal High,
        decimal Low,
        decimal Close,
        long? Volume
    );
}
