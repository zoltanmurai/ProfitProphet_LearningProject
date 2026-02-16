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

            // 2. BEÁLLÍTÁSOK ELŐRE BETÖLTÉSE (Hogy tudjunk dönteni)
            var cfgPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ProfitProphet", "settings.json");

            // Létrehozunk egy ideiglenes példányt, csak hogy kiolvassuk belőle az adatokat
            // (Még nem regisztráljuk a DI-ba, csak használjuk)
            var tempSettingsService = new AppSettingsService(cfgPath);
            var savedSettings = tempSettingsService.LoadSettings(); // <--- Itt vannak a mentett adatok!

            System.Diagnostics.Debug.WriteLine("---------------- DI DIAGNOSZTIKA ----------------");
            System.Diagnostics.Debug.WriteLine($"JSON Fájl helye: {cfgPath}");
            System.Diagnostics.Debug.WriteLine($"Beolvasott SelectedApi: '{savedSettings?.SelectedApi}'");
            //System.Diagnostics.Debug.WriteLine($"Beolvasott AlphaKulcs: '{savedSettings?.AlphaVantageApiKey}'");
            System.Diagnostics.Debug.WriteLine("-------------------------------------------------");

            // Most regisztráljuk a Service-t a többi osztály számára (pl. SettingsWindow)
            // Fontos: Azt a példányt adjuk át, amit már létrehoztunk, így nem tölti be kétszer.
            services.AddSingleton<IAppSettingsService>(tempSettingsService);

            // Opcionális: Magát a Settings objektumot is regisztrálhatod
            //services.AddSingleton<AppSettings>(provider => provider.GetRequiredService<IAppSettingsService>().CurrentSettings);

            //string selectedApi = savedSettings?.SelectedApi ?? "YahooFinance";

            //if (selectedApi == "AlphaVantage" && !string.IsNullOrWhiteSpace(savedSettings?.AlphaVantageApiKey))
            //{
            //    // Ha AlphaVantage van kiválasztva ÉS van hozzá kulcs
            //    string key = savedSettings.AlphaVantageApiKey;
            //    services.AddSingleton<IStockApiClient>(provider => new AlphaVantageClient(key));

            //    System.Diagnostics.Debug.WriteLine($">>> DI: AlphaVantage regisztrálva. Kulcs eleje: {key.Substring(0, Math.Min(3, key.Length))}...");
            //}
            //else if (selectedApi == "TwelveData" && !string.IsNullOrWhiteSpace(savedSettings?.TwelveDataApiKey))
            //{
            //    // Ha TwelveData van kiválasztva ÉS van hozzá kulcs
            //    string key = savedSettings.TwelveDataApiKey;
            //    services.AddSingleton<IStockApiClient>(provider => new TwelveDataClient(key));

            //    System.Diagnostics.Debug.WriteLine(">>> DI: TwelveData regisztrálva.");
            //}
            //else
            //{
            //    // Minden más esetben (YahooFinance, vagy ha a választotthoz nincs kulcs)
            //    services.AddSingleton<IStockApiClient, YahooFinanceClient>();

            //    System.Diagnostics.Debug.WriteLine(">>> DI: YahooFinance regisztrálva (Alapértelmezett).");
            //}

            services.AddSingleton<IStockApiClientFactory, StockApiClientFactory>();

            services.AddTransient<IStockApiClient>(provider =>
                provider.GetRequiredService<IStockApiClientFactory>().CreateClient());


            // 4. Egyéb szolgáltatások (Ezek jók voltak)
            services.AddSingleton<IIndicatorRegistry, IndicatorRegistry>();
            services.AddSingleton<ChartBuilder>();
            services.AddSingleton<DataService>();

            services.AddSingleton<IChartSettingsService, ChartSettingsService>();
            services.AddSingleton<IChartProfileService, ChartProfileService>();
            services.AddSingleton<IStrategySettingsService, StrategySettingsService>();

            services.AddSingleton<BacktestService>();
            services.AddSingleton<OptimizerService>();

            // 5. ViewModels
            services.AddSingleton<ChartViewModel>();
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<OptimizationViewModel>();

            // 6. Ablakok
            services.AddSingleton<MainWindow>();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 1. TÉMA BEÁLLÍTÁSA A MENTETT PREFERENCIA ALAPJÁN
            // Először elkérjük a beállításokat kezelő szervizt
            var settingsService = _serviceProvider.GetRequiredService<IAppSettingsService>();

            // Kiolvassuk, mit mentett a felhasználó (System, Light vagy Dark)
            // Ha nincs mentett, az alapértelmezett jön.
            var userPref = settingsService.CurrentSettings.ThemePreference;
            //var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();

            WindowStyleHelper.InitializeTheme(userPref);

            // az InitializeTheme() miatt nem kell:
            // Views.WindowStyleHelper.ApplyUserSelection(userPref);
            // Views.WindowStyleHelper.DetectSystemTheme();
            // Views.WindowStyleHelper.SetApplicationTheme(currentSystemTheme);
            // Views.WindowStyleHelper.StartListeningToSystemChanges();

            // 2. ADATBÁZIS ÉS EGYÉB INDÍTÁSI DOLGOK (MARAD A RÉGI)
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
            WindowStyleHelper.ApplyDarkTitleBar(mainWindow);
            mainWindow.Show();
        }
    }
}