using System.Collections.Generic;

namespace ProfitProphet.Settings
{
    public class AppSettings
    {
        public string SelectedApi { get; set; } = "YahooFinance";
        public string DefaultInterval { get; set; } = "1d";
        public string YahooApiKey { get; set; } = "";
        public string TwelveDataApiKey { get; set; } = "";
        public string AlphaVantageApiKey { get; set; } = "";
        public List<string> Watchlist { get; set; } = new();
        public int LookbackPeriodDays { get; set; } = 200;
        public bool AutoDataImport { get; set; } = false;
    }
}
