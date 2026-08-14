using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EZRecoil.Models;

/// <summary>DataGrid 中可编辑的步数项（支持绑定通知）。</summary>
public sealed class StepItem : INotifyPropertyChanged
{
  private int _index;
  private int _ver;
  private int _hor;

  public int Index
  {
    get => _index;
    set
    {
      if (_index != value)
      {
        _index = value;
        OnPropertyChanged();
      }
    }
  }

  public int Ver
  {
    get => _ver;
    set
    {
      if (_ver != value)
      {
        _ver = value;
        OnPropertyChanged();
      }
    }
  }

  public int Hor
  {
    get => _hor;
    set
    {
      if (_hor != value)
      {
        _hor = value;
        OnPropertyChanged();
      }
    }
  }

  public event PropertyChangedEventHandler? PropertyChanged;

  private void OnPropertyChanged([CallerMemberName] string? name = null)
    => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
