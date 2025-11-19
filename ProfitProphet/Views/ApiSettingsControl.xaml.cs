using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ProfitProphet.Services;
using ProfitProphet.Settings;

namespace ProfitProphet.Views
{
    public partial class ApiSettingsControl : UserControl
    {
        private readonly IAppSettingsService _settingsService;
        private AppSettings _settings;

        public ApiSettingsControl(IAppSettingsService settingsService)
        {
            InitializeComponent();

            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _settings = _settingsService.LoadSettings() ?? new AppSettings();

            ApiComboBox.ItemsSource = new[] { "YahooFinance", "TwelveData", "AlphaVantage" };

            var api = string.IsNullOrWhiteSpace(_settings.SelectedApi)
                ? "YahooFinance"
                : _settings.SelectedApi;

            //ApiComboBox.SelectedItem = _settings.SelectedApi;
            ApiComboBox.SelectedItem = api;

            //ApiKeyBox.Text = _settings.SelectedApi switch
            //{
            //    "TwelveData" => _settings.TwelveDataApiKey,
            //    "AlphaVantage" => _settings.AlphaVantageApiKey,
            //    _ => _settings.YahooApiKey
            //};

            UpdateApiKeyBox(api);
        }

        private void ApiComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = ApiComboBox.SelectedItem?.ToString();
            if (string.IsNullOrWhiteSpace(selected))
                return;
            UpdateApiKeyBox(selected);
        }

        private void UpdateApiKeyBox(string apiName)
        {
            switch (apiName)
            {
                case "TwelveData":
                    ApiKeyBox.Text = _settings.TwelveDataApiKey ?? "";
                    break;
                case "AlphaVantage":
                    ApiKeyBox.Text = _settings.AlphaVantageApiKey ?? "";
                    break;
                default:
                    ApiKeyBox.Text = _settings.YahooApiKey ?? "";
                    break;
            }
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            var selected = ApiComboBox.SelectedItem?.ToString();
            if (string.IsNullOrWhiteSpace(selected))
            {
                MessageBox.Show("Válassz egy API-t!", "Hiba", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _settings.SelectedApi = selected;
            var key = ApiKeyBox.Text?.Trim() ?? "";

            switch (selected)
            {
                case "TwelveData":
                    _settings.TwelveDataApiKey = key;
                    break;
                case "AlphaVantage":
                    _settings.AlphaVantageApiKey = key;
                    break;
                default:
                    _settings.YahooApiKey = key;
                    break;
            }
            await _settingsService.SaveSettingsAsync(_settings);

            MessageBox.Show("Beállítások elmentve.", "Settings", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
