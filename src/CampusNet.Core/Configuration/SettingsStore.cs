using System.Text.Json;
using CampusNet.Core.Models;

namespace CampusNet.Core.Configuration;

/// <summary>读写应用设置（明文 JSON，不含敏感信息）。</summary>
public static class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(AppPaths.SettingsFile))
            {
                string text = File.ReadAllText(AppPaths.SettingsFile);
                var s = JsonSerializer.Deserialize<AppSettings>(text, JsonOpts);
                if (s != null) return s;
            }
        }
        catch { }
        return new AppSettings();
    }

    public static void Save(AppSettings settings)
    {
        AppPaths.EnsureDirs();
        File.WriteAllText(AppPaths.SettingsFile, JsonSerializer.Serialize(settings, JsonOpts));
    }
}
