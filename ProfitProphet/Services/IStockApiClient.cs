using OxyPlot.Series;
using ProfitProphet.DTOs;
using ProfitProphet.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProfitProphet.Services
{
    public interface IStockApiClient
    {
        // Download OHLC candles for a given ticker
        //Task<List<HighLowItem>> GetIntradayData(string ticker, string apiKey = "");
        //Task<List<HighLowItem>> GetHistoricalData(string ticker, DateTime startDate, DateTime endDate, string interval = "1d", string apiKey = "");

        // TÖRTÉNETI adatok (CMF-hez kell a Volume) – ez lesz az alap
        Task<List<CandleDto>> GetHistoricalAsync(
            string symbol,
            string interval,
            DateTime? fromUtc = null,
            DateTime? toUtc = null);

        // Legfrissebb(ek) – opcionális count
        Task<List<CandleDto>> GetLatestAsync(
            string symbol,
            string interval,
            int? count = null);

    }
}

