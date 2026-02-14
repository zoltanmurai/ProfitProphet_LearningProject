using System;
using System.Windows;
using System.Windows.Controls;
using ProfitProphet.Services;
using ProfitProphet.Entities;

namespace ProfitProphet.Views
{
    public partial class AppearanceSettingsControl : UserControl
    {
        private readonly IAppSettingsService _settingsService;

        public AppearanceSettingsControl(IAppSettingsService settingsService)
        {
            InitializeComponent();
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

            // Betöltjük a mentett beállítást
            LoadCurrentTheme();
        }

        private void LoadCurrentTheme()
        {
            if (_settingsService?.CurrentSettings == null) return;

            var savedTheme = _settingsService.CurrentSettings.ThemePreference;

            switch (savedTheme)
            {
                case ThemeSelection.Light:
                    RadioLight.IsChecked = true;
                    break;
                case ThemeSelection.Dark:
                    RadioDark.IsChecked = true;
                    break;
                case ThemeSelection.System:
                default:
                    RadioSystem.IsChecked = true;
                    break;
            }
        }

        private async void RadioTheme_Checked(object sender, RoutedEventArgs e)
        {
            if (_settingsService == null) return;

            if (sender is not RadioButton rb) return;

            ThemeSelection selection = rb.Name switch
            {
                nameof(RadioLight) => ThemeSelection.Light,
                nameof(RadioDark) => ThemeSelection.Dark,
                _ => ThemeSelection.System
            };

            // Alkalmazás
            WindowStyleHelper.ApplyUserSelection(selection);

            // Mentés
            if (_settingsService.CurrentSettings != null)
            {
                _settingsService.CurrentSettings.ThemePreference = selection;
                await _settingsService.SaveSettingsAsync(_settingsService.CurrentSettings);
            }
        }
    }
}