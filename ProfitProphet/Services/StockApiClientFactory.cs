using ProfitProphet.Services.APIs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProfitProphet.Services
{
    public class StockApiClientFactory : IStockApiClientFactory
    {
        private readonly IAppSettingsService _settingsService;

        public StockApiClientFactory(IAppSettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        public IStockApiClient CreateClient()
        {
            // 1. Mindig frissen betöltjük a beállításokat!
            // (Mivel a LoadSettings olvassa a fájlt, vagy a memóriában lévő friss CurrentSettings-et adja)
            var settings = _settingsService.LoadSettings();

            string selectedApi = settings?.SelectedApi ?? "YahooFinance";

            // 2. A döntési logika (ugyanaz, ami az App.xaml.cs-ben volt)
            if (selectedApi == "AlphaVantage" && !string.IsNullOrWhiteSpace(settings?.AlphaVantageApiKey))
            {
                return new AlphaVantageClient(settings.AlphaVantageApiKey);
            }
            else if (selectedApi == "TwelveData" && !string.IsNullOrWhiteSpace(settings?.TwelveDataApiKey))
            {
                return new TwelveDataClient(settings.TwelveDataApiKey);
            }

            // Alapértelmezett
            return new YahooFinanceClient();
        }
    }
}
