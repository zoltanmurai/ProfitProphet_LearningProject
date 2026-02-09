using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using ProfitProphet.Data;
using ProfitProphet.Services;
using ProfitProphet.Services.APIs;
using ProfitProphet.Services.Charting;
using ProfitProphet.Services.Indicators;
using ProfitProphet.ViewModels;
using ProfitProphet.Views;
using ProfitProphet.Settings; // Fontos a Settings névtér!
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

            // 2. Alap szolgáltatások
            // Beállítások útvonala
            var cfgPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ProfitProphet", "settings.json");

            // Regisztráljuk a Service-t (aki ír/olvas)
            services.AddSingleton<IAppSettingsService>(provider => new AppSettingsService(cfgPath));

            // FONTOS: Regisztráljuk magát a Beállítás Objektumot is!
            // Így ha a ChartViewModel 'AppSettings'-t kér (nem a service-t), akkor a DI konténer tudni fogja, mit adjon.
            services.AddSingleton<AppSettings>(provider => provider.GetRequiredService<IAppSettingsService>().CurrentSettings);

            // 3. Üzleti logika és Indikátorok
            services.AddSingleton<IIndicatorRegistry, IndicatorRegistry>(); // <--- Ez kell a modularitáshoz!
            services.AddSingleton<ChartBuilder>(); // <-- Ő majd kéri a Registry-t, és meg is kapja
            services.AddSingleton<DataService>();
            services.AddSingleton<IStockApiClient, YahooFinanceClient>();

            services.AddSingleton<IChartSettingsService, ChartSettingsService>();
            services.AddSingleton<IChartProfileService, ChartProfileService>();
            services.AddSingleton<IStrategySettingsService, StrategySettingsService>();

            services.AddSingleton<BacktestService>();
            services.AddSingleton<OptimizerService>();

            // 4. ViewModels (Automatikusan megkapják a fenti service-eket)
            services.AddSingleton<ChartViewModel>(); // Ő kéri: ChartSettings, Builder, AppSettings, Registry
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<OptimizationViewModel>(); // Ha kell

            // 5. Ablakok
            // Singleton, hogy ne vesszenek el az adatok bezárás/újranyitás között (opcionális, lehet Transient is)
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

            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }
    }
}