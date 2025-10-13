using System;
using System.IO;
using System.Text.Json;

namespace ProfitProphet.Services
{
    public class JsonAppSettingsService : IAppSettingsService
    {
        private readonly string _path;

        public JsonAppSettingsService(string path)
        {
            _path = path;
        }

        public AppSettings Load()
        {
            if (!File.Exists(_path))
                return new AppSettings();

            var json = File.ReadAllText(_path);
            return System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json)
                   ?? new AppSettings();
        }

        public void Save(AppSettings settings)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var json = System.Text.Json.JsonSerializer.Serialize(settings, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(_path, json);
        }
    }
}
