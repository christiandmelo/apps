using System.Windows.Forms;
using TestEvidence.Services.Nativo;

namespace TestEvidence.Services;

public readonly record struct AtalhoDefinicao(uint Modificadores, Keys Tecla);

public sealed class AtalhoGlobal : IDisposable
{
  private IntPtr _handle;
  private readonly List<int> _ids = new();

  /// <summary>Converte "Ctrl+Shift+E" em modificadores + tecla. Exige ao menos um modificador.</summary>
  public static bool TentarConverter(string? texto, out AtalhoDefinicao atalho, out string erro)
  {
    atalho = default;
    erro = "";
    if (string.IsNullOrWhiteSpace(texto)) { erro = "Informe o atalho."; return false; }

    uint mods = 0;
    Keys? tecla = null;
    foreach (var parte in texto.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
    {
      switch (parte.ToLowerInvariant())
      {
        case "ctrl": case "control": mods |= User32.MOD_CONTROL; break;
        case "shift": mods |= User32.MOD_SHIFT; break;
        case "alt": mods |= User32.MOD_ALT; break;
        case "win": mods |= User32.MOD_WIN; break;
        default:
          if (tecla != null) { erro = "Use apenas uma tecla além dos modificadores."; return false; }
          if (!Enum.TryParse<Keys>(parte, true, out var k) || k == Keys.None)
          {
            erro = $"Tecla inválida: {parte}.";
            return false;
          }
          tecla = k;
          break;
      }
    }

    if (mods == 0) { erro = "O atalho precisa de pelo menos um modificador (Ctrl, Shift, Alt ou Win)."; return false; }
    if (tecla == null) { erro = "O atalho precisa de uma tecla."; return false; }
    atalho = new AtalhoDefinicao(mods, tecla.Value);
    return true;
  }

  public void Vincular(IntPtr handle) => _handle = handle;

  /// <summary>Registra o atalho; devolve false se estiver em uso por outro app ou for inválido.</summary>
  public bool Registrar(int id, string texto)
  {
    if (_handle == IntPtr.Zero || !TentarConverter(texto, out var a, out _)) return false;
    if (!User32.RegisterHotKey(_handle, id, a.Modificadores | User32.MOD_NOREPEAT, (uint)a.Tecla)) return false;
    _ids.Add(id);
    return true;
  }

  public void DesregistrarTodos()
  {
    foreach (var id in _ids)
      User32.UnregisterHotKey(_handle, id);
    _ids.Clear();
  }

  public void Dispose() => DesregistrarTodos();
}
