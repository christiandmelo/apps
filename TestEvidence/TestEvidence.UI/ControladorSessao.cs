using TestEvidence.Models;
using TestEvidence.Services;

namespace TestEvidence.UI;

/// <summary>Segura a sessão ativa (uma por processo) e liga barra, captura e persistência.</summary>
public sealed class ControladorSessao
{
  public static ControladorSessao? Ativo { get; private set; }

  /// <summary>Disparado quando uma sessão começa, pausa, encerra ou muda (histórico se atualiza).</summary>
  public static event EventHandler? EstadoMudou;

  public static void NotificarEstado() => EstadoMudou?.Invoke(null, EventArgs.Empty);

  private readonly CapturaService _captura = new();
  private readonly Form? _shell;
  private BarraFlutuanteForm? _barra;
  private bool _capturando;
  private bool _shellMinimizadoPorNos;

  public SessaoEvidencia Sessao { get; }
  public ConfiguracaoApp Config { get; }
  public SessaoRepository Repositorio { get; }
  public bool Pausada { get; private set; }

  public event EventHandler? SessaoAlterada;

  private ControladorSessao(SessaoEvidencia sessao, ConfiguracaoApp config, SessaoRepository repo, Form? shell)
  {
    Sessao = sessao;
    Config = config;
    Repositorio = repo;
    _shell = shell;
  }

  /// <summary>Ativa a sessão (nova ou retomada). Se já houver outra ativa, devolve a existente.</summary>
  public static ControladorSessao? Iniciar(SessaoEvidencia sessao, ConfiguracaoApp config, SessaoRepository repo, Form? shell)
  {
    if (Ativo != null)
    {
      if (Ativo.Sessao.Id == sessao.Id) return Ativo;
      MessageBox.Show("Já existe uma evidência em andamento. Finalize ou pause antes de iniciar outra.",
        "Evidência de Teste", MessageBoxButtons.OK, MessageBoxIcon.Information);
      return null;
    }

    sessao.Status = StatusSessao.EmAndamento;
    if (sessao.Etapas.Count == 0) sessao.Etapas.Add("Evidências");
    if (!sessao.Etapas.Contains(sessao.EtapaAtual)) sessao.EtapaAtual = sessao.Etapas[0];
    SalvarSeguro(repo, sessao);

    var c = new ControladorSessao(sessao, config, repo, shell);
    Ativo = c;
    c.MostrarBarra();
    if (config.MinimizarShellAoIniciar && shell is { WindowState: not FormWindowState.Minimized })
    {
      shell.WindowState = FormWindowState.Minimized;
      c._shellMinimizadoPorNos = true;
    }
    NotificarEstado();
    return c;
  }

  private void MostrarBarra()
  {
    _barra = new BarraFlutuanteForm(this);
    _barra.Show(); // sem owner, para não minimizar junto com o shell
  }

  public async Task EvidenciarAsync(TipoCaptura tipo)
  {
    if (_capturando || Pausada || _barra == null) return;
    _capturando = true;
    var barra = _barra;
    Bitmap? imagem = null;
    var tipoReal = tipo;
    RegiaoTela? regiaoUsada = null;
    try
    {
      barra.EsconderParaCaptura();
      await Task.Delay(150);

      using (new ContextoDpi())
      {
        switch (tipo)
        {
          case TipoCaptura.TelaInteira:
            imagem = _captura.CapturarMonitorDoCursor(out var tela);
            break;

          case TipoCaptura.RepetirRegiao when Sessao.UltimaRegiao != null:
            imagem = _captura.CapturarRegiao(Sessao.UltimaRegiao);
            regiaoUsada = Sessao.UltimaRegiao;
            break;

          default:
            tipoReal = TipoCaptura.Regiao;
            (imagem, regiaoUsada, tipoReal) = SelecionarRegiao();
            break;
        }
      }
    }
    catch (Exception ex)
    {
      imagem?.Dispose();
      imagem = null;
      barra.MostrarAposCaptura();
      _capturando = false;
      MessageBox.Show($"Não foi possível capturar a tela: {ex.Message}", "Evidência de Teste",
        MessageBoxButtons.OK, MessageBoxIcon.Warning);
      return;
    }

    barra.MostrarAposCaptura();

    try
    {
      if (imagem == null) return;
      RegistrarCaptura(imagem, tipoReal, regiaoUsada, barra);
    }
    finally
    {
      imagem?.Dispose();
      _capturando = false;
    }
  }

  private (Bitmap? imagem, RegiaoTela? regiao, TipoCaptura tipo) SelecionarRegiao()
  {
    using var tela = _captura.CapturarTelaVirtual();
    var telaVirtual = CapturaService.TelaVirtual;
    using var selecao = new SelecaoRegiaoForm(tela, telaVirtual, Sessao.UltimaRegiao);
    if (selecao.ShowDialog() != DialogResult.OK || selecao.Resultado == null)
      return (null, null, TipoCaptura.Regiao);

    var recorte = CapturaService.Recortar(tela, telaVirtual, selecao.Resultado);
    return (recorte, selecao.TipoResultado == TipoCaptura.TelaInteira ? null : selecao.Resultado, selecao.TipoResultado);
  }

  private void RegistrarCaptura(Bitmap imagem, TipoCaptura tipo, RegiaoTela? regiao, BarraFlutuanteForm barra)
  {
    Evidencia evidencia;
    try
    {
      var relativo = Repositorio.AdicionarImagem(Sessao, imagem);
      evidencia = new Evidencia
      {
        Ordem = Sessao.Evidencias.Count == 0 ? 1 : Sessao.Evidencias.Max(e => e.Ordem) + 1,
        Etapa = Sessao.EtapaAtual,
        ArquivoImagem = relativo,
        CapturadoEm = DateTime.Now,
        Tipo = tipo
      };
      Sessao.Evidencias.Add(evidencia);
      if (regiao != null) Sessao.UltimaRegiao = regiao;
      Repositorio.Salvar(Sessao);
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
      MessageBox.Show($"Não foi possível salvar o print: {ex.Message}", "Evidência de Teste",
        MessageBoxButtons.OK, MessageBoxIcon.Warning);
      return;
    }

    barra.Flash();
    SessaoAlterada?.Invoke(this, EventArgs.Empty);

    if (Config.PedirLegendaAposCaptura)
    {
      using var form = new LegendaForm(imagem, Sessao.Etapas, Sessao.EtapaAtual);
      form.PosicionarPerto(barra.Bounds);
      form.ShowDialog();

      try
      {
        if (form.Descartado)
        {
          Repositorio.ExcluirEvidencia(Sessao, evidencia);
        }
        else
        {
          evidencia.Legenda = form.Legenda;
          evidencia.Etapa = form.Etapa;
          Repositorio.Salvar(Sessao);
        }
      }
      catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
      {
        MessageBox.Show($"Não foi possível salvar a legenda: {ex.Message}", "Evidência de Teste",
          MessageBoxButtons.OK, MessageBoxIcon.Warning);
      }
      SessaoAlterada?.Invoke(this, EventArgs.Empty);
    }
    NotificarEstado();
  }

  public void TrocarEtapa(string etapa)
  {
    if (string.IsNullOrWhiteSpace(etapa)) return;
    if (!Sessao.Etapas.Contains(etapa)) Sessao.Etapas.Add(etapa);
    Sessao.EtapaAtual = etapa;
    SalvarSeguro(Repositorio, Sessao);
    SessaoAlterada?.Invoke(this, EventArgs.Empty);
  }

  public void Pausar()
  {
    Pausada = true;
    Sessao.Status = StatusSessao.Pausada;
    SalvarSeguro(Repositorio, Sessao);
    SessaoAlterada?.Invoke(this, EventArgs.Empty);
    NotificarEstado();
  }

  public void Retomar()
  {
    Pausada = false;
    Sessao.Status = StatusSessao.EmAndamento;
    SalvarSeguro(Repositorio, Sessao);
    SessaoAlterada?.Invoke(this, EventArgs.Empty);
    NotificarEstado();
  }

  /// <summary>Fecha a barra e abre a revisão.</summary>
  public void Finalizar()
  {
    var revisao = new RevisaoForm(Sessao, Repositorio, Config, _shell);
    Encerrar();
    revisao.Show();
  }

  /// <summary>Fecha a barra e deixa a sessão pausada em disco (retomável pelo histórico).</summary>
  public void Encerrar()
  {
    if (Ativo != this) return;
    Ativo = null;
    Sessao.Status = StatusSessao.Pausada;
    SalvarSeguro(Repositorio, Sessao);

    var barra = _barra;
    _barra = null;
    barra?.FecharDefinitivo();

    if (_shellMinimizadoPorNos && _shell is { IsDisposed: false })
      _shell.WindowState = FormWindowState.Normal;
    NotificarEstado();
  }

  public void SalvarPosicaoBarra(Point p)
  {
    Config.PosicaoBarra = new PosicaoTela(p.X, p.Y);
    Config.Salvar();
  }

  public static void SalvarSeguro(SessaoRepository repo, SessaoEvidencia sessao)
  {
    try { repo.Salvar(sessao); }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
      MessageBox.Show($"Não foi possível salvar a sessão: {ex.Message}", "Evidência de Teste",
        MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }
  }
}
