using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ProfitProphet.Data;
using ProfitProphet.Services;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace ProfitProphet
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public DataService DataService { get; private set; }
        public IAppSettingsService SettingsService { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // DB migráció induláskor
            using (var ctx = new StockContext())
            {
                //ctx.Database.Migrate();
                ctx.Database.EnsureCreated();
            }
            var cfgPath = Path.Combine(
           Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
           "ProfitProphet", "settings.json");

            DataService = new DataService(new StockContext());
            //SettingsService = new JsonAppSettingsService(cfgPath);
            SettingsService = new AppSettingsService(cfgPath);


#if DEBUG
            var dbPath = Path.Combine(AppContext.BaseDirectory, "Candles.db");
            using var c = new SqliteConnection($"Data Source={dbPath}");
            c.Open();

            using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' ORDER BY 1;";
            using var r = cmd.ExecuteReader();

            var names = new List<string>();
            while (r.Read()) names.Add(r.GetString(0));

            Debug.WriteLine($"DB: {c.DataSource}");
            Debug.WriteLine("Tables: " + string.Join(", ", names));
#endif
        }
    }
}
