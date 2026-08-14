namespace EZRecoil.Models;

/// <summary>触发方式。</summary>
public enum TriggerMode
{
  /// <summary>仅按住鼠标左键触发。</summary>
  LeftButton,

  /// <summary>同时按住鼠标左键 + 右键触发。</summary>
  LeftRightButtons,

  /// <summary>最后一次按下的主键盘数字键为 X，且同时按住左键 + 右键触发。</summary>
  LeftRightWithDigit,
}

/// <summary>全局应用设置（不随配置档案变化）。</summary>
public sealed class AppSettings
{
  /// <summary>触发方式。</summary>
  public TriggerMode Mode { get; set; } = TriggerMode.LeftButton;

  /// <summary>模式3 的目标数字键 X（0-9）。</summary>
  public int TriggerDigit { get; set; } = 1;
}
