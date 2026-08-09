using System.IO;
using System.Text.Json;
using NoRecoil.Models;

namespace NoRecoil.Core;

/// <summary>
/// JsonRAW 文件夹下配置文件（.json）的读写、新增、快速切换管理。
/// JSON 格式为数组：第一个元素为运行变量 { time4S, sustain(毫秒), smooth, rad }，
/// 后续元素为移动步数 { Ver, Hor }。
/// </summary>
public static class ProfileStore
{
  /// <summary>JsonRAW 文件夹路径（优先工作目录，其次程序目录）。</summary>
  public static string JsonRawDir { get; } = FindJsonRawDir();

  /// <summary>记录当前使用配置的标记文件路径。</summary>
  public static string ActiveMarkerPath => Path.Combine(JsonRawDir, ".active");

  private static string FindJsonRawDir()
  {
    string[] candidates =
    {
      Path.Combine(Environment.CurrentDirectory, "JsonRAW"),
      Path.Combine(AppContext.BaseDirectory, "JsonRAW"),
    };

    foreach (string dir in candidates)
    {
      if (Directory.Exists(dir))
      {
        return dir;
      }
    }

    Directory.CreateDirectory(candidates[0]);
    return candidates[0];
  }

  /// <summary>列出 JsonRAW 下的所有 .json 配置文件（按名称排序）。</summary>
  public static IEnumerable<string> ListProfiles()
    => Directory.EnumerateFiles(JsonRawDir, "*.json")
      .OrderBy(f => f, StringComparer.OrdinalIgnoreCase);

  /// <summary>读取当前使用配置的文件名（无则返回 null）。</summary>
  public static string? GetActiveName()
  {
    if (File.Exists(ActiveMarkerPath))
    {
      string? name = File.ReadAllText(ActiveMarkerPath).Trim();
      return string.IsNullOrEmpty(name) ? null : name;
    }
    return null;
  }

  /// <summary>记录当前使用配置的文件名。</summary>
  public static void SetActiveName(string fileName)
    => File.WriteAllText(ActiveMarkerPath, fileName);

  /// <summary>从文件加载配置文件，无法解析时返回 null。</summary>
  public static Profile? Load(string path)
  {
    string json = File.ReadAllText(path);
    using JsonDocument doc = JsonDocument.Parse(json);
    JsonElement root = doc.RootElement;
    if (root.ValueKind != JsonValueKind.Array)
    {
      return null;
    }

    var profile = new Profile { FilePath = path };
    foreach (JsonElement item in root.EnumerateArray())
    {
      if (item.ValueKind != JsonValueKind.Object)
      {
        continue;
      }

      // 运行变量元素
      if (item.TryGetProperty("time4S", out _) || item.TryGetProperty("sustain", out _))
      {
        profile.Time4S = item.TryGetProperty("time4S", out JsonElement t) ? t.GetInt32() : profile.Time4S;
        profile.Sustain = item.TryGetProperty("sustain", out JsonElement s) ? s.GetInt32() : profile.Sustain;
        profile.Smooth = item.TryGetProperty("smooth", out JsonElement m) ? m.GetInt32() : 1;
        profile.Rad = item.TryGetProperty("rad", out JsonElement r) ? r.GetInt32() : 0;
        continue;
      }

      // 移动步数元素
      if (item.TryGetProperty("Ver", out JsonElement v) && item.TryGetProperty("Hor", out JsonElement h))
      {
        profile.Steps.Add(new MoveStep { Ver = v.GetInt32(), Hor = h.GetInt32() });
      }
    }

    if (profile.Smooth < 1)
    {
      profile.Smooth = 1;
    }
    return profile;
  }

  /// <summary>把配置文件写回 JSON 数组文件。</summary>
  public static void Save(Profile profile)
  {
    var items = new List<object>
    {
      new { time4S = profile.Time4S, sustain = profile.Sustain, smooth = profile.Smooth, rad = profile.Rad },
    };
    foreach (MoveStep step in profile.Steps)
    {
      items.Add(new { Ver = step.Ver, Hor = step.Hor });
    }

    string json = JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true });
    File.WriteAllText(profile.FilePath, json);
  }

  /// <summary>在 JsonRAW 下新建一个带默认步数的配置文件并立即写入文件。</summary>
  public static Profile Create(string folder, string name)
  {
    string path = Path.Combine(folder,
      name.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? name : name + ".json");

    var profile = new Profile
    {
      FilePath = path,
      Steps = new List<MoveStep> { new() { Ver = 5, Hor = 0 } },
    };
    Save(profile);
    return profile;
  }
}
