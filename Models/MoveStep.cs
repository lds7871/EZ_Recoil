namespace NoRecoil.Models;

/// <summary>每一步触发的鼠标移动参数。</summary>
public sealed class MoveStep
{
  /// <summary>纵向移动像素（正=下，负=上）。</summary>
  public int Ver { get; set; }

  /// <summary>横向移动像素（正=右，负=左）。</summary>
  public int Hor { get; set; }
}
