using System;
using System.IO;
using System.Threading.Tasks;
using AVASDecoderChecker.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AVASDecoderChecker.Services
{
    public class SettingsService : ISettingsService
    {
        public string SettingsFilePath { get; }

        public SettingsService(string? customPath = null)
        {
            if (!string.IsNullOrWhiteSpace(customPath))
            {
                SettingsFilePath = customPath;
            }
            else
            {
                // Default location: config/settings.json relative to app directory or current directory
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string candidate1 = Path.Combine(baseDir, "config", "settings.json");
                string candidate2 = Path.Combine(Directory.GetCurrentDirectory(), "config", "settings.json");

                SettingsFilePath = File.Exists(candidate2) ? candidate2 : candidate1;
            }
        }

        public async Task<AppSettings> LoadSettingsAsync()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    string json = await File.ReadAllTextAsync(SettingsFilePath);
                    var settings = JsonConvert.DeserializeObject<AppSettings>(json);
                    if (settings != null)
                    {
                        return settings;
                    }
                }

                // Check for config.default.json as fallback
                string configDir = Path.GetDirectoryName(SettingsFilePath) ?? "config";
                string defaultPath = Path.Combine(configDir, "config.default.json");
                if (File.Exists(defaultPath))
                {
                    string defaultJson = await File.ReadAllTextAsync(defaultPath);
                    var jObj = JObject.Parse(defaultJson);
                    var settings = new AppSettings();

                    if (jObj["control_server"]?["ip"] != null)
                        settings.ControlServerIp = jObj["control_server"]!["ip"]!.ToString();

                    if (jObj["control_server"]?["port"] != null)
                        settings.ControlServerPort = jObj["control_server"]!["port"]!.Value<int>();

                    if (jObj["control_server"]?["timeout_ms"] != null)
                        settings.TimeoutMs = jObj["control_server"]!["timeout_ms"]!.Value<int>();

                    if (jObj["monitoring"]?["interval_seconds"] != null)
                        settings.PollingIntervalSeconds = jObj["monitoring"]!["interval_seconds"]!.Value<double>();

                    return settings;
                }
            }
            catch (Exception)
            {
                // Fallback to default in case of corrupted file
            }

            return new AppSettings();
        }

        public async Task SaveSettingsAsync(AppSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            string? dir = Path.GetDirectoryName(SettingsFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string json = JsonConvert.SerializeObject(settings, Formatting.Indented);
            await File.WriteAllTextAsync(SettingsFilePath, json);
        }
    }
}
