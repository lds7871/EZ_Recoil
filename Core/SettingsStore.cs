using System.IO;
using System.Text.Json;
using EZRecoil.Models;

namespace EZRecoil.Core;

/// <summary>
/// 全局应用设置读写。存于 JsonRAW/.settings（不带 .json 后缀，
/// 避免被当作配置档案列表显示）。
/// </summary>
public static class SettingsStore
{
  public static string SettingsPath => Path.Combine(ProfileStore.JsonRawDir, ".settings");

  public static AppSettings Load()
  {
    try
    {
      if (File.Exists(SettingsPath))
      {
        string json = File.ReadAllText(SettingsPath);
        AppSettings? settings = JsonSerializer.Deserialize<AppSettings>(json);
        if (settings is not null)
        {
          return settings;
        }
      }
    }
    catch
    {
      // 读取失败则使用默认设置
    }
    return new AppSettings();
  }

  public static void Save(AppSettings settings)
  {
    string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
    File.WriteAllText(SettingsPath, json);
  }
}
