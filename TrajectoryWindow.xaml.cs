using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using EZRecoil.Models;

namespace EZRecoil;

/// <summary>
/// 轨迹图窗口：按当前参数绘制鼠标移动轨迹。
///   - 正向（红）：按步数顺序累积的移动轨迹，与宏实际执行的路径一致；
///   - 反向（绿）：正向轨迹关于起点的镜像（每一步取反方向）；
///   - 每个参数点（即轨迹的转折点）用一个小圆点标注，起点用空心圆标注。
/// 横轴为 Hor（正右负左），纵轴为 Ver（正下负上），与鼠标实际移动方向一致。
/// </summary>
public partial class TrajectoryWindow : Window
{
  private const double DotRadius = 3.5;
  private const double OriginRadius = 5.5;
  private const double Pad = 26;          // 画布内边距（给轨迹留白）
  private const double MinGridPixels = 46; // 每格最小像素宽度

  private static readonly Brush ForwardBrush = Frozen(0xE5, 0x39, 0x35);
  private static readonly Brush ReverseBrush = Frozen(0x2E, 0x7D, 0x32);
  private static readonly Brush GridBrush = Frozen(0xEE, 0xEE, 0xEE);
  private static readonly Brush AxisBrush = Frozen(0xB0, 0xB0, 0xB0);
  private static readonly Brush OriginBrush = Frozen(0x42, 0x42, 0x42);

  private readonly List<MoveStep> _steps;
  private readonly int _time4S;
  private readonly int _sustain;
  private readonly int _smooth;
  private readonly int _rad;

  /// <summary>正向轨迹的世界坐标点（第一个点为起点）。</summary>
  private readonly List<Point> _forward = new();

  /// <summary>反向轨迹的世界坐标点（第一个点为起点）。</summary>
  private readonly List<Point> _reverse = new();

  private double _gridStep = 1;

  public TrajectoryWindow(List<MoveStep> steps, int time4S, int sustain, int smooth, int rad)
  {
    InitializeComponent();
    _steps = steps;
    _time4S = time4S;
    _sustain = sustain;
    _smooth = smooth;
    _rad = rad;

    BuildPoints();
    UpdateSummary();
    Loaded += (_, _) => Render();
  }

  private static Brush Frozen(byte r, byte g, byte b)
  {
    var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
    brush.Freeze();
    return brush;
  }

  /// <summary>把步数累积为正向轨迹点，并镜像出反向轨迹点。</summary>
  private void BuildPoints()
  {
    _forward.Clear();
    _reverse.Clear();
    _forward.Add(new Point(0, 0));
    _reverse.Add(new Point(0, 0));

    double hor = 0;
    double ver = 0;
    foreach (MoveStep step in _steps)
    {
      hor += step.Hor;
      ver += step.Ver;
      _forward.Add(new Point(hor, ver));
      _reverse.Add(new Point(-hor, -ver));
    }
  }

  private void UpdateSummary()
  {
    SummaryText.Text =
      $"步数 {_steps.Count} ｜ time4S {_time4S} 次/分钟 ｜ sustain {_sustain} 毫秒 ｜ smooth {_smooth} 段/次 ｜ rad {_rad}";

    if (_steps.Count > 0)
    {
      Point forwardEnd = _forward[^1];
      Point reverseEnd = _reverse[^1];
      EndpointText.Text =
        $"正向终点 Hor {forwardEnd.X:+0;-0;0} / Ver {forwardEnd.Y:+0;-0;0}　｜　" +
        $"反向终点 Hor {reverseEnd.X:+0;-0;0} / Ver {reverseEnd.Y:+0;-0;0}";
    }
    else
    {
      EndpointText.Text = string.Empty;
    }

    if (_rad > 0)
    {
      RadHintText.Text =
        $"rad = {_rad}：实际运行时每次触发会随机向 上/下/左/右 之一额外偏移 0~{_rad} 像素，图中绘制的是不含随机偏移的基准轨迹。";
      RadHintText.Visibility = Visibility.Visible;
    }
    else
    {
      RadHintText.Visibility = Visibility.Collapsed;
    }
  }

  private void ChartCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => Render();

  private void Render()
  {
    ChartCanvas.Children.Clear();

    double width = ChartCanvas.ActualWidth;
    double height = ChartCanvas.ActualHeight;
    if (width < 40 || height < 40)
    {
      return;
    }

    if (_steps.Count == 0)
    {
      ScaleText.Text = string.Empty;
      var hint = new TextBlock
      {
        Text = "当前没有步数，请先在主窗口添加步数。",
        Foreground = AxisBrush,
        FontSize = 14,
      };
      hint.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
      Canvas.SetLeft(hint, (width - hint.DesiredSize.Width) / 2);
      Canvas.SetTop(hint, (height - hint.DesiredSize.Height) / 2);
      ChartCanvas.Children.Add(hint);
      return;
    }

    // 世界坐标范围（正向 + 反向 + 起点）
    double minX = 0, maxX = 0, minY = 0, maxY = 0;
    foreach (Point p in _forward.Concat(_reverse))
    {
      minX = Math.Min(minX, p.X);
      maxX = Math.Max(maxX, p.X);
      minY = Math.Min(minY, p.Y);
      maxY = Math.Max(maxY, p.Y);
    }

    // 等比缩放，使轨迹完整落在画布内
    double availW = Math.Max(1, width - Pad * 2);
    double availH = Math.Max(1, height - Pad * 2);
    double rangeX = maxX - minX;
    double rangeY = maxY - minY;
    double scaleX = rangeX > 0 ? availW / rangeX : double.MaxValue;
    double scaleY = rangeY > 0 ? availH / rangeY : double.MaxValue;
    double scale = Math.Min(scaleX, scaleY);
    if (double.IsInfinity(scale))
    {
      scale = 1; // 所有点重合在起点
    }

    // 世界坐标中心映射到画布中心（正向/反向互为镜像，故中心即起点）
    double worldCx = (minX + maxX) / 2;
    double worldCy = (minY + maxY) / 2;
    double canvasCx = width / 2;
    double canvasCy = height / 2;

    Point ToScreen(Point world) => new(
      canvasCx + (world.X - worldCx) * scale,
      canvasCy + (world.Y - worldCy) * scale);

    _gridStep = ChooseGridStep(scale);
    DrawGrid(width, height, scale, worldCx, worldCy, canvasCx, canvasCy);

    AddPolyline(_reverse, ReverseBrush, ToScreen);
    AddPolyline(_forward, ForwardBrush, ToScreen);

    foreach (Point p in _reverse)
    {
      AddDot(ToScreen(p), ReverseBrush, Brushes.White, DotRadius, 1);
    }
    foreach (Point p in _forward)
    {
      AddDot(ToScreen(p), ForwardBrush, Brushes.White, DotRadius, 1);
    }
    AddDot(ToScreen(new Point(0, 0)), Brushes.White, OriginBrush, OriginRadius, 2); // 起点画在最上层

    ScaleText.Text = $"网格 1 格 = {_gridStep:0.##} 像素移动　｜　共 {_steps.Count} 个参数点（不含起点）";
  }

  /// <summary>选择让每格不小于 MinGridPixels 的“整齐”网格步长。</summary>
  private static double ChooseGridStep(double scale)
  {
    double[] candidates = { 1, 2, 5, 10, 20, 25, 50, 100, 200, 250, 500, 1000, 2000, 2500, 5000, 10000 };
    foreach (double candidate in candidates)
    {
      if (candidate * scale >= MinGridPixels)
      {
        return candidate;
      }
    }
    return candidates[^1];
  }

  private void DrawGrid(double width, double height, double scale,
                        double worldCx, double worldCy, double canvasCx, double canvasCy)
  {
    double halfW = width / 2 / scale;
    double halfH = height / 2 / scale;

    int firstX = (int)Math.Floor((worldCx - halfW) / _gridStep);
    int lastX = (int)Math.Ceiling((worldCx + halfW) / _gridStep);
    for (int i = firstX; i <= lastX; i++)
    {
      double x = canvasCx + (i * _gridStep - worldCx) * scale;
      ChartCanvas.Children.Add(new Line
      {
        X1 = x,
        Y1 = 0,
        X2 = x,
        Y2 = height,
        Stroke = i == 0 ? AxisBrush : GridBrush,
        StrokeThickness = 1,
      });
    }

    int firstY = (int)Math.Floor((worldCy - halfH) / _gridStep);
    int lastY = (int)Math.Ceiling((worldCy + halfH) / _gridStep);
    for (int i = firstY; i <= lastY; i++)
    {
      double y = canvasCy + (i * _gridStep - worldCy) * scale;
      ChartCanvas.Children.Add(new Line
      {
        X1 = 0,
        Y1 = y,
        X2 = width,
        Y2 = y,
        Stroke = i == 0 ? AxisBrush : GridBrush,
        StrokeThickness = 1,
      });
    }
  }

  private void AddPolyline(List<Point> worldPoints, Brush brush, Func<Point, Point> toScreen)
  {
    var polyline = new Polyline
    {
      Stroke = brush,
      StrokeThickness = 2,
      StrokeLineJoin = PenLineJoin.Round,
      StrokeStartLineCap = PenLineCap.Round,
      StrokeEndLineCap = PenLineCap.Round,
    };
    foreach (Point p in worldPoints)
    {
      polyline.Points.Add(toScreen(p));
    }
    ChartCanvas.Children.Add(polyline);
  }

  /// <summary>在参数点（转折点）上标注一个小圆点。</summary>
  private void AddDot(Point screen, Brush fill, Brush stroke, double radius, double strokeThickness)
  {
    var dot = new Ellipse
    {
      Width = radius * 2,
      Height = radius * 2,
      Fill = fill,
      Stroke = stroke,
      StrokeThickness = strokeThickness,
    };
    Canvas.SetLeft(dot, screen.X - radius);
    Canvas.SetTop(dot, screen.Y - radius);
    ChartCanvas.Children.Add(dot);
  }
}
