using TestEvidence.Models;

namespace TestEvidence.UI;

public sealed class NovaSessaoForm : Form
{
  private readonly CabecalhoSessaoPanel _cabecalho = new(comEtapas: true);
  private readonly ConfiguracaoApp _config;

  /// <summary>Dados preenchidos (ainda sem Id/Inicio: quem cria a sessão é o repositório).</summary>
  public SessaoEvidencia Dados { get; } = new();

  public NovaSessaoForm(ConfiguracaoApp config)
  {
    _config = config;
    Text = "Nova evidência";
    Icon = IconeApp.Obter();
    FormBorderStyle = FormBorderStyle.FixedDialog;
    StartPosition = FormStartPosition.CenterParent;
    MaximizeBox = MinimizeBox = false;
    ShowInTaskbar = false;
    ClientSize = new Size(560, 520);

    Dados.Testador = string.IsNullOrWhiteSpace(config.UltimoTestador) ? Environment.UserName : config.UltimoTestador;
    Dados.Ambiente = config.UltimoAmbiente;
    Dados.Etapas = new List<string>(config.EtapasPadrao);
    Dados.EtapaAtual = Dados.Etapas.FirstOrDefault() ?? "Antes";
    _cabecalho.Carregar(Dados, config.AmbientesUsados);

    var iniciar = new Button { Text = "Iniciar", Width = 90, Left = 360, Top = 12 };
    var cancelar = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, Width = 90, Left = 458, Top = 12 };
    iniciar.Click += (_, _) => Confirmar();
    var rodape = new Panel { Dock = DockStyle.Bottom, Height = 48 };
    rodape.Controls.Add(iniciar);
    rodape.Controls.Add(cancelar);

    Controls.Add(_cabecalho);
    Controls.Add(rodape);
    AcceptButton = null;
    CancelButton = cancelar;
  }

  private void Confirmar()
  {
    if (_cabecalho.Tarefa.Length == 0)
    {
      MessageBox.Show(this, "Informe a tarefa.", "Nova evidência", MessageBoxButtons.OK, MessageBoxIcon.Information);
      return;
    }
    _cabecalho.Aplicar(Dados);

    _config.UltimoTestador = Dados.Testador;
    _config.UltimoAmbiente = Dados.Ambiente;
    if (Dados.Ambiente.Length > 0 && !_config.AmbientesUsados.Contains(Dados.Ambiente))
      _config.AmbientesUsados.Insert(0, Dados.Ambiente);
    _config.Salvar();

    DialogResult = DialogResult.OK;
  }
}
