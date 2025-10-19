using ProfitProphet.Settings;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace ProfitProphet.Services
{
    public class AppSettingsService : IAppSettingsService
    {
        //private const string FileName = "AppSettings.json";
        private readonly string _filePath;

        public AppSettingsService(string filePath)
        {
            _filePath = filePath;
        }


        public AppSettings LoadSettings()
        {
            try
            {
                if (!File.Exists(_filePath))
                    return new AppSettings();

                var json = File.ReadAllText(_filePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                return settings ?? new AppSettings();
            }
            catch
            {
                return new AppSettings();
            }
        }

        public async Task SaveSettingsAsync(AppSettings settings)
        {
            try
            {
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                await File.WriteAllTextAsync(_filePath, json);
            }
            catch (Exception ex)
            {
                // opcionálisan logolható
                Console.WriteLine($"AppSettings mentési hiba: {ex.Message}");
            }
        }
    }
}
