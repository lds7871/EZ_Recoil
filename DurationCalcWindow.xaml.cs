using System.Windows;
using System.Windows.Controls;

namespace EZRecoil;

/// <summary>
/// 总时长计算器：按 射速（每分钟）与 子弹数量 计算总时长（毫秒）。
/// 总时长 = 子弹数量 ÷ 射速 × 60000。
/// 结果只放在可选中的文本框里，由用户自己选中后 Ctrl+C 复制（不直接操作剪贴板）。
/// </summary>
public partial class DurationCalcWindow : Window
{
  private const string FormulaHint = "公式：子弹数量 ÷ 射速 × 60000";

  /// <summary>最近一次已处理的输入组合，用于忽略重复的 TextChanged 回调。</summary>
  private string? _lastInputs;

  public DurationCalcWindow(int initialRate)
  {
    InitializeComponent();

    if (initialRate > 0)
    {
      RateText.Text = initialRate.ToString();
    }

    Recalculate();

    Loaded += (_, _) =>
    {
      RateText.Focus();
      RateText.SelectAll();
    };
  }

  private void Input_TextChanged(object sender, TextChangedEventArgs e) => Recalculate();

  /// <summary>输入变化时立即重新计算（输入不合法则清空结果并给出提示）。</summary>
  private void Recalculate()
  {
    // InitializeComponent 期间控件尚未全部创建，需跳过
    if (ResultText is null || RateText is null || BulletText is null || StatusText is null)
    {
      return;
    }

    string rateInput = RateText.Text.Trim();
    string bulletInput = BulletText.Text.Trim();

    // 输入没变就不重复刷新，避免迟到的 TextChanged 回调覆盖当前提示
    string signature = rateInput + "\u0000" + bulletInput;
    if (signature == _lastInputs)
    {
      return;
    }
    _lastInputs = signature;

    // 还没填完：给灰色引导提示，不报错（打开窗口时预填了射速，子弹数量通常还是空的）
    if (rateInput.Length == 0 || bulletInput.Length == 0)
    {
      SetNoResult(rateInput.Length == 0 ? "请输入射速（每分钟）。" : "请输入子弹数量。", isError: false);
      return;
    }

    if (!int.TryParse(rateInput, out int rate) || rate <= 0)
    {
      SetNoResult("射速必须是大于 0 的整数。", isError: true);
      return;
    }

    if (!int.TryParse(bulletInput, out int bullets) || bullets <= 0)
    {
      SetNoResult("子弹数量必须是大于 0 的整数。", isError: true);
      return;
    }

    double totalMs = (double)bullets / rate * 60000.0;
    long rounded = (long)Math.Round(totalMs, MidpointRounding.AwayFromZero);

    ResultText.Text = rounded.ToString();

    // 明确告诉用户结果可选中复制
    string hint = FormulaHint + "　（选中结果框按 Ctrl+C 可复制）";
    if (rounded >= 1000)
    {
      hint += $"\n≈ {rounded / 1000.0:0.###} 秒";
    }
    if (rounded != totalMs)
    {
      hint += $"\n精确值 {totalMs:0.###} 毫秒（显示值已取整）";
    }
    SetStatus(hint, 0x77, 0x77, 0x77);
  }

  private void SetNoResult(string message, bool isError)
  {
    ResultText.Text = string.Empty;
    SetStatus(message, isError ? (byte)0xC6 : (byte)0x77, isError ? (byte)0x28 : (byte)0x77,
      isError ? (byte)0x28 : (byte)0x77);
  }

  private void SetStatus(string message, byte r, byte g, byte b)
  {
    StatusText.Text = message;
    StatusText.Foreground = new System.Windows.Media.SolidColorBrush(
      System.Windows.Media.Color.FromRgb(r, g, b));
  }

  private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

  /// <summary>Esc 关闭窗口（不使用 IsCancel，避免与 Close 重复结束对话框）。</summary>
  protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
  {
    if (e.Key == System.Windows.Input.Key.Escape)
    {
      e.Handled = true;
      Close();
      return;
    }
    base.OnKeyDown(e);
  }
}
