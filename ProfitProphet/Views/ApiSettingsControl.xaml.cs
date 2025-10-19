using System;
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
            _settings = _settingsService.LoadSettings();

            ApiComboBox.ItemsSource = new[] { "YahooFinance", "TwelveData", "AlphaVantage" };
            ApiComboBox.SelectedItem = _settings.SelectedApi;

            ApiKeyBox.Text = _settings.SelectedApi switch
            {
                "TwelveData" => _settings.TwelveDataApiKey,
                "AlphaVantage" => _settings.AlphaVantageApiKey,
                _ => _settings.YahooApiKey
            };
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var selected = ApiComboBox.SelectedItem?.ToString();
            if (string.IsNullOrWhiteSpace(selected))
            {
                MessageBox.Show("Válassz egy API-t!", "Hiba", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _settings.SelectedApi = selected;

            switch (selected)
            {
                case "TwelveData":
                    _settings.TwelveDataApiKey = ApiKeyBox.Text?.Trim() ?? "";
                    break;
                case "AlphaVantage":
                    _settings.AlphaVantageApiKey = ApiKeyBox.Text?.Trim() ?? "";
                    break;
                default:
                    _settings.YahooApiKey = ApiKeyBox.Text?.Trim() ?? "";
                    break;
            }
            _settingsService.SaveSettingsAsync(_settings);

            MessageBox.Show("Beállítások elmentve.", "Settings", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
