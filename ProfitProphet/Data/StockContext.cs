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
            // Location of AppData\Local folder
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dbDir = System.IO.Path.Combine(appData, "ProfitProphet");

            // Ensure the database directory exists
            System.IO.Directory.CreateDirectory(dbDir);

            // Path to DB file (AppData\Local\ProfitProphet\Candles.db)
            var dbPath = System.IO.Path.Combine(dbDir, "Candles.db");

            // Configure SQLite using the file path
            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }

        protected override void OnModelCreating(ModelBuilder mb)
        {
            base.OnModelCreating(mb);

            // Configure Candle entity mappings
            mb.Entity<Candle>(e =>
            {
                // Map entity to "Candles" table
                e.ToTable("Candles"); 

                // Unique index on Symbol, TimestampUtc and Timeframe
                e.HasIndex(x => new { x.Symbol, x.TimestampUtc, x.Timeframe }).IsUnique();

                // Set precision for numeric price fields
                e.Property(x => x.Open).HasPrecision(18, 6);
                e.Property(x => x.High).HasPrecision(18, 6);
                e.Property(x => x.Low).HasPrecision(18, 6);
                e.Property(x => x.Close).HasPrecision(18, 6);

                // Store Timeframe as string and require it
                e.Property(x => x.Timeframe).HasConversion<string>().IsRequired();
            });

            // Configure ChartProfile entity mappings
            mb.Entity<ChartProfile>(b =>
            {
                // Primary key
                b.HasKey(x => x.Id);

                // Symbol is required with max length 32
                b.Property(x => x.Symbol).IsRequired().HasMaxLength(32);

                // Interval is required with max length 8
                b.Property(x => x.Interval).IsRequired().HasMaxLength(8);

                // Indicators JSON is required
                b.Property(x => x.IndicatorsJson).IsRequired();

                // Unique (Symbol, Interval) - one chart profile per symbol×interval
                b.HasIndex(x => new { x.Symbol, x.Interval }).IsUnique();
            });
        }
    }
}

