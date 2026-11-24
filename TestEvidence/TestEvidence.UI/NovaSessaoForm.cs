using AppsCm.Compartilhado;
using AppsCm.Visual;
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
    ClientSize = new Size(640, 672);

    Dados.Testador = string.IsNullOrWhiteSpace(config.UltimoTestador) ? Environment.UserName : config.UltimoTestador;
    if (config.UltimoClienteId.Length > 0 && CatalogoClientes.Carregar().ClientePorId(config.UltimoClienteId) is { Ativo: true } ultimo)
    {
      Dados.ClienteId = ultimo.Id;
      Dados.Cliente = ultimo.Nome;
    }
    Dados.Ambiente = config.UltimoAmbiente;
    Dados.Etapas = new List<string>(config.EtapasPadrao);
    Dados.EtapaAtual = Dados.Etapas.FirstOrDefault() ?? "Antes";
    _cabecalho.Carregar(Dados, config.AmbientesUsados);

    var iniciar = Tema.BotaoPrimario("Iniciar");
    var cancelar = Tema.BotaoSecundario("Cancelar");
    cancelar.DialogResult = DialogResult.Cancel;
    iniciar.Click += (_, _) => Confirmar();

    var corpo = new Panel();
    corpo.Controls.Add(_cabecalho);
    Tema.Dialogo(this, corpo, iniciar, cancelar);
    AcceptButton = null;   // campos multilinha usam Enter
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
    if (Dados.ClienteId.Length > 0) _config.UltimoClienteId = Dados.ClienteId;
    _config.UltimoAmbiente = Dados.Ambiente;
    if (Dados.Ambiente.Length > 0 && !_config.AmbientesUsados.Contains(Dados.Ambiente))
      _config.AmbientesUsados.Insert(0, Dados.Ambiente);
    _config.Salvar();

    DialogResult = DialogResult.OK;
  }
}
