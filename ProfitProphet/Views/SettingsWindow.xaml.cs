using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ProfitProphet.Services;

namespace ProfitProphet.Views
{
    public partial class SettingsWindow : Window
    {
        private readonly DataService _dataService;
        private readonly IAppSettingsService _settingsService;

        public SettingsWindow(DataService dataService, IAppSettingsService settings)
        {
            InitializeComponent();

            _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
            _settingsService = settings ?? throw new ArgumentNullException(nameof(settings));


            Sidebar.SelectionChanged += Sidebar_SelectionChanged;
            // első oldal
            Sidebar.SelectedIndex = 0;
        }

        private void Sidebar_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Sidebar.SelectedItem is not ListBoxItem lbi) return;
            var tag = (lbi.Tag as string) ?? "ApiSettings";
            LoadContent(tag);
        }

        private void LoadContent(string tag)
        {
            object content = tag switch
            {
                "ApiSettings" => new ApiSettingsControl(_settingsService),
                "DataImport"  => new DataImportControl(_dataService, _settingsService),
                _ => new TextBlock { Text = "Coming soon...", Foreground = Brushes.Gray }
            };
            ContentArea.Content = content;
        }
    }
}
