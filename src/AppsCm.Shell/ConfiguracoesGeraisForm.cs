using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using AppsCm.Compartilhado;
using AppsCm.Visual;

namespace AppsCm.Shell;

/// <summary>Configurações gerais do shell: conexão com o Jira (jira.json) e cadastro de clientes e projetos (clientes.json).</summary>
public sealed class ConfiguracoesGeraisForm : Form
{
  private const string Origem = "shell";
  private readonly ConexaoJira _jira = ConexaoJira.Carregar();

  // Jira
  private readonly TextBox _url = new() { PlaceholderText = "ex.: https://jiraproducao.totvs.com.br" };
  private readonly TextBox _token = new() { UseSystemPasswordChar = true };
  private readonly TextBox _headers = new() { Multiline = true, AcceptsReturn = true, ScrollBars = ScrollBars.Vertical, WordWrap = false };
  private readonly CheckBox _verificarSsl = new() { Text = "Verificar certificado SSL", AutoSize = true };
  private readonly NumericUpDown _timeout = new() { Minimum = 5, Maximum = 300, Width = 80 };
  private readonly Button _testar = Tema.BotaoSecundario("Testar conexão");
  private readonly Label _resultadoTeste = new() { AutoSize = true, MaximumSize = new Size(560, 0) };
  private readonly Label _avisoJira = new() { AutoSize = true, ForeColor = Tema.Alerta, MaximumSize = new Size(600, 0) };
  private bool _tokenAlterado;

  // Clientes e projetos
  private readonly TreeView _arvore = new() { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, HideSelection = false, ItemHeight = 26, ShowLines = false };
  private readonly CheckBox _mostrarInativos = new() { Text = "Mostrar inativos", AutoSize = true };
  private readonly Label _avisoCatalogo = new() { Dock = DockStyle.Top, AutoSize = true, ForeColor = Tema.Alerta, MaximumSize = new Size(700, 0), Visible = false };
  private readonly Button _novoCliente = Tema.BotaoPrimario("Novo cliente");
  private readonly Button _novoProjeto = Tema.BotaoSecundario("Novo projeto");
  private readonly Button _renomear = Tema.BotaoSecundario("Renomear");
  private readonly Button _alternarAtivo = Tema.BotaoPerigo("Desativar");
  private readonly Button _chaveJira = Tema.BotaoSecundario("Chave do Jira");

  private sealed record NoCatalogo(string ClienteId, string? ProjetoId);

  public ConfiguracoesGeraisForm()
  {
    Text = "Configurações gerais";
    ClientSize = new Size(780, 600);

    var abas = new TabControl { Dock = DockStyle.Fill };
    var abaJira = new TabPage("Jira");
    var abaClientes = new TabPage("Clientes e projetos");
    abaJira.Controls.Add(ConstruirJira());
    abaClientes.Controls.Add(ConstruirClientes());
    abas.TabPages.Add(abaJira);
    abas.TabPages.Add(abaClientes);
    Tema.EstilizarAbas(abas);

    var salvar = Tema.BotaoPrimario("Salvar Jira");
    var fechar = Tema.BotaoSecundario("Fechar");
    fechar.DialogResult = DialogResult.Cancel;
    salvar.Click += (_, _) => SalvarJira();
    Tema.Dialogo(this, abas, salvar, fechar);
    AcceptButton = null;
    StartPosition = FormStartPosition.CenterParent;

    CarregarJira();
    Recarregar();

    _novoCliente.Click += (_, _) => NovoCliente();
    _novoProjeto.Click += (_, _) => NovoProjeto();
    _renomear.Click += (_, _) => Renomear();
    _alternarAtivo.Click += (_, _) => AlternarAtivo();
    _chaveJira.Click += (_, _) => DefinirChaveJira();
    _mostrarInativos.CheckedChanged += (_, _) => Recarregar();
    _arvore.AfterSelect += (_, _) => AtualizarBotoes();
    _testar.Click += async (_, _) => await TestarConexaoAsync();
    _token.TextChanged += (_, _) => _tokenAlterado = true;

    CatalogoClientes.Alterado += AoAlterarCatalogo;
    FormClosed += (_, _) => CatalogoClientes.Alterado -= AoAlterarCatalogo;
  }

  // ---------- Jira ----------

  private Control ConstruirJira()
  {
    var grade = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, AutoSize = true, Padding = new Padding(8) };
    grade.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
    grade.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

    void Linha(string rotulo, Control campo, int altura = 28)
    {
      grade.RowStyles.Add(new RowStyle(SizeType.Absolute, altura + 10));
      var lbl = Tema.Rotulo(rotulo);
      lbl.AutoSize = false;
      lbl.Dock = DockStyle.Fill;
      lbl.TextAlign = ContentAlignment.MiddleLeft;
      grade.Controls.Add(lbl);
      campo.Dock = DockStyle.Fill;
      grade.Controls.Add(campo);
    }

    Tema.EstilizarCampo(_url);
    Tema.EstilizarCampo(_token);
    Tema.EstilizarCampo(_headers);
    Tema.EstilizarCampo(_timeout);
    Linha("URL", _url);
    Linha("Token", _token);
    Linha("Headers extras", _headers, 90);
    Linha("Timeout (s)", _timeout);
    Linha("", _verificarSsl);

    var rodapeTeste = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true, Padding = new Padding(8, 4, 8, 0) };
    _testar.Margin = new Padding(0, 0, 12, 8);
    _resultadoTeste.Margin = new Padding(0, 8, 0, 0);
    rodapeTeste.Controls.Add(_testar);
    rodapeTeste.Controls.Add(_resultadoTeste);

    _avisoJira.Dock = DockStyle.Top;
    _avisoJira.Padding = new Padding(8);

    var painel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
    painel.Controls.Add(rodapeTeste);
    painel.Controls.Add(grade);
    painel.Controls.Add(_avisoJira);
    return painel;
  }

  private void CarregarJira()
  {
    _url.Text = _jira.Url;
    _headers.Text = string.Join(Environment.NewLine, _jira.HeadersExtras.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(h => h.Trim()));
    _verificarSsl.Checked = _jira.VerificarSsl;
    _timeout.Value = Math.Clamp(_jira.TimeoutSegundos, (int)_timeout.Minimum, (int)_timeout.Maximum);
    _token.PlaceholderText = _jira.Token() != null ? "(token salvo, digite para trocar)" : "";
    _tokenAlterado = false;

    _avisoJira.Text = _jira.VersaoMaisNova
      ? "A configuração do Jira foi criada por uma versão mais nova do apps-cm e não pode ser alterada por esta versão."
      : !_jira.Configurada && string.IsNullOrWhiteSpace(_jira.Url)
        ? "Jira desligado: informe a URL e o token."
        : !string.IsNullOrEmpty(_jira.TokenProtegido) && _jira.Token() == null
          ? "Token não configurado: o token salvo não pôde ser lido neste usuário/máquina. Digite-o novamente."
          : "";
    _avisoJira.Visible = _avisoJira.Text.Length > 0;
  }

  /// <summary>Monta uma conexão a partir dos campos da tela (sem gravar). Retorna null e avisa se algo for inválido.</summary>
  private ConexaoJira? LerCamposJira()
  {
    var url = _url.Text.Trim().TrimEnd('/');
    if (url.Length > 0 && !ConexaoJira.UrlValida(url))
    {
      MessageBox.Show(this, "Informe uma URL completa, começando com http:// ou https://.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
      return null;
    }

    var conexao = new ConexaoJira
    {
      Url = url,
      TokenProtegido = _jira.TokenProtegido,
      HeadersExtras = string.Join(";", _headers.Lines.Select(l => l.Trim()).Where(l => l.Length > 0)),
      VerificarSsl = _verificarSsl.Checked,
      TimeoutSegundos = (int)_timeout.Value,
    };
    if (_tokenAlterado && _token.Text.Length > 0) conexao.DefinirToken(_token.Text.Trim());
    return conexao;
  }

  private void SalvarJira()
  {
    if (LerCamposJira() is not { } conexao) return;
    try
    {
      conexao.Salvar();
    }
    catch (ConfiguracaoCompartilhadaException ex)
    {
      MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
      return;
    }
    _jira.Url = conexao.Url;
    _jira.TokenProtegido = conexao.TokenProtegido;
    _jira.HeadersExtras = conexao.HeadersExtras;
    _jira.VerificarSsl = conexao.VerificarSsl;
    _jira.TimeoutSegundos = conexao.TimeoutSegundos;
    _token.Clear();
    CarregarJira();
    MessageBox.Show(this, "Configuração do Jira salva.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
  }

  private async Task TestarConexaoAsync()
  {
    if (LerCamposJira() is not { } conexao) return;
    if (!ConexaoJira.UrlValida(conexao.Url)) { MostrarResultado("Informe a URL do Jira.", Tema.Perigo); return; }
    var token = conexao.Token();
    if (token == null) { MostrarResultado("Token não configurado.", Tema.Perigo); return; }

    _testar.Enabled = false;
    MostrarResultado("Testando...", Tema.TextoSecundario);
    try
    {
      using var handler = new HttpClientHandler();
      if (!conexao.VerificarSsl) handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
      using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(conexao.TimeoutSegundos) };

      using var req = new HttpRequestMessage(HttpMethod.Get, $"{conexao.Url}/rest/api/2/myself");
      req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
      foreach (var (chave, valor) in conexao.Headers())
        req.Headers.TryAddWithoutValidation(chave, valor);

      using var resp = await http.SendAsync(req);
      var corpo = await resp.Content.ReadAsStringAsync();

      if (resp.IsSuccessStatusCode && !corpo.TrimStart().StartsWith('<'))
      {
        var nome = TentarLerNome(corpo);
        MostrarResultado(nome != null ? $"Conectado como {nome}." : "Conectado.", Tema.Sucesso);
      }
      else if (corpo.TrimStart().StartsWith('<'))
        MostrarResultado("O servidor respondeu com uma página HTML (provável bloqueio de firewall/WAF): confira a URL e os headers extras.", Tema.Perigo);
      else if (resp.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        MostrarResultado($"Credencial recusada ({(int)resp.StatusCode}): confira o token e as permissões.", Tema.Perigo);
      else if (resp.StatusCode == HttpStatusCode.NotFound)
        MostrarResultado("Endereço não encontrado (404): confira a URL do Jira.", Tema.Perigo);
      else
        MostrarResultado($"O Jira respondeu {(int)resp.StatusCode} {resp.ReasonPhrase}.", Tema.Perigo);
    }
    catch (TaskCanceledException)
    {
      MostrarResultado("Tempo esgotado ao conectar.", Tema.Perigo);
    }
    catch (HttpRequestException ex)
    {
      MostrarResultado($"Falha de conexão: {ex.Message}", Tema.Perigo);
    }
    finally
    {
      _testar.Enabled = true;
    }
  }

  private static string? TentarLerNome(string json)
  {
    try
    {
      using var doc = JsonDocument.Parse(json);
      return doc.RootElement.TryGetProperty("displayName", out var n) ? n.GetString() : null;
    }
    catch (JsonException)
    {
      return null;
    }
  }

  private void MostrarResultado(string texto, Color cor)
  {
    _resultadoTeste.ForeColor = cor;
    _resultadoTeste.Text = texto;
  }

  // ---------- Clientes e projetos ----------

  private Control ConstruirClientes()
  {
    var cartao = new Cartao { Dock = DockStyle.Fill };
    Font = Tema.Fonte;
    cartao.Controls.Add(_arvore);

    var botoes = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, WrapContents = true, Padding = new Padding(0, 10, 0, 0) };
    botoes.Controls.AddRange(new Control[] { _novoCliente, _novoProjeto, _renomear, _chaveJira, _alternarAtivo });

    var topo = new Panel { Dock = DockStyle.Top, Height = 30 };
    _mostrarInativos.Location = new Point(0, 4);
    topo.Controls.Add(_mostrarInativos);

    var painel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
    painel.Controls.Add(cartao);
    painel.Controls.Add(botoes);
    painel.Controls.Add(topo);
    painel.Controls.Add(_avisoCatalogo);
    return painel;
  }

  private void AoAlterarCatalogo(object? sender, EventArgs e)
  {
    if (IsHandleCreated && !IsDisposed) BeginInvoke(Recarregar);
  }

  private NoCatalogo? Selecao => _arvore.SelectedNode?.Tag as NoCatalogo;

  private void Recarregar(string? selecionarCliente = null, string? selecionarProjeto = null)
  {
    var anterior = selecionarCliente != null ? new NoCatalogo(selecionarCliente, selecionarProjeto) : Selecao;
    var catalogo = CatalogoClientes.Carregar();
    var inativos = _mostrarInativos.Checked;

    _avisoCatalogo.Text = catalogo.Aviso ?? "";
    _avisoCatalogo.Visible = catalogo.Aviso != null;

    _arvore.BeginUpdate();
    _arvore.Nodes.Clear();
    TreeNode? escolhido = null;
    foreach (var cliente in catalogo.Clientes.Where(c => inativos || c.Ativo).OrderBy(c => c.Nome, StringComparer.CurrentCultureIgnoreCase))
    {
      var no = CriarNo(cliente.Nome, cliente.CriadoPor, cliente.Ativo, new NoCatalogo(cliente.Id, null));
      _arvore.Nodes.Add(no);
      if (anterior is { ProjetoId: null } a && a.ClienteId == cliente.Id) escolhido = no;

      foreach (var projeto in cliente.Projetos.Where(p => inativos || p.Ativo).OrderBy(p => p.Nome, StringComparer.CurrentCultureIgnoreCase))
      {
        var texto = projeto.JiraProjetoChave is { Length: > 0 } ? $"{projeto.Nome}  [{projeto.JiraProjetoChave}]" : projeto.Nome;
        var filho = CriarNo(texto, projeto.CriadoPor, projeto.Ativo && cliente.Ativo, new NoCatalogo(cliente.Id, projeto.Id));
        no.Nodes.Add(filho);
        if (anterior is { ProjetoId: not null } b && b.ProjetoId == projeto.Id) escolhido = filho;
      }
      no.Expand();
    }
    _arvore.EndUpdate();
    if (escolhido != null) _arvore.SelectedNode = escolhido;
    AtualizarBotoes();
  }

  private static TreeNode CriarNo(string nome, string criadoPor, bool ativo, NoCatalogo tag)
  {
    var texto = criadoPor.Length > 0 ? $"{nome}   (criado por {criadoPor})" : nome;
    return new TreeNode(texto) { Tag = tag, ForeColor = ativo ? Tema.Texto : Tema.TextoSecundario };
  }

  private void AtualizarBotoes()
  {
    var sel = Selecao;
    _novoProjeto.Enabled = sel != null;
    _renomear.Enabled = sel != null;
    _alternarAtivo.Enabled = sel != null;
    _chaveJira.Enabled = sel?.ProjetoId != null;

    var ativo = true;
    if (sel != null)
    {
      var catalogo = CatalogoClientes.Carregar();
      var cliente = catalogo.ClientePorId(sel.ClienteId);
      ativo = sel.ProjetoId == null ? cliente?.Ativo ?? true : catalogo.ProjetoPorId(sel.ClienteId, sel.ProjetoId)?.Ativo ?? true;
    }
    _alternarAtivo.Text = ativo ? "Desativar" : "Reativar";
  }

  private void Executar(Action acao, string? clienteParaSelecionar = null, string? projetoParaSelecionar = null)
  {
    try
    {
      acao();
    }
    catch (ConfiguracaoCompartilhadaException ex)
    {
      MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }
    Recarregar(clienteParaSelecionar, projetoParaSelecionar);
  }

  private void NovoCliente()
  {
    var nome = EntradaTexto.Perguntar(this, "Novo cliente", "Nome do cliente:");
    if (nome == null) return;
    Cliente? criado = null;
    Executar(() => criado = CatalogoClientes.ObterOuCriarCliente(nome, Origem));
    if (criado != null) Recarregar(criado.Id);
  }

  private void NovoProjeto()
  {
    if (Selecao is not { } sel) return;
    var nome = EntradaTexto.Perguntar(this, "Novo projeto", "Nome do projeto:");
    if (nome == null) return;
    Projeto? criado = null;
    Executar(() => criado = CatalogoClientes.ObterOuCriarProjeto(sel.ClienteId, nome, Origem));
    if (criado != null) Recarregar(sel.ClienteId, criado.Id);
  }

  private void Renomear()
  {
    if (Selecao is not { } sel) return;
    var catalogo = CatalogoClientes.Carregar();
    var atual = sel.ProjetoId == null
      ? catalogo.ClientePorId(sel.ClienteId)?.Nome
      : catalogo.ProjetoPorId(sel.ClienteId, sel.ProjetoId)?.Nome;
    var nome = EntradaTexto.Perguntar(this, "Renomear", "Novo nome:", atual ?? "");
    if (nome == null) return;
    Executar(() => CatalogoClientes.Renomear(sel.ClienteId, sel.ProjetoId, nome), sel.ClienteId, sel.ProjetoId);
  }

  private void AlternarAtivo()
  {
    if (Selecao is not { } sel) return;
    var reativar = _alternarAtivo.Text == "Reativar";
    if (!reativar)
    {
      var alvo = sel.ProjetoId == null ? "o cliente" : "o projeto";
      if (MessageBox.Show(this, $"Desativar {alvo}? Ele some dos combos, mas o que já foi gravado nos apps continua valendo.",
            Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
    }
    Executar(() => CatalogoClientes.DefinirAtivo(sel.ClienteId, sel.ProjetoId, reativar), sel.ClienteId, sel.ProjetoId);
  }

  private void DefinirChaveJira()
  {
    if (Selecao is not { ProjetoId: not null } sel) return;
    var atual = CatalogoClientes.Carregar().ProjetoPorId(sel.ClienteId, sel.ProjetoId)?.JiraProjetoChave ?? "";
    var chave = EntradaTexto.Perguntar(this, "Chave do Jira", "Chave do projeto no Jira (vazio remove):", atual);
    if (chave == null) return;
    Executar(() => CatalogoClientes.DefinirChaveJira(sel.ClienteId, sel.ProjetoId, chave), sel.ClienteId, sel.ProjetoId);
  }
}

/// <summary>Diálogo de uma linha de texto, no padrão visual do tema.</summary>
internal static class EntradaTexto
{
  /// <summary>Retorna o texto digitado (aparado, pode ser vazio) ou null se cancelado.</summary>
  public static string? Perguntar(IWin32Window dono, string titulo, string mensagem, string valorInicial = "")
  {
    using var form = new Form { Text = titulo, ClientSize = new Size(420, 160) };
    var texto = new TextBox { Text = valorInicial, Dock = DockStyle.Top };
    Tema.EstilizarCampo(texto);
    var rotulo = Tema.Rotulo(mensagem);
    rotulo.Dock = DockStyle.Top;
    var ok = Tema.BotaoPrimario("OK");
    ok.DialogResult = DialogResult.OK;
    var cancelar = Tema.BotaoSecundario("Cancelar");
    cancelar.DialogResult = DialogResult.Cancel;

    var corpo = new Panel();
    corpo.Controls.Add(texto);
    corpo.Controls.Add(rotulo);
    Tema.Dialogo(form, corpo, ok, cancelar);
    form.Shown += (_, _) => texto.Focus();
    return form.ShowDialog(dono) == DialogResult.OK ? texto.Text.Trim() : null;
  }
}
