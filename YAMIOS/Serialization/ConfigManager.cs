using System;
using System.IO;
using System.Text.Json;

namespace YAMIOS.Serialization
{
    public class EditorConfig
    {
        public string? SS14ResourcesPath { get; set; }
        public string? SS14RepoRoot { get; set; }
    }

    public static class ConfigManager
    {
        private static readonly string ConfigFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "EditorConfig.json");

        public static EditorConfig Load()
        {
            if (!File.Exists(ConfigFilePath))
                return new EditorConfig();
            var json = File.ReadAllText(ConfigFilePath);
            return JsonSerializer.Deserialize<EditorConfig>(json) ?? new EditorConfig();
        }

        public static void Save(EditorConfig config)
        {
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigFilePath, json);
        }
    }
}
