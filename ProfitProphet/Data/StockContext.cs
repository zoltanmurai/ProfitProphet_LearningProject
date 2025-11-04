using Microsoft.EntityFrameworkCore;
using ProfitProphet.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace ProfitProphet.Data
{
    public class StockContext : DbContext
    {
        //public StockContext(DbContextOptions<StockContext> options) : base(options) { }

        public DbSet<ChartProfile> ChartProfiles { get; set; } = null!;
        public DbSet<Ticker> Tickers { get; set; }
        public DbSet<Candle> Candles { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // AppData\Local mappa helye
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dbDir = System.IO.Path.Combine(appData, "ProfitProphet");

            // létrehozom a mappát, ha nem létezne
            System.IO.Directory.CreateDirectory(dbDir);

            //  AppData\Local\ProfitProphet
            var dbPath = System.IO.Path.Combine(dbDir, "Candles.db");

            //  konfiguráció
            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }

        protected override void OnModelCreating(ModelBuilder mb)
        {
            base.OnModelCreating(mb);

            mb.Entity<Candle>(e =>
            {
                //e.Property(x => x.Timeframe).HasConversion<string>().IsRequired();
                //e.HasIndex(x => new { x.Symbol, x.TimestampUtc, x.Timeframe }).IsUnique();
                //e.Property(x => x.Open).HasPrecision(18, 6);
                //e.Property(x => x.High).HasPrecision(18, 6);
                //e.Property(x => x.Low).HasPrecision(18, 6);
                //e.Property(x => x.Close).HasPrecision(18, 6);

                e.ToTable("Candles"); 
                e.HasIndex(x => new { x.Symbol, x.TimestampUtc, x.Timeframe }).IsUnique();
                e.Property(x => x.Open).HasPrecision(18, 6);
                e.Property(x => x.High).HasPrecision(18, 6);
                e.Property(x => x.Low).HasPrecision(18, 6);
                e.Property(x => x.Close).HasPrecision(18, 6);
                e.Property(x => x.Timeframe).HasConversion<string>().IsRequired();
            });

            mb.Entity<ChartProfile>(b =>
            {
                b.HasKey(x => x.Id);
                b.Property(x => x.Symbol).IsRequired().HasMaxLength(32);
                b.Property(x => x.Interval).IsRequired().HasMaxLength(8);
                b.Property(x => x.IndicatorsJson).IsRequired();

                // egyedi (Symbol, Interval) – egy chartprofil / symbol×időkeret
                b.HasIndex(x => new { x.Symbol, x.Interval }).IsUnique();
            });
        }
    }
}

