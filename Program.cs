using System.Windows;

namespace NoRecoil;

/// <summary>
/// WPF GUI 入口：创建 Application 并显示主窗口。
/// </summary>
internal static class Program
{
  [STAThread]
  private static void Main()
  {
    Application app = new();
    app.Run(new MainWindow());
  }
}
