using AppsCm.Visual;
using TestEvidence.Models;
using TestEvidence.Services;
using TestEvidence.Services.Nativo;

namespace TestEvidence.UI;

public sealed class ConfigForm : Form
{
  private readonly ConfiguracaoApp _config;
  private readonly TextBox _atalhoEvidenciar = new() { Dock = DockStyle.Fill };
  private readonly TextBox _atalhoRepetir = new() { Dock = DockStyle.Fill };
  private readonly CheckBox _chkLegenda = new() { Text = "Pedir legenda após cada captura", AutoSize = true };
  private readonly CheckBox _chkMinimizar = new() { Text = "Minimizar o apps-cm ao iniciar uma evidência", AutoSize = true };
  private readonly TextBox _pasta = new() { Dock = DockStyle.Fill };
  private readonly TextBox _etapas = new() { Multiline = true, Dock = DockStyle.Fill, AcceptsReturn = true };

  public ConfigForm(ConfiguracaoApp config)
  {
    _config = config;
    Text = "Configurações";
    Icon = IconeApp.Obter();
    FormBorderStyle = FormBorderStyle.FixedDialog;
    StartPosition = FormStartPosition.CenterParent;
    MaximizeBox = MinimizeBox = false;
    ShowInTaskbar = false;
    ClientSize = new Size(640, 430);

    _atalhoEvidenciar.Text = config.AtalhoEvidenciar;
    _atalhoRepetir.Text = config.AtalhoRepetirRegiao;
    _chkLegenda.Checked = config.PedirLegendaAposCaptura;
    _chkMinimizar.Checked = config.MinimizarShellAoIniciar;
    _pasta.Text = config.PastaSaidaDocumentos;
    _etapas.Text = string.Join(Environment.NewLine, config.EtapasPadrao);

    var testar = Tema.BotaoSecundario("Testar");
    testar.Dock = DockStyle.Fill;
    testar.Click += (_, _) => TestarAtalhos();
    var procurar = Tema.BotaoSecundario("...");
    procurar.Dock = DockStyle.Fill;
    procurar.Click += (_, _) =>
    {
      using var dlg = new FolderBrowserDialog { SelectedPath = _config.PastaSaidaEfetiva };
      if (dlg.ShowDialog(this) == DialogResult.OK) _pasta.Text = dlg.SelectedPath;
    };

    var grade = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 3, AutoSize = true, Padding = Padding.Empty };
    grade.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
    grade.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
    grade.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));

    void Linha(string rotulo, Control campo, Control? extra, int altura = 28)
    {
      grade.RowStyles.Add(new RowStyle(SizeType.Absolute, altura + 8));
      var lbl = Tema.Rotulo(rotulo);
      lbl.AutoSize = false;
      lbl.Dock = DockStyle.Fill;
      lbl.TextAlign = ContentAlignment.MiddleLeft;
      Tema.EstilizarCampo(campo);
      grade.Controls.Add(lbl);
      grade.Controls.Add(campo);
      grade.Controls.Add(extra ?? new Label());
    }

    Linha("Atalho evidenciar", _atalhoEvidenciar, testar);
    Linha("Atalho repetir região", _atalhoRepetir, null);
    Linha("Pasta dos documentos", _pasta, procurar);
    Linha("Etapas padrão (uma por linha)", _etapas, null, 80);
    Linha("", _chkLegenda, null);
    Linha("", _chkMinimizar, null);

    var salvar = Tema.BotaoPrimario("Salvar");
    var cancelar = Tema.BotaoSecundario("Cancelar");
    cancelar.DialogResult = DialogResult.Cancel;
    salvar.Click += (_, _) => Salvar();

    var corpo = new Panel();
    corpo.Controls.Add(grade);
    Tema.Dialogo(this, corpo, salvar, cancelar);
    AcceptButton = null;   // campo de etapas é multilinha
  }

  private bool Validar()
  {
    foreach (var (texto, nome) in new[] { (_atalhoEvidenciar.Text, "evidenciar"), (_atalhoRepetir.Text, "repetir região") })
    {
      if (!AtalhoGlobal.TentarConverter(texto, out _, out var erro))
      {
        MessageBox.Show(this, $"Atalho de {nome}: {erro}", "Configurações", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return false;
      }
    }
    if (string.Equals(_atalhoEvidenciar.Text.Trim(), _atalhoRepetir.Text.Trim(), StringComparison.OrdinalIgnoreCase))
    {
      MessageBox.Show(this, "Os dois atalhos não podem ser iguais.", "Configurações", MessageBoxButtons.OK, MessageBoxIcon.Warning);
      return false;
    }
    return true;
  }

  private void TestarAtalhos()
  {
    if (!Validar()) return;
    using var atalhos = new AtalhoGlobal();
    atalhos.Vincular(Handle);
    var ok1 = atalhos.Registrar(100, _atalhoEvidenciar.Text);
    var ok2 = atalhos.Registrar(101, _atalhoRepetir.Text);
    atalhos.DesregistrarTodos();

    var msg = ok1 && ok2
      ? "Os dois atalhos estão livres."
      : "Em uso por outro app: " + string.Join(", ", new[] { ok1 ? null : _atalhoEvidenciar.Text, ok2 ? null : _atalhoRepetir.Text }.OfType<string>());
    MessageBox.Show(this, msg, "Testar atalhos", MessageBoxButtons.OK, ok1 && ok2 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
  }

  private void Salvar()
  {
    if (!Validar()) return;
    _config.AtalhoEvidenciar = _atalhoEvidenciar.Text.Trim();
    _config.AtalhoRepetirRegiao = _atalhoRepetir.Text.Trim();
    _config.PedirLegendaAposCaptura = _chkLegenda.Checked;
    _config.MinimizarShellAoIniciar = _chkMinimizar.Checked;
    _config.PastaSaidaDocumentos = _pasta.Text.Trim();
    var etapas = _etapas.Lines.Select(l => l.Trim()).Where(l => l.Length > 0).Distinct().ToList();
    if (etapas.Count > 0) _config.EtapasPadrao = etapas;
    _config.Salvar();
    DialogResult = DialogResult.OK;
  }
}
