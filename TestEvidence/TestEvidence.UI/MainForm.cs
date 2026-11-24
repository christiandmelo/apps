using System.Diagnostics;
using AppsCm.Compartilhado;
using TestEvidence.Models;
using TestEvidence.Services;
using AppsCm.Visual;

namespace TestEvidence.UI;

public sealed class MainForm : Form
{
  private sealed record Opcao(string Valor, string Texto)
  {
    public override string ToString() => Texto;
  }

  private readonly SessaoRepository _repo = new();
  private ConfiguracaoApp _config = new();
  private bool _inicializado;
  private bool _ajustandoFiltros;
  private readonly HashSet<string> _gruposRecolhidos = new();
  private readonly System.Windows.Forms.Timer _timerBusca = new() { Interval = 300 };

  private readonly ComboBox _filtroCliente = ComboFiltro(170);
  private readonly ComboBox _filtroProjeto = ComboFiltro(170);
  private readonly ComboBox _filtroStatus = ComboFiltro(150);
  private readonly ComboBox _filtroResultado = ComboFiltro(120);
  private readonly TextBox _filtroTexto = new() { PlaceholderText = "Buscar...", Width = 170, Margin = new Padding(0, 0, 14, 0) };
  private readonly CheckBox _agrupar = new() { Text = "Agrupar por cliente/projeto", AutoSize = true, Margin = new Padding(0, 4, 14, 0) };
  private readonly Button _btnLimpar = Tema.BotaoSecundario("Limpar");
  private readonly Label _lblMostrando = new() { AutoSize = true, ForeColor = Tema.TextoSecundario, Margin = new Padding(14, 6, 0, 0) };

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
    Text = "Nenhuma evidência ainda.\nClique em \"Nova evidência\" para começar.",
    Visible = false
  };
  private readonly Label _lblSubtitulo = new() { AutoSize = true, ForeColor = Tema.TextoSecundario, Location = new Point(21, 44) };
  private readonly Button _btnNova = Tema.BotaoPrimario("+ Nova evidência");
  private readonly Button _btnConfig = Tema.BotaoSecundario("Configurações");
  private readonly Button _btnRevisao = Tema.BotaoSecundario("Abrir revisão");
  private readonly Button _btnRetomar = Tema.BotaoSecundario("Retomar");
  private readonly Button _btnGerar = Tema.BotaoSecundario("Gerar documento");
  private readonly Button _btnAbrirDoc = Tema.BotaoSecundario("Abrir documento");
  private readonly Button _btnAbrirPasta = Tema.BotaoSecundario("Abrir pasta");
  private readonly Button _btnExcluir = Tema.BotaoPerigo("Excluir");
  private SessaoEvidencia? _sessaoEmAndamento;

  /// <summary>True enquanto existe uma evidência ativa (barra flutuante aberta) — usado pelo shell ao fechar a aba.</summary>
  public bool EmSessaoAtiva => ControladorSessao.Ativo != null;

  private IWin32Window Dono => (IWin32Window?)ParentForm ?? this;

  public MainForm()
  {
    Text = "Evidência de Teste";
    Icon = IconeApp.Obter();
    ClientSize = new Size(1000, 620);
    MinimumSize = new Size(760, 440);
    StartPosition = FormStartPosition.CenterScreen;
    Tema.Aplicar(this);

    _lista.Columns.Add("Tarefa", 110);
    _lista.Columns.Add("Cliente", 130);
    _lista.Columns.Add("Projeto", 130);
    _lista.Columns.Add("Título", 260);
    _lista.Columns.Add("Início", 130);
    _lista.Columns.Add("Prints", 70);
    _lista.Columns.Add("Resultado", 120);
    _lista.Columns.Add("Status", 110);
    Tema.EstilizarLista(_lista);

    // cabeçalho: título + resumo à esquerda, ações principais à direita
    var cabecalho = new FaixaComBorda { Dock = DockStyle.Top, Height = 76, Padding = new Padding(20, 0, 20, 0) };
    var titulo = new Label { Text = "Evidências de teste", Font = Tema.FonteTitulo, AutoSize = true, Location = new Point(18, 12) };
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

    // corpo: aviso de evidência em andamento + cartão com a lista
    _btnRetomarFaixa.Dock = DockStyle.Right;
    _btnRetomarFaixa.Margin = Padding.Empty;
    _faixa.Controls.Add(_lblFaixa);
    _faixa.Controls.Add(_btnRetomarFaixa);

    var cartao = new Cartao { Dock = DockStyle.Fill };
    cartao.Controls.Add(_lista);
    cartao.Controls.Add(_vazio);
    _vazio.BringToFront();

    var barraFiltros = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, WrapContents = false, Padding = new Padding(0, 4, 0, 0) };
    barraFiltros.Controls.AddRange(new Control[]
    {
      RotuloFiltro("Cliente"), _filtroCliente, RotuloFiltro("Projeto"), _filtroProjeto,
      RotuloFiltro("Status"), _filtroStatus, RotuloFiltro("Resultado"), _filtroResultado,
      _filtroTexto, _agrupar, _btnLimpar, _lblMostrando
    });
    Tema.EstilizarCampo(_filtroTexto);
    _btnLimpar.Margin = Padding.Empty;

    var corpo = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20, 16, 20, 16) };
    corpo.Controls.Add(cartao);
    corpo.Controls.Add(barraFiltros);
    corpo.Controls.Add(_espacoFaixa);
    corpo.Controls.Add(_faixa);

    // rodapé: ações da evidência selecionada; Excluir separado à direita
    var rodape = new FaixaComBorda { BordaEmCima = true, Dock = DockStyle.Bottom, Height = 62, Padding = new Padding(20, 14, 20, 0) };
    var acoes = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, BackColor = Tema.Superficie };
    acoes.Controls.AddRange(new Control[] { _btnRevisao, _btnRetomar, _btnGerar, _btnAbrirDoc, _btnAbrirPasta });
    var acoesPerigo = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, WrapContents = false, BackColor = Tema.Superficie };
    _btnExcluir.Margin = Padding.Empty;
    acoesPerigo.Controls.Add(_btnExcluir);
    rodape.Controls.Add(acoes);
    rodape.Controls.Add(acoesPerigo);

    var menu = new ContextMenuStrip();
    menu.Items.Add("Abrir revisão", null, (_, _) => AbrirRevisao());
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
    CatalogoClientes.Alterado += AoAlterarCatalogo;

    _filtroCliente.SelectionChangeCommitted += (_, _) => AoMudarFiltro(() =>
    {
      _config.Filtro.ClienteId = ValorDe(_filtroCliente);
      _config.Filtro.ProjetoId = "";
    });
    _filtroProjeto.SelectionChangeCommitted += (_, _) => AoMudarFiltro(() => _config.Filtro.ProjetoId = ValorDe(_filtroProjeto));
    _filtroStatus.SelectionChangeCommitted += (_, _) => AoMudarFiltro(() =>
      _config.Filtro.Status = ValorDe(_filtroStatus) is { Length: > 0 } v ? Enum.Parse<StatusSessao>(v) : null);
    _filtroResultado.SelectionChangeCommitted += (_, _) => AoMudarFiltro(() =>
      _config.Filtro.Resultado = ValorDe(_filtroResultado) is { Length: > 0 } v ? Enum.Parse<ResultadoTeste>(v) : null);
    _agrupar.CheckedChanged += (_, _) => { if (!_ajustandoFiltros) AoMudarFiltro(() => _config.AgruparLista = _agrupar.Checked); };
    _filtroTexto.TextChanged += (_, _) => { if (!_ajustandoFiltros) { _timerBusca.Stop(); _timerBusca.Start(); } };
    _timerBusca.Tick += (_, _) =>
    {
      _timerBusca.Stop();
      AoMudarFiltro(() => _config.Filtro.Texto = _filtroTexto.Text.Trim());
    };
    _btnLimpar.Click += (_, _) =>
    {
      _timerBusca.Stop();
      AoMudarFiltro(() =>
      {
        _config.Filtro = new FiltroLista();
        _ajustandoFiltros = true;
        _filtroTexto.Clear();
        _ajustandoFiltros = false;
      });
    };
    Shown += (_, _) => Inicializar();
    VisibleChanged += (_, _) => { if (Visible) Inicializar(); };
    FormClosed += AoFechar;
  }

  private static ComboBox ComboFiltro(int largura)
  {
    var c = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = largura, Margin = new Padding(0, 0, 14, 0) };
    Tema.EstilizarCampo(c);
    return c;
  }

  private static Label RotuloFiltro(string texto)
  {
    var l = Tema.RotuloSecundario(texto);
    l.Margin = new Padding(0, 6, 6, 0);
    return l;
  }

  private static string ValorDe(ComboBox c) => (c.SelectedItem as Opcao)?.Valor ?? "";

  private static string GrupoChave(SessaoEvidencia s) => FiltroLista.ChaveCliente(s) + "|" + FiltroLista.ChaveProjeto(s);

  private void AoMudarFiltro(Action alterar)
  {
    if (_ajustandoFiltros || !_inicializado) return;
    alterar();
    _config.Salvar();
    Atualizar();
  }

  private void AoAlterarCatalogo(object? sender, EventArgs e)
  {
    if (IsDisposed || !IsHandleCreated) return;
    BeginInvoke(Atualizar);
  }

  // Sob o shell o Program.Main nunca roda: a inicialização vive aqui, de forma idempotente
  public void Inicializar()
  {
    if (_inicializado) return;
    _inicializado = true;
    try { Directory.CreateDirectory(CaminhoConfig.PastaSessoes); }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    _config = ConfiguracaoApp.Carregar();
    AplicarConfigNosControles();
    Atualizar();
  }

  private void AplicarConfigNosControles()
  {
    _ajustandoFiltros = true;
    _agrupar.Checked = _config.AgruparLista;
    _filtroTexto.Text = _config.Filtro.Texto;
    PreencherFixos();
    _ajustandoFiltros = false;
  }

  private void PreencherFixos()
  {
    static void Preencher(ComboBox c, IEnumerable<Opcao> opcoes, string valor)
    {
      c.Items.Clear();
      c.Items.AddRange(opcoes.Cast<object>().ToArray());
      c.SelectedItem = c.Items.Cast<Opcao>().FirstOrDefault(o => o.Valor == valor) ?? c.Items[0];
    }
    Preencher(_filtroStatus, new[]
    {
      new Opcao("", "(Todos)"),
      new Opcao(nameof(StatusSessao.EmAndamento), "Em andamento/Interrompida"),
      new Opcao(nameof(StatusSessao.Pausada), "Pausada"),
      new Opcao(nameof(StatusSessao.Finalizada), "Finalizada")
    }, _config.Filtro.Status?.ToString() ?? "");
    Preencher(_filtroResultado, new[]
    {
      new Opcao("", "(Todos)"),
      new Opcao(nameof(ResultadoTeste.Pendente), "Pendente"),
      new Opcao(nameof(ResultadoTeste.Aprovado), "Aprovado"),
      new Opcao(nameof(ResultadoTeste.Reprovado), "Reprovado"),
      new Opcao(nameof(ResultadoTeste.ComRessalvas), "Com ressalvas")
    }, _config.Filtro.Resultado?.ToString() ?? "");
  }

  /// <summary>Repopula Cliente e Projeto com base em todas as sessões, preservando a seleção (some se deixou de existir).</summary>
  private void PreencherFiltrosDinamicos(IReadOnlyList<SessaoEvidencia> todas, Func<SessaoEvidencia, (string cliente, string projeto)> nomes)
  {
    var filtro = _config.Filtro;
    var comparador = StringComparer.CurrentCultureIgnoreCase;

    var clientes = todas.Where(s => FiltroLista.ChaveCliente(s) != FiltroLista.SemCliente)
      .GroupBy(FiltroLista.ChaveCliente)
      .Select(g => new Opcao(g.Key, nomes(g.First()).cliente))
      .OrderBy(o => o.Texto, comparador)
      .ToList();
    var opcoesCliente = new List<Opcao> { new("", "(Todos)") };
    opcoesCliente.AddRange(clientes);
    if (todas.Any(s => FiltroLista.ChaveCliente(s) == FiltroLista.SemCliente))
      opcoesCliente.Add(new Opcao(FiltroLista.SemCliente, "(sem cliente)"));
    if (opcoesCliente.All(o => o.Valor != filtro.ClienteId)) filtro.ClienteId = "";

    var projetos = todas.Where(s => FiltroLista.ChaveProjeto(s).Length > 0 && (filtro.ClienteId.Length == 0 || FiltroLista.ChaveCliente(s) == filtro.ClienteId))
      .GroupBy(FiltroLista.ChaveProjeto)
      .Select(g => new Opcao(g.Key, nomes(g.First()).projeto))
      .OrderBy(o => o.Texto, comparador)
      .ToList();
    var opcoesProjeto = new List<Opcao> { new("", "(Todos)") };
    opcoesProjeto.AddRange(projetos);
    if (opcoesProjeto.All(o => o.Valor != filtro.ProjetoId)) filtro.ProjetoId = "";

    _ajustandoFiltros = true;
    Definir(_filtroCliente, opcoesCliente, filtro.ClienteId);
    Definir(_filtroProjeto, opcoesProjeto, filtro.ProjetoId);
    _ajustandoFiltros = false;

    static void Definir(ComboBox c, List<Opcao> opcoes, string valor)
    {
      c.Items.Clear();
      c.Items.AddRange(opcoes.Cast<object>().ToArray());
      c.SelectedItem = opcoes.First(o => o.Valor == valor);
    }
  }

  private void AoEstadoMudar(object? sender, EventArgs e)
  {
    if (IsDisposed || !IsHandleCreated) return;
    BeginInvoke(Atualizar);
  }

  private void AoFechar(object? sender, FormClosedEventArgs e)
  {
    ControladorSessao.EstadoMudou -= AoEstadoMudar;
    CatalogoClientes.Alterado -= AoAlterarCatalogo;
    _timerBusca.Dispose();
    // fechar a aba não pode deixar a barra órfã: a sessão fica Pausada em disco
    ControladorSessao.Ativo?.Encerrar();
  }

  private SessaoEvidencia? Selecionada => _lista.SelectedItems.Count > 0 ? _lista.SelectedItems[0].Tag as SessaoEvidencia : null;

  private void Atualizar()
  {
    if (!_inicializado) return;
    var selecionadaId = Selecionada?.Id;

    // memoriza os grupos recolhidos antes de recriá-los, para não reabrirem a cada print capturado
    foreach (ListViewGroup g in _lista.Groups)
    {
      if (g.CollapsedState == ListViewGroupCollapsedState.Collapsed) _gruposRecolhidos.Add(g.Name);
      else _gruposRecolhidos.Remove(g.Name);
    }

    var todas = _repo.Listar();
    var catalogo = CatalogoClientes.Carregar();
    (string cliente, string projeto) Nomes(SessaoEvidencia s) => VinculoCatalogo.Nomes(s, catalogo);
    PreencherFiltrosDinamicos(todas, Nomes);
    var filtro = _config.Filtro;
    var sessoes = todas.Where(s => filtro.Aceita(s, Nomes)).ToList();

    _lista.BeginUpdate();
    _lista.Items.Clear();
    _lista.Groups.Clear();
    _lista.ShowGroups = _config.AgruparLista;
    var grupos = new Dictionary<string, ListViewGroup>();
    if (_config.AgruparLista)
    {
      var ordenados = sessoes.GroupBy(GrupoChave)
        .Select(g => (chave: g.Key, qtd: g.Count(), nomes: Nomes(g.First()), semCliente: FiltroLista.ChaveCliente(g.First()) == FiltroLista.SemCliente))
        .OrderBy(g => g.semCliente)
        .ThenBy(g => g.nomes.cliente, StringComparer.CurrentCultureIgnoreCase)
        .ThenBy(g => g.nomes.projeto.Length == 0)
        .ThenBy(g => g.nomes.projeto, StringComparer.CurrentCultureIgnoreCase);
      foreach (var g in ordenados)
      {
        var titulo = g.semCliente ? "(sem cliente)"
          : g.nomes.projeto.Length > 0 ? $"{g.nomes.cliente} / {g.nomes.projeto}" : $"{g.nomes.cliente} / (sem projeto)";
        var grupo = new ListViewGroup(g.chave, $"{titulo} ({g.qtd})")
        {
          CollapsedState = _gruposRecolhidos.Contains(g.chave) ? ListViewGroupCollapsedState.Collapsed : ListViewGroupCollapsedState.Expanded
        };
        grupos[g.chave] = grupo;
        _lista.Groups.Add(grupo);
      }
    }
    foreach (var s in sessoes)
    {
      var (nomeCliente, nomeProjeto) = Nomes(s);
      var item = new ListViewItem(s.Tarefa) { Tag = s, UseItemStyleForSubItems = false };
      item.SubItems.Add(nomeCliente);
      item.SubItems.Add(nomeProjeto);
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
      if (_config.AgruparLista) item.Group = grupos[GrupoChave(s)];
      _lista.Items.Add(item);
    }
    _lista.EndUpdate();
    _vazio.Visible = todas.Count == 0;
    _lblMostrando.Text = filtro.Ativo ? $"Mostrando {sessoes.Count} de {todas.Count}" : "";
    var abertas = todas.Count(x => x.Status != StatusSessao.Finalizada);
    _lblSubtitulo.Text = todas.Count == 0 ? "Nenhuma evidência registrada"
      : $"{todas.Count} {(todas.Count == 1 ? "evidência" : "evidências")}" + (abertas > 0 ? $" · {abertas} em aberto" : "");

    _sessaoEmAndamento = ControladorSessao.Ativo == null
      ? todas.FirstOrDefault(s => s.Status != StatusSessao.Finalizada)
      : null;
    _faixa.Visible = _espacoFaixa.Visible = _sessaoEmAndamento != null;
    if (_sessaoEmAndamento != null)
      _lblFaixa.Text = $"Evidência em andamento: {_sessaoEmAndamento.Tarefa} — {_sessaoEmAndamento.Titulo}";
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
      MessageBox.Show(Dono, $"Não foi possível criar a sessão: {ex.Message}", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
    if (MessageBox.Show(Dono, $"Excluir a evidência {s.Tarefa} e todos os prints? (vai para a Lixeira)", Text,
          MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
    try
    {
      _repo.ExcluirSessao(s.Id);
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
      MessageBox.Show(Dono, $"Não foi possível excluir: {ex.Message}", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }
    Atualizar();
  }
}
