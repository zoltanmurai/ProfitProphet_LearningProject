using ProfitProphet.Entities;
using ProfitProphet.Services;
using ProfitProphet.Services.APIs;
using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ProfitProphet.Views
{
    public partial class DataImportControl : UserControl
    {
        private readonly DataService _dataService;
        private readonly IAppSettingsService _settingsService;
        private readonly AppSettings _settings;

        public DataImportControl(DataService dataService, IAppSettingsService settings)
        {
            InitializeComponent();
            //_data = dataService ?? throw new ArgumentNullException(nameof(dataService));
            //_settings = settings ?? throw new ArgumentNullException(nameof(settings));
            //_settings = _settingsService.Load();
            LookbackCombo.SelectedIndex = 0;
            _dataService = dataService;
            _settingsService = settings;

            _settings = _settingsService.Load();
        }

        private (DateTime fromUtc, DateTime toUtc) ParseLookback(string s)
        {
            var to = DateTime.UtcNow;
            if (string.IsNullOrWhiteSpace(s)) return (to.AddDays(-200), to);

            s = s.Trim().ToLowerInvariant();
            if (s.EndsWith("days") && int.TryParse(new string(s.Where(char.IsDigit).ToArray()), out int d))
                return (to.AddDays(-d), to);
            if (s.EndsWith("months") && int.TryParse(new string(s.Where(char.IsDigit).ToArray()), out int m))
                return (to.AddMonths(-m), to);
            if (s.EndsWith("years") && int.TryParse(new string(s.Where(char.IsDigit).ToArray()), out int y))
                return (to.AddYears(-y), to);

            return (to.AddDays(-200), to);
        }

        private Services.IStockApiClient CreateClient()
        {
            return _settings.SelectedApi switch
            {
                "TwelveData"   => new TwelveDataClient(_settings.TwelveDataApiKey),
                "AlphaVantage" => new AlphaVantageClient(_settings.AlphaVantageApiKey),
                _              => new YahooFinanceClient()
            };
        }

        private async void StartImport_Click(object sender, RoutedEventArgs e)
        {
            var lookback = (LookbackCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "200 days";
            var (fromUtc, toUtc) = ParseLookback(lookback);

            var watch = _settings.Watchlist?.Distinct().ToList() ?? new();
            if (watch.Count == 0)
            {
                MessageBox.Show("A watchlist üres. Add hozzá a tickereket a beállításokban vagy a főablakban.", "Import", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var client = CreateClient();
            var interval = _settings.DefaultInterval ?? "1d";

            int ok = 0, fail = 0;
            foreach (var symbol in watch)
            {
                try
                {
                    var bars = await client.GetHistoricalAsync(symbol, interval, fromUtc, toUtc);
                    // DTO → Entity konverzió
                    //var entities = bars.Select(dto => new Candle
                    //{
                    //    Symbol = dto.Symbol,
                    //    TimestampUtc = dto.TimestampUtc,
                    //    Open = dto.Open,
                    //    High = dto.High,
                    //    Low = dto.Low,
                    //    Close = dto.Close,
                    //    Volume = dto.Volume ?? 0,
                    //    Timeframe = Enum.Parse<Timeframe>(interval, ignoreCase: true)
                    //}).ToList();
                    //await _dataService.SaveCandlesAsync(interval, entities);
                    var dtoList = await client.GetHistoricalAsync(symbol, interval, fromUtc, toUtc);
                    await _dataService.SaveCandlesAsync(interval, dtoList); // a mapper és a mentés bent van a DataService-ben
                    ok++;
                }
                catch (Exception ex)
                {
                    fail++;
                    // opcionálisan log
                }
            }

            MessageBox.Show($"Kész: {ok} sikeres, {fail} sikertelen.", "Import", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
