using System;
using System.IO;
using System.Text.Json;
using TransparentTaskbar.Models;

namespace TransparentTaskbar.Services
{
    public static class ConfigService
    {
        private const string AppFolderName = "TransparentTaskbar";
        private const string ConfigFileName = "config.json";

        public static string ConfigFilePath
        {
            get
            {
                // Portable mode: check if config.json exists next to exe
                string exeDir = AppDomain.CurrentDomain.BaseDirectory;
                string localConfig = Path.Combine(exeDir, ConfigFileName);
                if (File.Exists(localConfig))
                {
                    return localConfig;
                }

                // Standard install mode: %AppData%\TransparentTaskbar\config.json
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string folder = Path.Combine(appData, AppFolderName);
                if (!Directory.Exists(folder))
                {
                    try
                    {
                        Directory.CreateDirectory(folder);
                    }
                    catch
                    {
                        // Fallback to local directory if AppData fails
                        return localConfig;
                    }
                }
                return Path.Combine(folder, ConfigFileName);
            }
        }

        public static AppConfig Load()
        {
            string path = ConfigFilePath;
            try
            {
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    var config = JsonSerializer.Deserialize<AppConfig>(json);
                    if (config != null)
                    {
                        return config;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load config: {ex.Message}");
            }

            return new AppConfig();
        }

        public static bool Save(AppConfig config)
        {
            try
            {
                string path = ConfigFilePath;
                string? dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(config, options);
                File.WriteAllText(path, json);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save config: {ex.Message}");
                return false;
            }
        }
    }
}
