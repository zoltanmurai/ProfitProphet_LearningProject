using ProfitProphet.Models.Strategies;
using ProfitProphet.Settings;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProfitProphet.Services
{
    public class StrategySettingsService : IStrategySettingsService
    {
        private const string SettingsFileName = "strategies.json";
        private readonly string _filePath;
        private StrategySettings _currentSettings;

        public StrategySettingsService()
        {
            // 1. Megkeressük a szabványos AppData/Local mappát
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            // 2. Hozzáfűzzük a program nevét ("ProfitProphet")
            string folderPath = Path.Combine(appData, "ProfitProphet");

            // 3. HA NEM LÉTEZIK, LÉTREHOZZUK! (Ez hiányzott, ezért nem tudott menteni)
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            // 4. Összerakjuk a teljes fájl útvonalat
            _filePath = Path.Combine(folderPath, SettingsFileName);

            LoadSettingsFromFile();
        }

        public List<StrategyProfile> LoadProfiles()
        {
            return _currentSettings.Profiles;
        }

        public void SaveProfile(StrategyProfile profile)
        {
            if (profile == null) return;

            // Ha már van ilyen szimbólumhoz mentés, kivesszük a régit
            var existing = _currentSettings.Profiles.FirstOrDefault(p => p.Symbol == profile.Symbol);

            if (existing != null)
            {
                _currentSettings.Profiles.Remove(existing);
            }

            // Betesszük a frisset
            _currentSettings.Profiles.Add(profile);

            SaveSettingsToFile();
        }

        public void DeleteProfile(StrategyProfile profile)
        {
            if (profile != null && _currentSettings.Profiles.Remove(profile))
            {
                SaveSettingsToFile();
            }
        }

        private void LoadSettingsFromFile()
        {
            // Ha a fájl nem létezik, üres listával indulunk
            if (!File.Exists(_filePath))
            {
                _currentSettings = new StrategySettings();
                return;
            }

            try
            {
                string json = File.ReadAllText(_filePath);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new JsonStringEnumConverter() }
                };
                _currentSettings = JsonSerializer.Deserialize<StrategySettings>(json, options) ?? new StrategySettings();
            }
            catch
            {
                // Ha sérült a fájl, inkább újat kezdünk, minthogy összeomoljon
                _currentSettings = new StrategySettings();
            }
        }

        private void SaveSettingsToFile()
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Converters = { new JsonStringEnumConverter() }
                };
                string json = JsonSerializer.Serialize(_currentSettings, options);

                // MENTÉS
                File.WriteAllText(_filePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"KRITIKUS HIBA a mentésnél: {ex.Message}");
            }
        }
    }
}