using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using NoRecoil.Core;
using NoRecoil.Models;

namespace NoRecoil;

public partial class MainWindow : Window
{
  private readonly MacroEngine _engine = new();
  private readonly ObservableCollection<StepItem> _steps = new();
  private readonly Dictionary<string, Profile> _loaded = new(StringComparer.OrdinalIgnoreCase);

  private string? _editingPath; // 编辑器当前编辑的配置文件（保存目标）
  private string? _currentPath; // 当前正在使用的配置文件（引擎）
  private bool _quickSwitchArmed; // O 键已按下，等待数字键

  public MainWindow()
  {
    InitializeComponent();
    StepsGrid.ItemsSource = _steps;

    _engine.MasterChanged += OnMasterChanged;
    _engine.ActiveChanged += OnActiveChanged;
    Loaded += (_, _) => _engine.Start();
    Closed += (_, _) => _engine.Stop();

    RefreshProfiles();
    UpdateMasterStatus(_engine.MasterOn);
  }

  // ---------- 状态回调（来自后台线程，需切回 UI 线程） ----------

  private void OnMasterChanged(bool on) => Dispatcher.Invoke(() => UpdateMasterStatus(on));

  private void OnActiveChanged(bool active) => Dispatcher.Invoke(() =>
  {
    ActiveStatusText.Text = active ? "   |   激活中..." : "   |   未激活";
    ActiveStatusText.Foreground = active
      ? new SolidColorBrush(Color.FromRgb(0x4F, 0xDD, 0x6C))
      : new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99));
  });

  private void UpdateMasterStatus(bool on)
  {
    MasterStatusText.Text = on ? "ON" : "OFF";
    MasterStatusText.Foreground = new SolidColorBrush(on
      ? Color.FromRgb(0x4F, 0xDD, 0x6C)
      : Color.FromRgb(0xFF, 0x52, 0x52));
    if (!on)
    {
      ActiveStatusText.Text = "   |   未激活";
      ActiveStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99));
    }
  }

  // ---------- 配置文件列表 ----------

  private void RefreshProfiles()
  {
    _loaded.Clear();
    foreach (string file in ProfileStore.ListProfiles())
    {
      try
      {
        if (ProfileStore.Load(file) is { } profile)
        {
          _loaded[Path.GetFileName(file)] = profile;
        }
      }
      catch
      {
        // 忽略无法解析的文件
      }
    }

    if (_loaded.Count == 0)
    {
      ProfileStore.Create(ProfileStore.JsonRawDir, "config");
      foreach (string file in ProfileStore.ListProfiles())
      {
        try
        {
          if (ProfileStore.Load(file) is { } profile)
          {
            _loaded[Path.GetFileName(file)] = profile;
          }
        }
        catch
        {
          // ignore
        }
      }
    }

    ProfileList.ItemsSource = _loaded.Keys.ToList();

    string? activeName = ProfileStore.GetActiveName();
    if (activeName is not null && _loaded.ContainsKey(activeName))
    {
      ProfileList.SelectedItem = activeName;
    }
    else if (_loaded.Count > 0)
    {
      ProfileList.SelectedIndex = 0;
    }
  }

  private void ProfileList_SelectionChanged(object sender, SelectionChangedEventArgs e)
  {
    if (ProfileList.SelectedItem is not string name || !_loaded.TryGetValue(name, out Profile? profile))
    {
      return;
    }

    // 快速切换：立即作为当前使用配置，并应用到引擎
    _currentPath = profile.FilePath;
    _editingPath = profile.FilePath;
    ProfileStore.SetActiveName(name);
    _engine.SetProfile(CloneForEngine(profile));
    LoadEditor(profile);
  }

  // ---------- 编辑器 ----------

  private void LoadEditor(Profile profile)
  {
    Time4SText.Text = profile.Time4S.ToString();
    SustainText.Text = profile.Sustain.ToString();
    SmoothText.Text = profile.Smooth.ToString();
    RadText.Text = profile.Rad.ToString();

    _steps.Clear();
    foreach (MoveStep step in profile.Steps)
    {
      _steps.Add(new StepItem { Ver = step.Ver, Hor = step.Hor });
    }
  }

  private void BtnAddStep_Click(object sender, RoutedEventArgs e) => _steps.Add(new StepItem());

  private void BtnRemoveStep_Click(object sender, RoutedEventArgs e)
  {
    if (StepsGrid.SelectedItem is StepItem item)
    {
      _steps.Remove(item);
    }
  }

  private void BtnSave_Click(object sender, RoutedEventArgs e)
  {
    if (_editingPath is null)
    {
      MessageBox.Show("请先在列表中选择一个配置文件。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
      return;
    }

    if (!int.TryParse(Time4SText.Text, out int time4S) || time4S <= 0)
    {
      MessageBox.Show("time4S 必须是大于 0 的整数。", "输入错误");
      return;
    }
    if (!int.TryParse(SustainText.Text, out int sustain) || sustain <= 0)
    {
      MessageBox.Show("sustain 必须是大于 0 的整数。", "输入错误");
      return;
    }
    if (!int.TryParse(SmoothText.Text, out int smooth) || smooth < 1)
    {
      MessageBox.Show("smooth 必须是不小于 1 的整数。", "输入错误");
      return;
    }
    if (!int.TryParse(RadText.Text, out int rad) || rad < 0)
    {
      MessageBox.Show("rad 必须是不小于 0 的整数。", "输入错误");
      return;
    }

    var profile = new Profile
    {
      FilePath = _editingPath,
      Time4S = time4S,
      Sustain = sustain,
      Smooth = smooth,
      Rad = rad,
      Steps = _steps.Select(s => new MoveStep { Ver = s.Ver, Hor = s.Hor }).ToList(),
    };

    try
    {
      ProfileStore.Save(profile);
    }
    catch (Exception ex)
    {
      MessageBox.Show("保存失败：" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
      return;
    }

    _loaded[Path.GetFileName(_editingPath)] = profile;
    _engine.SetProfile(CloneForEngine(profile)); // 保存后立即应用到引擎

    MessageBox.Show("已保存并应用。", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
  }

  // ---------- 新增 / 删除 / 刷新 ----------

  private void BtnNew_Click(object sender, RoutedEventArgs e)
  {
    string? name = PromptName("新增配置文件", "new-config");
    if (string.IsNullOrWhiteSpace(name))
    {
      return;
    }

    name = SanitizeName(name);
    string path = Path.Combine(ProfileStore.JsonRawDir, name + ".json");
    if (File.Exists(path))
    {
      MessageBox.Show($"文件 {name}.json 已存在，请换一个名称。", "提示");
      return;
    }

    Profile created = ProfileStore.Create(ProfileStore.JsonRawDir, name);
    RefreshProfiles();
    ProfileList.SelectedItem = Path.GetFileName(created.FilePath); // 立即选中并进入编辑
  }

  private void BtnDelete_Click(object sender, RoutedEventArgs e)
  {
    if (_editingPath is null)
    {
      MessageBox.Show("请先在列表中选择要删除的配置文件。", "提示");
      return;
    }

    string name = Path.GetFileName(_editingPath);
    if (MessageBox.Show($"确定删除配置文件 “{name}” 吗？", "删除确认",
          MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
    {
      return;
    }

    try
    {
      File.Delete(_editingPath);
    }
    catch (Exception ex)
    {
      MessageBox.Show("删除失败：" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
      return;
    }

    if (_currentPath == _editingPath)
    {
      _currentPath = null;
    }
    RefreshProfiles();
  }

  private void BtnRefresh_Click(object sender, RoutedEventArgs e) => RefreshProfiles();

  // ---------- 快捷键：O + 数字键 快速切换配置 ----------

  protected override void OnKeyDown(KeyEventArgs e)
  {
    if (e.Key == Key.O)
    {
      // 按下 O：进入等待数字键状态
      _quickSwitchArmed = true;
      QuickHintText.Visibility = Visibility.Visible;
      e.Handled = true;
    }
    else if (_quickSwitchArmed && e.Key >= Key.D1 && e.Key <= Key.D9)
    {
      // O + 数字：快速切换配置
      int index = e.Key - Key.D1;
      _quickSwitchArmed = false;
      QuickHintText.Visibility = Visibility.Collapsed;
      if (index >= 0 && index < ProfileList.Items.Count)
      {
        ProfileList.SelectedIndex = index;
        e.Handled = true;
      }
    }
    else
    {
      // 按了其它键：取消等待状态
      _quickSwitchArmed = false;
      QuickHintText.Visibility = Visibility.Collapsed;
    }
    base.OnKeyDown(e);
  }

  // ---------- 工具方法 ----------

  private static Profile CloneForEngine(Profile source) => new()
  {
    Time4S = source.Time4S,
    Sustain = source.Sustain,
    Smooth = source.Smooth,
    Rad = source.Rad,
    Steps = source.Steps.Select(s => new MoveStep { Ver = s.Ver, Hor = s.Hor }).ToList(),
  };

  private static string SanitizeName(string name)
  {
    foreach (char c in Path.GetInvalidFileNameChars())
    {
      name = name.Replace(c, '-');
    }
    return name.Trim();
  }

  private string? PromptName(string title, string defaultName)
  {
    var win = new Window
    {
      Title = title,
      Width = 340,
      Height = 150,
      WindowStartupLocation = WindowStartupLocation.CenterOwner,
      Owner = this,
      ResizeMode = ResizeMode.NoResize,
      ShowInTaskbar = false,
    };

    var panel = new StackPanel { Margin = new Thickness(14) };
    panel.Children.Add(new TextBlock { Text = "配置文件名称：", Margin = new Thickness(0, 0, 0, 8) });
    var input = new TextBox { Text = defaultName };
    panel.Children.Add(input);

    var cancel = new Button { Content = "取消", Width = 76, IsCancel = true, Margin = new Thickness(0, 14, 0, 0) };
    var ok = new Button { Content = "确定", Width = 76, IsDefault = true, Margin = new Thickness(8, 14, 0, 0) };
    var bar = new StackPanel
    {
      Orientation = Orientation.Horizontal,
      HorizontalAlignment = HorizontalAlignment.Right,
    };
    bar.Children.Add(cancel);
    bar.Children.Add(ok);
    panel.Children.Add(bar);

    win.Content = panel;

    string? result = null;
    ok.Click += (_, _) =>
    {
      result = input.Text;
      win.DialogResult = true;
    };
    cancel.Click += (_, _) => win.DialogResult = false;

    win.ShowDialog();
    return result;
  }
}
