using ProfitProphet.Settings;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace ProfitProphet.Services
{
    public class AppSettingsService : IAppSettingsService
    {
        private readonly string _filePath;
        public AppSettings CurrentSettings { get; private set; }

        public AppSettingsService(string filePath)
        {
            _filePath = filePath;
            // FONTOS: Inicializáljuk a CurrentSettings-t a konstruktorban!
            CurrentSettings = LoadSettings();
        }

        public AppSettings LoadSettings()
        {
            try
            {
                // Ellenőrizzük, hogy létezik-e a könyvtár, ha nem, létrehozzuk
                var directory = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                if (!File.Exists(_filePath))
                    return new AppSettings();

                var json = File.ReadAllText(_filePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                return settings ?? new AppSettings();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"AppSettings betöltési hiba: {ex.Message}");
                return new AppSettings();
            }
        }

        public async Task SaveSettingsAsync(AppSettings settings)
        {
            try
            {
                // Ellenőrizzük, hogy létezik-e a könyvtár
                var directory = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                await File.WriteAllTextAsync(_filePath, json);

                // Frissítjük a CurrentSettings-t is a mentett értékkel
                CurrentSettings = settings;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"AppSettings mentési hiba: {ex.Message}");
                throw; // Újradobjuk a hibát, hogy a hívó oldal is tudjon róla
            }
        }
    }
}
