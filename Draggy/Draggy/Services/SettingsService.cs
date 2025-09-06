using System;
using System.IO;
using System.Text.Json;

namespace Draggy.Services
{
    internal static class SettingsService
    {
        private static readonly string SettingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Draggy");

        private static readonly string SettingsFilePath = Path.Combine(SettingsDirectory, "settings.json");

        private class AppSettings
        {
            public double Left { get; set; }
            public double Top { get; set; }
        }

        public static bool TryLoadWindowPosition(out double left, out double top)
        {
            left = 0;
            top = 0;
            try
            {
                if (!File.Exists(SettingsFilePath))
                    return false;

                var json = File.ReadAllText(SettingsFilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings == null)
                    return false;

                left = settings.Left;
                top = settings.Top;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void SaveWindowPosition(double left, double top)
        {
            try
            {
                if (!Directory.Exists(SettingsDirectory))
                {
                    Directory.CreateDirectory(SettingsDirectory);
                }

                var settings = new AppSettings { Left = left, Top = top };
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(SettingsFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Errore nel salvataggio impostazioni: {ex.Message}");
            }
        }
    }
}


