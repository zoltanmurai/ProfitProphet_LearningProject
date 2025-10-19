using ProfitProphet.Entities;
using ProfitProphet.Services;
using ProfitProphet.Services.APIs;
using ProfitProphet.Settings;
using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ProfitProphet.Views
{
    public partial class DataImportControl : UserControl
    {
        private readonly DataService _dataService;
        private readonly IAppSettingsService _settingsService;
        private readonly AppSettings _settings;
        private bool _isImporting = false;
        private bool _initializing = true;

        public DataImportControl(DataService dataService, IAppSettingsService settings)
        {
            InitializeComponent();
            _dataService = dataService;
            _settingsService = settings;
            _settings = _settingsService.LoadSettings();
            DataContext = _settings;
            SetLookbackSelection(_settings.LookbackPeriodDays);
            //AutoImportCheckBox.IsChecked = _settings.AutoDataImport;
            _initializing = false;
        }

        private void SetLookbackSelection(int days)
        {
            var item = LookbackCombo.Items.OfType<ComboBoxItem>()
                .FirstOrDefault(i => i.Content.ToString().StartsWith(days.ToString()));
            if (item != null)
                LookbackCombo.SelectedItem = item;
        }

        private async void LookbackCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_initializing) return;
            _settings.LookbackPeriodDays = GetSelectedLookbackDays();
            await _settingsService.SaveSettingsAsync(_settings);
        }

        private int GetSelectedLookbackDays()
        {
            var content = (LookbackCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();
            if (content == null) return 200;
            if (content.Contains("200")) return 200;
            if (content.Contains("350")) return 350;
            if (content.Contains("720")) return 720;
            if (content.Contains("12")) return 365;
            if (content.Contains("24")) return 730;
            return 200;
        }

        private async void AutoImportCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (_initializing) return;
            _settings.AutoDataImport = AutoImportCheckBox.IsChecked == true;
            await _settingsService.SaveSettingsAsync(_settings);
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
            if (_isImporting)
                return;

            _isImporting = true;
            var button = (Button)sender;
            button.IsEnabled = false;
            StatusTextBlock.Foreground = Brushes.LightGray;
            StatusTextBlock.Text = "Importálás elindítva...";

            // --- Beállítások mentése ---
            _settings.AutoDataImport = AutoImportCheckBox.IsChecked == true;
            _settings.LookbackPeriodDays = GetSelectedLookbackDays(); // a korábban definiált metódusod
            await _settingsService.SaveSettingsAsync(_settings);

            var lookback = (LookbackCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "200 days";
            var (fromUtc, toUtc) = ParseLookback(lookback);

            var watch = _settings.Watchlist?.Distinct().ToList() ?? new();
            if (watch.Count == 0)
            {
                MessageBox.Show("A watchlist üres. Add hozzá a tickereket a beállításokban vagy a főablakban.", "Import", MessageBoxButton.OK, MessageBoxImage.Information);
                _isImporting = false;
                button.IsEnabled = true;
                StatusTextBlock.Text = "Import megszakítva – nincs szimbólum.";
                return;
            }

            var client = CreateClient();
            var interval = _settings.DefaultInterval ?? "1d";

            int ok = 0, fail = 0;
            int total = watch.Count;
            int done = 0;

            foreach (var symbol in watch)
            {
                try
                {
                    StatusTextBlock.Text = $"Importálás: {symbol} ({++done}/{total})...";
                    await Task.Delay(150); // kis delay

                    var dtoList = await client.GetHistoricalAsync(symbol, interval, fromUtc, toUtc);
                    await _dataService.SaveCandlesAsync(interval, dtoList);
                    ok++;
                }
                catch (Exception ex)
                {
                    fail++;
                    //  logolas majd egyszer:
                    // _logService.LogError($"Import hiba {symbol}: {ex.Message}");
                }
            }

            StatusTextBlock.Foreground = fail == 0 ? Brushes.LightGreen : Brushes.OrangeRed;
            StatusTextBlock.Text = $"Import befejezve. Sikeres: {ok}, Sikertelen: {fail}.";
            MessageBox.Show($"Kész: {ok} sikeres, {fail} sikertelen.", "Import", MessageBoxButton.OK, MessageBoxImage.Information);

            _isImporting = false;
            button.IsEnabled = true;
        }

    }
}
