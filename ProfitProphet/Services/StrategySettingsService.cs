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
            _filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SettingsFileName);
            LoadSettingsFromFile();
        }

        public List<StrategyProfile> LoadProfiles()
        {
            return _currentSettings.Profiles;
        }

        public void SaveProfile(StrategyProfile profile)
        {
            if (profile == null) return;

            // Megkeressük, van-e már ilyen ID-jú vagy nevű/szimbólumú profil
            // Itt most egyszerűsítve a Symbol alapján frissítünk, ahogy kérted
            var existing = _currentSettings.Profiles.FirstOrDefault(p => p.Symbol == profile.Symbol);

            if (existing != null)
            {
                _currentSettings.Profiles.Remove(existing);
            }

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
                // Ha sérült a fájl, üreset hozunk létre
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
                File.WriteAllText(_filePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Hiba a mentésnél: {ex.Message}");
            }
        }
    }
}
