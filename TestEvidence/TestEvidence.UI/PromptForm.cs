using AppsCm.Visual;

namespace TestEvidence.UI;

/// <summary>Diálogo simples de uma linha de texto.</summary>
public sealed class PromptForm : Form
{
  private readonly TextBox _txt = new() { Dock = DockStyle.Top };

  public string Valor => _txt.Text.Trim();

  public PromptForm(string titulo, string mensagem, string valorInicial = "")
  {
    Text = titulo;
    FormBorderStyle = FormBorderStyle.FixedDialog;
    StartPosition = FormStartPosition.CenterScreen;
    MaximizeBox = MinimizeBox = false;
    ShowInTaskbar = false;
    TopMost = true;
    ClientSize = new Size(380, 150);

    var lbl = Tema.Rotulo(mensagem);
    lbl.Dock = DockStyle.Top;
    _txt.Text = valorInicial;
    Tema.EstilizarCampo(_txt);
    var ok = Tema.BotaoPrimario("OK");
    ok.DialogResult = DialogResult.OK;
    var cancelar = Tema.BotaoSecundario("Cancelar");
    cancelar.DialogResult = DialogResult.Cancel;

    var corpo = new Panel();
    corpo.Controls.Add(_txt);
    corpo.Controls.Add(lbl);
    Tema.Dialogo(this, corpo, ok, cancelar);
    StartPosition = FormStartPosition.CenterScreen;
  }
}
