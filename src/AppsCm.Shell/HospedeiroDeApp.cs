namespace AppsCm.Shell;

/// <summary>Reparenteia um Form de módulo (TopLevel=false) dentro de um container do shell.
/// Ver ordem das atribuições abaixo — WindowState/MinimumSize precisam ser normalizados
/// ANTES de TopLevel=false, ou os bounds ficam inconsistentes.</summary>
internal static class HospedeiroDeApp
{
  public static void Hospedar(Control view, Control container)
  {
    if (view is Form janela)
    {
      janela.WindowState = FormWindowState.Normal;
      janela.MinimumSize = Size.Empty;
      janela.MaximumSize = Size.Empty;
      janela.TopLevel = false;
      janela.FormBorderStyle = FormBorderStyle.None;
      janela.ControlBox = false;
      janela.ShowInTaskbar = false;
    }

    view.Dock = DockStyle.Fill;
    container.Controls.Add(view);
    view.Show();
  }

  /// <summary>Fecha a view hospedada. Retorna false se um FormClosing do módulo cancelou
  /// o fechamento (ex.: execução em andamento e o usuário optou por não interromper).</summary>
  public static bool Encerrar(Control view)
  {
    if (view is Form janela)
    {
      janela.Close();
      return janela.IsDisposed;
    }

    view.Dispose();
    return true;
  }
}
