using System.IO;

namespace NoRecoil.Models;

/// <summary>一个配置文件（对应 JsonRAW 下的一个 .json）。</summary>
public sealed class Profile
{
  /// <summary>每分钟触发次数。</summary>
  public int Time4S { get; set; } = 600;

  /// <summary>每次激活最多持续毫秒数。</summary>
  public int Sustain { get; set; } = 2000;

  /// <summary>每次触发细分的平滑段数（>=1，越大越平滑）。</summary>
  public int Smooth { get; set; } = 1;

  /// <summary>每次触发随机往某一方向添加的 0~Rad 像素偏移（0 表示关闭）。</summary>
  public int Rad { get; set; } = 0;

  /// <summary>移动步数序列。</summary>
  public List<MoveStep> Steps { get; set; } = new();

  /// <summary>配置文件完整路径（不序列化到 JSON）。</summary>
  public string FilePath { get; set; } = string.Empty;

  /// <summary>配置文件显示名（不含扩展名）。</summary>
  public string Name => Path.GetFileNameWithoutExtension(FilePath);
}
