using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using ProfitProphet.Data;
using ProfitProphet.Services;
using ProfitProphet.Services.APIs;
using ProfitProphet.Services.Charting;
using ProfitProphet.Services.Indicators;
using ProfitProphet.ViewModels;
using ProfitProphet.Views;
using System;
using System.IO;
using System.Windows;

namespace ProfitProphet
{
    public partial class App : Application
    {
        private IServiceProvider _serviceProvider;

        public App()
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // 1. Adatbázis
            services.AddDbContext<StockContext>();

            // 2. Szolgáltatások (Singleton = egyetlen közös példány)
            services.AddSingleton<ChartBuilder>(); // <-- Itt a lényeg a nyilakhoz!
            services.AddSingleton<DataService>();
            services.AddSingleton<IStockApiClient, YahooFinanceClient>();
            services.AddSingleton<IIndicatorRegistry, IndicatorRegistry>();
            services.AddSingleton<IChartSettingsService, ChartSettingsService>();
            services.AddSingleton<IChartProfileService, ChartProfileService>();
            services.AddSingleton<IStrategySettingsService, StrategySettingsService>();

            // Beállítások útvonala
            var cfgPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ProfitProphet", "settings.json");
            services.AddSingleton<IAppSettingsService>(provider => new AppSettingsService(cfgPath));

            // 3. ViewModels
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<ChartViewModel>();

            // 4. Ablakok
            services.AddSingleton<MainWindow>();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Adatbázis biztosítása
            using (var scope = _serviceProvider.CreateScope())
            {
                try
                {
                    var ctx = scope.ServiceProvider.GetRequiredService<StockContext>();
                    ctx.Database.EnsureCreated();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Adatbázis hiba induláskor: {ex.Message}");
                }
            }

            // Kézi indítás (StartupUri helyett)
            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }
    }
}