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
    ClientSize = new Size(360, 110);

    var lbl = new Label { Text = mensagem, Dock = DockStyle.Top, Height = 24 };
    _txt.Text = valorInicial;
    var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Left = 190, Top = 72, Width = 75 };
    var cancelar = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, Left = 272, Top = 72, Width = 75 };

    var topo = new Panel { Dock = DockStyle.Top, Height = 60, Padding = new Padding(12, 10, 12, 0) };
    topo.Controls.Add(_txt);
    topo.Controls.Add(lbl);
    Controls.Add(ok);
    Controls.Add(cancelar);
    Controls.Add(topo);
    AcceptButton = ok;
    CancelButton = cancelar;
  }
}
