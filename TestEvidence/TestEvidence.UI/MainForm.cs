using System.Diagnostics;
using TestEvidence.Models;
using TestEvidence.Services;
using AppsCm.Visual;

namespace TestEvidence.UI;

public sealed class MainForm : Form
{
  private readonly SessaoRepository _repo = new();
  private ConfiguracaoApp _config = new();
  private bool _inicializado;

  private readonly Aviso _faixa = new() { Dock = DockStyle.Top, Height = 52, Visible = false };
  private readonly Panel _espacoFaixa = new() { Dock = DockStyle.Top, Height = 12, Visible = false };
  private readonly Label _lblFaixa = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Tema.Alerta, Font = Tema.FonteSemibold };
  private readonly Button _btnRetomarFaixa = Tema.BotaoPrimario("Retomar");
  private readonly ListView _lista = new()
  {
    Dock = DockStyle.Fill,
    View = View.Details,
    FullRowSelect = true,
    MultiSelect = false,
    HideSelection = false
  };
  private readonly Label _vazio = new()
  {
    Dock = DockStyle.Fill,
    TextAlign = ContentAlignment.MiddleCenter,
    ForeColor = Tema.TextoSecundario,
    BackColor = Tema.Superficie,
    Text = "Nenhuma evidÃªncia ainda.\nClique em \"Nova evidÃªncia\" para comeÃ§ar.",
    Visible = false
  };
  private readonly Label _lblSubtitulo = new() { AutoSize = true, ForeColor = Tema.TextoSecundario, Location = new Point(21, 44) };
  private readonly Button _btnNova = Tema.BotaoPrimario("+ Nova evidÃªncia");
  private readonly Button _btnConfig = Tema.BotaoSecundario("ConfiguraÃ§Ãµes");
  private readonly Button _btnRevisao = Tema.BotaoSecundario("Abrir revisÃ£o");
  private readonly Button _btnRetomar = Tema.BotaoSecundario("Retomar");
  private readonly Button _btnGerar = Tema.BotaoSecundario("Gerar documento");
  private readonly Button _btnAbrirDoc = Tema.BotaoSecundario("Abrir documento");
  private readonly Button _btnAbrirPasta = Tema.BotaoSecundario("Abrir pasta");
  private readonly Button _btnExcluir = Tema.BotaoPerigo("Excluir");
  private SessaoEvidencia? _sessaoEmAndamento;

  /// <summary>True enquanto existe uma evidÃªncia ativa (barra flutuante aberta) â€” usado pelo shell ao fechar a aba.</summary>
  public bool EmSessaoAtiva => ControladorSessao.Ativo != null;

  private IWin32Window Dono => (IWin32Window?)ParentForm ?? this;

  public MainForm()
  {
    Text = "EvidÃªncia de Teste";
    Icon = IconeApp.Obter();
    ClientSize = new Size(1000, 620);
    MinimumSize = new Size(760, 440);
    StartPosition = FormStartPosition.CenterScreen;
    Tema.Aplicar(this);

    _lista.Columns.Add("Tarefa", 130);
    _lista.Columns.Add("TÃ­tulo", 300);
    _lista.Columns.Add("InÃ­cio", 130);
    _lista.Columns.Add("Prints", 70);
    _lista.Columns.Add("Resultado", 120);
    _lista.Columns.Add("Status", 110);
    Tema.EstilizarLista(_lista);

    // cabeÃ§alho: tÃ­tulo + resumo Ã  esquerda, aÃ§Ãµes principais Ã  direita
    var cabecalho = new FaixaComBorda { Dock = DockStyle.Top, Height = 76, Padding = new Padding(20, 0, 20, 0) };
    var titulo = new Label { Text = "EvidÃªncias de teste", Font = Tema.FonteTitulo, AutoSize = true, Location = new Point(18, 12) };
    var botoesTopo = new FlowLayoutPanel
    {
      Dock = DockStyle.Right,
      AutoSize = true,
      WrapContents = false,
      Padding = new Padding(0, 21, 0, 0),
      BackColor = Tema.Superficie
    };
    _btnNova.Margin = Padding.Empty;
    botoesTopo.Controls.AddRange(new Control[] { _btnConfig, _btnNova });
    cabecalho.Controls.Add(titulo);
    cabecalho.Controls.Add(_lblSubtitulo);
    cabecalho.Controls.Add(botoesTopo);

    // corpo: aviso de evidÃªncia em andamento + cartÃ£o com a lista
    _btnRetomarFaixa.Dock = DockStyle.Right;
    _btnRetomarFaixa.Margin = Padding.Empty;
    _faixa.Controls.Add(_lblFaixa);
    _faixa.Controls.Add(_btnRetomarFaixa);

    var cartao = new Cartao { Dock = DockStyle.Fill };
    cartao.Controls.Add(_lista);
    cartao.Controls.Add(_vazio);
    _vazio.BringToFront();

    var corpo = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20, 16, 20, 16) };
    corpo.Controls.Add(cartao);
    corpo.Controls.Add(_espacoFaixa);
    corpo.Controls.Add(_faixa);

    // rodapÃ©: aÃ§Ãµes da evidÃªncia selecionada; Excluir separado Ã  direita
    var rodape = new FaixaComBorda { BordaEmCima = true, Dock = DockStyle.Bottom, Height = 62, Padding = new Padding(20, 14, 20, 0) };
    var acoes = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, BackColor = Tema.Superficie };
    acoes.Controls.AddRange(new Control[] { _btnRevisao, _btnRetomar, _btnGerar, _btnAbrirDoc, _btnAbrirPasta });
    var acoesPerigo = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, WrapContents = false, BackColor = Tema.Superficie };
    _btnExcluir.Margin = Padding.Empty;
    acoesPerigo.Controls.Add(_btnExcluir);
    rodape.Controls.Add(acoes);
    rodape.Controls.Add(acoesPerigo);

    var menu = new ContextMenuStrip();
    menu.Items.Add("Abrir revisÃ£o", null, (_, _) => AbrirRevisao());
    menu.Items.Add("Retomar", null, (_, _) => Retomar(Selecionada));
    menu.Items.Add("Gerar documento", null, (_, _) => GerarDocumento());
    menu.Items.Add("Abrir documento", null, (_, _) => AbrirDocumento());
    menu.Items.Add("Abrir pasta", null, (_, _) => AbrirPasta());
    menu.Items.Add("Excluir", null, (_, _) => Excluir());
    _lista.ContextMenuStrip = menu;

    Controls.Add(corpo);
    Controls.Add(rodape);
    Controls.Add(cabecalho);

    _btnNova.Click += (_, _) => NovaEvidencia();
    _btnConfig.Click += (_, _) => { using var f = new ConfigForm(_config); f.ShowDialog(Dono); };
    _btnRetomarFaixa.Click += (_, _) => Retomar(_sessaoEmAndamento);
    _btnRevisao.Click += (_, _) => AbrirRevisao();
    _btnRetomar.Click += (_, _) => Retomar(Selecionada);
    _btnGerar.Click += (_, _) => GerarDocumento();
    _btnAbrirDoc.Click += (_, _) => AbrirDocumento();
    _btnAbrirPasta.Click += (_, _) => AbrirPasta();
    _btnExcluir.Click += (_, _) => Excluir();
    _lista.SelectedIndexChanged += (_, _) => AtualizarBotoes();
    _lista.DoubleClick += (_, _) => AbrirRevisao();

    ControladorSessao.EstadoMudou += AoEstadoMudar;
    Shown += (_, _) => Inicializar();
    VisibleChanged += (_, _) => { if (Visible) Inicializar(); };
    FormClosed += AoFechar;
  }

  // Sob o shell o Program.Main nunca roda: a inicializaÃ§Ã£o vive aqui, de forma idempotente
  public void Inicializar()
  {
    if (_inicializado) return;
    _inicializado = true;
    try { Directory.CreateDirectory(CaminhoConfig.PastaSessoes); }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    _config = ConfiguracaoApp.Carregar();
    Atualizar();
  }

  private void AoEstadoMudar(object? sender, EventArgs e)
  {
    if (IsDisposed || !IsHandleCreated) return;
    BeginInvoke(Atualizar);
  }

  private void AoFechar(object? sender, FormClosedEventArgs e)
  {
    ControladorSessao.EstadoMudou -= AoEstadoMudar;
    // fechar a aba nÃ£o pode deixar a barra Ã³rfÃ£: a sessÃ£o fica Pausada em disco
    ControladorSessao.Ativo?.Encerrar();
  }

  private SessaoEvidencia? Selecionada => _lista.SelectedItems.Count > 0 ? _lista.SelectedItems[0].Tag as SessaoEvidencia : null;

  private void Atualizar()
  {
    if (!_inicializado) return;
    var selecionadaId = Selecionada?.Id;
    var sessoes = _repo.Listar();

    _lista.BeginUpdate();
    _lista.Items.Clear();
    foreach (var s in sessoes)
    {
      var item = new ListViewItem(s.Tarefa) { Tag = s, UseItemStyleForSubItems = false };
      item.SubItems.Add(s.Titulo);
      item.SubItems.Add(s.Inicio.ToString("dd/MM/yyyy HH:mm"));
      item.SubItems.Add(s.Evidencias.Count.ToString());
      var (textoResultado, corResultado) = s.Resultado switch
      {
        ResultadoTeste.Aprovado => ("Aprovado", Tema.Sucesso),
        ResultadoTeste.Reprovado => ("Reprovado", Tema.Perigo),
        ResultadoTeste.ComRessalvas => ("Com ressalvas", Tema.Alerta),
        _ => ("Pendente", Tema.TextoSecundario)
      };
      item.SubItems.Add(new ListViewItem.ListViewSubItem(item, textoResultado) { ForeColor = corResultado, Font = Tema.FonteSemibold });
      var ativa = ControladorSessao.Ativo?.Sessao.Id == s.Id;
      item.SubItems.Add(ativa ? "Em andamento" : s.Status switch
      {
        StatusSessao.EmAndamento => "Interrompida",
        StatusSessao.Pausada => "Pausada",
        _ => "Finalizada"
      });
      if (s.Id == selecionadaId) item.Selected = true;
      _lista.Items.Add(item);
    }
    _lista.EndUpdate();
    _vazio.Visible = sessoes.Count == 0;
    var abertas = sessoes.Count(x => x.Status != StatusSessao.Finalizada);
    _lblSubtitulo.Text = sessoes.Count == 0 ? "Nenhuma evidÃªncia registrada"
      : $"{sessoes.Count} {(sessoes.Count == 1 ? "evidÃªncia" : "evidÃªncias")}" + (abertas > 0 ? $" Â· {abertas} em aberto" : "");

    _sessaoEmAndamento = ControladorSessao.Ativo == null
      ? sessoes.FirstOrDefault(s => s.Status != StatusSessao.Finalizada)
      : null;
    _faixa.Visible = _espacoFaixa.Visible = _sessaoEmAndamento != null;
    if (_sessaoEmAndamento != null)
      _lblFaixa.Text = $"EvidÃªncia em andamento: {_sessaoEmAndamento.Tarefa} â€” {_sessaoEmAndamento.Titulo}";
    AtualizarBotoes();
  }

  private void AtualizarBotoes()
  {
    var s = Selecionada;
    var temSelecao = s != null;
    var semAtiva = ControladorSessao.Ativo == null;
    _btnRevisao.Enabled = temSelecao && ControladorSessao.Ativo?.Sessao.Id != s!.Id;
    _btnRetomar.Enabled = temSelecao && semAtiva && s!.Status != StatusSessao.Finalizada;
    _btnGerar.Enabled = _btnRevisao.Enabled;
    _btnAbrirDoc.Enabled = temSelecao && !string.IsNullOrEmpty(s!.UltimoDocumentoGerado) && File.Exists(s.UltimoDocumentoGerado);
    _btnAbrirPasta.Enabled = temSelecao;
    _btnExcluir.Enabled = temSelecao && ControladorSessao.Ativo?.Sessao.Id != s!.Id;
    _btnNova.Enabled = semAtiva;
  }

  private Form? FormularioDono => ParentForm;

  private void NovaEvidencia()
  {
    using var form = new NovaSessaoForm(_config);
    if (form.ShowDialog(Dono) != DialogResult.OK) return;
    try
    {
      var sessao = _repo.Criar(form.Dados);
      ControladorSessao.Iniciar(sessao, _config, _repo, FormularioDono ?? this);
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
      MessageBox.Show(Dono, $"NÃ£o foi possÃ­vel criar a sessÃ£o: {ex.Message}", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }
  }

  private void Retomar(SessaoEvidencia? sessao)
  {
    if (sessao == null) return;
    var completa = _repo.Carregar(sessao.Id) ?? sessao;
    ControladorSessao.Iniciar(completa, _config, _repo, FormularioDono ?? this);
  }

  private void AbrirRevisao()
  {
    if (Selecionada is not { } s || ControladorSessao.Ativo?.Sessao.Id == s.Id) return;
    var completa = _repo.Carregar(s.Id) ?? s;
    new RevisaoForm(completa, _repo, _config, FormularioDono ?? this).Show();
  }

  private void GerarDocumento()
  {
    if (Selecionada is not { } s || ControladorSessao.Ativo?.Sessao.Id == s.Id) return;
    var completa = _repo.Carregar(s.Id) ?? s;
    GeradorDocumento.Gerar(Dono, completa, _repo, _config);
  }

  private void AbrirDocumento()
  {
    if (Selecionada?.UltimoDocumentoGerado is { Length: > 0 } arq && File.Exists(arq))
      Abrir(arq);
  }

  private void AbrirPasta()
  {
    if (Selecionada is { } s)
      Abrir(_repo.PastaDa(s));
  }

  private static void Abrir(string caminho)
  {
    try { Process.Start(new ProcessStartInfo(caminho) { UseShellExecute = true }); }
    catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or FileNotFoundException) { }
  }

  private void Excluir()
  {
    if (Selecionada is not { } s) return;
    if (MessageBox.Show(Dono, $"Excluir a evidÃªncia {s.Tarefa} e todos os prints? (vai para a Lixeira)", Text,
          MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
    try
    {
      _repo.ExcluirSessao(s.Id);
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
      MessageBox.Show(Dono, $"NÃ£o foi possÃ­vel excluir: {ex.Message}", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }
    Atualizar();
  }
}
