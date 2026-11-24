using System.Drawing;
using System.Drawing.Imaging;
using TestEvidence.Models;
using TestEvidence.Services.Nativo;

namespace TestEvidence.Services;

/// <summary>
/// Captura de tela em pixels físicos. Quem chama deve estar com o contexto de DPI
/// PER_MONITOR_AWARE_V2 ativo na thread (ver <see cref="ContextoDpi"/>).
/// Esconder/mostrar a barra flutuante é responsabilidade de quem chama.
/// </summary>
public sealed class CapturaService
{
  public static RegiaoTela TelaVirtual => new(
    User32.GetSystemMetrics(User32.SM_XVIRTUALSCREEN),
    User32.GetSystemMetrics(User32.SM_YVIRTUALSCREEN),
    User32.GetSystemMetrics(User32.SM_CXVIRTUALSCREEN),
    User32.GetSystemMetrics(User32.SM_CYVIRTUALSCREEN));

  public Bitmap CapturarTelaVirtual() => CapturarRegiao(TelaVirtual);

  public Bitmap CapturarRegiao(RegiaoTela regiao)
  {
    if (regiao.Largura <= 0 || regiao.Altura <= 0)
      throw new ArgumentException("A região de captura é inválida.");

    var bmp = new Bitmap(regiao.Largura, regiao.Altura, PixelFormat.Format32bppArgb);
    try
    {
      using var g = Graphics.FromImage(bmp);
      g.CopyFromScreen(regiao.X, regiao.Y, 0, 0, new Size(regiao.Largura, regiao.Altura), CopyPixelOperation.SourceCopy);
      return bmp;
    }
    catch
    {
      bmp.Dispose();
      throw;
    }
  }

  public Bitmap CapturarMonitorDoCursor(out RegiaoTela regiao)
  {
    regiao = MonitorDoCursor();
    return CapturarRegiao(regiao);
  }

  public static RegiaoTela MonitorDoCursor()
  {
    User32.GetCursorPos(out var pt);
    var monitor = User32.MonitorFromPoint(pt, User32.MONITOR_DEFAULTTONEAREST);
    var info = new User32.MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<User32.MONITORINFO>() };
    if (!User32.GetMonitorInfo(monitor, ref info))
      return TelaVirtual;
    var r = info.rcMonitor;
    return new RegiaoTela(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
  }

  /// <summary>Recorta uma região (em coordenadas da tela virtual) de um bitmap que cobre a tela virtual.</summary>
  public static Bitmap Recortar(Bitmap tela, RegiaoTela telaVirtual, RegiaoTela regiao)
  {
    var origem = new Rectangle(regiao.X - telaVirtual.X, regiao.Y - telaVirtual.Y, regiao.Largura, regiao.Altura);
    origem.Intersect(new Rectangle(0, 0, tela.Width, tela.Height));
    if (origem.Width <= 0 || origem.Height <= 0)
      throw new ArgumentException("A região está fora da tela.");
    return tela.Clone(origem, PixelFormat.Format32bppArgb);
  }
}

/// <summary>Ativa DPI per-monitor v2 na thread atual durante o uso.</summary>
public sealed class ContextoDpi : IDisposable
{
  private readonly IntPtr _anterior = User32.SetThreadDpiAwarenessContext(User32.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);

  public void Dispose()
  {
    if (_anterior != IntPtr.Zero)
      User32.SetThreadDpiAwarenessContext(_anterior);
  }
}
