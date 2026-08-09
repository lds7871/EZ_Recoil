using System.Diagnostics;
using System.Runtime.InteropServices;
using NoRecoil.Models;

namespace NoRecoil.Core;

/// <summary>
/// 后台鼠标宏引擎：
///   - L 键按下瞬间切换总开关（ON/OFF）；
///   - 总开关为 ON 且按住鼠标左键时激活宏，松开左键立即取消；
///   - 每次激活最多持续 sustain 毫秒，结束后必须松开左键才能再次激活；
///   - 每次触发按 smooth 段平滑移动，步数用完后保持最后一步；
///   - sustain 为毫秒；Rad > 0 时每次触发随机往某一方向添加 0~Rad 偏移。
/// </summary>
public sealed class MacroEngine
{
  private const uint MOUSEEVENTF_MOVE = 0x0001;
  private const int VK_L = 0x4C;          // 键盘 L 键（总开关）
  private const int VK_LBUTTON = 0x01;    // 鼠标左键（激活）

  [DllImport("user32.dll")]
  private static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, UIntPtr dwExtraInfo);

  [DllImport("user32.dll")]
  private static extern short GetAsyncKeyState(int vKey);

  private static bool IsKeyDown(int vKey) => (GetAsyncKeyState(vKey) & 0x8000) != 0;

  private static readonly Random Rng = new();

  private Thread? _thread;
  private volatile bool _running;
  private volatile bool _masterOn;
  private volatile Profile? _profile;

  /// <summary>总开关状态变化事件（后台线程触发）。</summary>
  public event Action<bool>? MasterChanged;

  /// <summary>宏是否正在执行事件（后台线程触发）。</summary>
  public event Action<bool>? ActiveChanged;

  /// <summary>当前总开关状态。</summary>
  public bool MasterOn => _masterOn;

  /// <summary>设置当前使用的配置文件（快照，运行中替换安全）。</summary>
  public void SetProfile(Profile profile) => _profile = profile;

  public void Start()
  {
    if (_running)
    {
      return;
    }
    _running = true;
    _thread = new Thread(Loop) { IsBackground = true, Name = "MacroEngine" };
    _thread.Start();
  }

  public void Stop() => _running = false;

  private void Loop()
  {
    bool lastL = false;
    while (_running)
    {
      // L 键按下瞬间切换总开关
      lastL = PollMasterToggle(lastL);

      // 总开关开启且按住鼠标左键 -> 执行一次宏
      if (_masterOn && IsKeyDown(VK_LBUTTON))
      {
        RunPattern();

        // 一次激活结束（sustain 超时或松开左键）后，必须等左键完全松开，
        // 否则按住不放会让 Loop 立刻重新激活，导致 sustain 形同虚设、一直执行。
        while (_running && IsKeyDown(VK_LBUTTON))
        {
          lastL = PollMasterToggle(lastL);
          Thread.Sleep(5);
        }
      }

      Thread.Sleep(5);
    }
  }

  /// <summary>检测 L 键按下瞬间并切换总开关；返回本次 L 键是否按下，供边沿检测使用。</summary>
  private bool PollMasterToggle(bool lastL)
  {
    bool lDown = IsKeyDown(VK_L);
    if (lDown && !lastL)
    {
      _masterOn = !_masterOn;
      MasterChanged?.Invoke(_masterOn);
    }
    return lDown;
  }

  private void RunPattern()
  {
    Profile? profile = _profile;
    if (profile is null || profile.Time4S <= 0 || profile.Steps.Count == 0)
    {
      return;
    }

    int smooth = profile.Smooth < 1 ? 1 : profile.Smooth;
    TimeSpan triggerInterval = TimeSpan.FromSeconds(1.0 / profile.Time4S);
    TimeSpan maxDuration = TimeSpan.FromMilliseconds(profile.Sustain);

    ActiveChanged?.Invoke(true);
    Stopwatch sw = Stopwatch.StartNew();
    int stepIndex = 0;

    try
    {
      while (_running && _masterOn && IsKeyDown(VK_LBUTTON) && sw.Elapsed < maxDuration)
      {
        // 步数用完后保持最后一步
        MoveStep step = profile.Steps[Math.Min(stepIndex, profile.Steps.Count - 1)];

        // Rad：每次触发随机往某一方向（上/下/左/右之一）添加 0~Rad 的随机偏移
        int ver = step.Ver;
        int hor = step.Hor;
        if (profile.Rad > 0)
        {
          int offset = Rng.Next(0, profile.Rad + 1);
          switch (Rng.Next(4))
          {
            case 0: ver -= offset; break; // 上
            case 1: ver += offset; break; // 下
            case 2: hor -= offset; break; // 左
            default: hor += offset; break; // 右
          }
        }

        // 把 (Hor, Ver) 细分为 smooth 段，平滑移动避免瞬移
        int segHor = hor / smooth;
        int segVer = ver / smooth;
        int remHor = hor % smooth;
        int remVer = ver % smooth;

        TimeSpan stepStart = triggerInterval * stepIndex - triggerInterval;
        for (int seg = 0; seg < smooth; seg++)
        {
          // 余数分摊到前几段，保证整段位移恰好等于 step
          int dx = segHor + (seg < Math.Abs(remHor) ? Math.Sign(remHor) : 0);
          int dy = segVer + (seg < Math.Abs(remVer) ? Math.Sign(remVer) : 0);

          mouse_event(MOUSEEVENTF_MOVE, dx, dy, 0, UIntPtr.Zero);

          // 精确按 time4S × smooth 的频率等待下一小段
          TimeSpan nextAt = stepStart + triggerInterval / smooth * (seg + 1);
          while (_running && _masterOn && IsKeyDown(VK_LBUTTON)
                 && sw.Elapsed < nextAt && sw.Elapsed < maxDuration)
          {
            Thread.Sleep(1);
          }

          // 若已被取消/超时/总开关关闭，立即结束本次触发
          if (!(_running && _masterOn && IsKeyDown(VK_LBUTTON) && sw.Elapsed < maxDuration))
          {
            return;
          }
        }

        stepIndex++;
      }
    }
    finally
    {
      sw.Stop();
      ActiveChanged?.Invoke(false);
    }
  }
}
