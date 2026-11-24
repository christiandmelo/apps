using AppsCm.Compartilhado;
using AppsCm.Visual;
using TestEvidence.Models;

namespace TestEvidence.UI;

/// <summary>Campos do cabeçalho da sessão, compartilhados entre Nova sessão e Revisão.</summary>
public sealed class CabecalhoSessaoPanel : TableLayoutPanel
{
  private readonly TextBox _tarefa = new() { Dock = DockStyle.Fill };
  private readonly ComboBox _cliente = ComboCatalogo();
  private readonly ComboBox _projeto = ComboCatalogo();
  private readonly Label _dicaCliente = Dica();
  private readonly Label _dicaProjeto = Dica();
  private readonly TextBox _titulo = new() { Dock = DockStyle.Fill };
  private readonly TextBox _requisito = Multilinha(60);
  private readonly TextBox _cenario = Multilinha(60);
  private readonly ComboBox _ambiente = new() { Dock = DockStyle.Fill };
  private readonly TextBox _testador = new() { Dock = DockStyle.Fill };
  private readonly TextBox? _etapas;
  private CatalogoClientes _catalogo = CatalogoClientes.Carregar();

  public CabecalhoSessaoPanel(bool comEtapas)
  {
    ColumnCount = 2;
    Dock = DockStyle.Top;
    AutoSize = true;
    Padding = Padding.Empty;
    ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
    ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

    Linha("Tarefa *", _tarefa, 26);
    Linha("Cliente", ComDica(_cliente, _dicaCliente), 26, estilizar: false);
    Linha("Projeto", ComDica(_projeto, _dicaProjeto), 26, estilizar: false);
    Linha("Título", _titulo, 26);
    Linha("Requisito", _requisito, 66);
    Linha("Cenário / Pré-condições", _cenario, 66);
    Linha("Ambiente", _ambiente, 26);
    Linha("Testador", _testador, 26);
    if (comEtapas)
    {
      _etapas = Multilinha(66);
      Linha("Etapas (uma por linha)", _etapas, 70);
    }

    _cliente.TextChanged += (_, _) => AoMudarCliente();
    _projeto.TextChanged += (_, _) => AtualizarDicas();
    CatalogoClientes.Alterado += AoAlterarCatalogo;
  }

  protected override void Dispose(bool disposing)
  {
    if (disposing) CatalogoClientes.Alterado -= AoAlterarCatalogo;
    base.Dispose(disposing);
  }

  private static ComboBox ComboCatalogo()
  {
    var c = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDown };
    c.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
    c.AutoCompleteSource = AutoCompleteSource.ListItems;
    Tema.EstilizarCampo(c);
    return c;
  }

  private static Label Dica() => new()
  {
    Text = "(novo, será criado)",
    AutoSize = true,
    Dock = DockStyle.Right,
    TextAlign = ContentAlignment.MiddleLeft,
    ForeColor = Tema.TextoSecundario,
    Padding = new Padding(8, 0, 0, 0),
    Visible = false
  };

  private static Panel ComDica(ComboBox combo, Label dica)
  {
    var painel = new Panel { Dock = DockStyle.Fill, Height = 28 };
    painel.Controls.Add(combo);
    painel.Controls.Add(dica);
    return painel;
  }

  private static TextBox Multilinha(int altura) => new()
  {
    Multiline = true,
    AcceptsReturn = true,
    ScrollBars = ScrollBars.Vertical,
    Dock = DockStyle.Fill,
    Height = altura
  };

  private void Linha(string rotulo, Control campo, int altura, bool estilizar = true)
  {
    RowStyles.Add(new RowStyle(SizeType.Absolute, altura + 10));
    var lbl = Tema.Rotulo(rotulo);
    lbl.AutoSize = false;
    lbl.Dock = DockStyle.Fill;
    lbl.TextAlign = ContentAlignment.MiddleLeft;
    if (estilizar) Tema.EstilizarCampo(campo);
    Controls.Add(lbl);
    Controls.Add(campo);
  }

  public string Tarefa => _tarefa.Text.Trim();

  // ---------- cliente e projeto (catálogo compartilhado) ----------

  private void AoAlterarCatalogo(object? sender, EventArgs e)
  {
    if (IsDisposed || !IsHandleCreated) return;
    BeginInvoke(() =>
    {
      _catalogo = CatalogoClientes.Carregar();
      PreencherClientes();
      PreencherProjetos();
      AtualizarDicas();
    });
  }

  private void PreencherClientes()
  {
    var texto = _cliente.Text;
    _cliente.Items.Clear();
    _cliente.Items.AddRange(_catalogo.Clientes.Where(c => c.Ativo).Select(c => c.Nome).OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase).Cast<object>().ToArray());
    _cliente.Text = texto;
  }

  private Cliente? ClienteDigitado()
  {
    var texto = _cliente.Text.Trim();
    return texto.Length == 0 ? null : _catalogo.Clientes.FirstOrDefault(c => VinculoCatalogo.NomeIgual(c.Nome, texto));
  }

  private void PreencherProjetos()
  {
    var texto = _projeto.Text;
    _projeto.Items.Clear();
    if (ClienteDigitado() is { } cliente)
      _projeto.Items.AddRange(cliente.Projetos.Where(p => p.Ativo).Select(p => p.Nome).OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase).Cast<object>().ToArray());
    _projeto.Text = texto;
  }

  private void AoMudarCliente()
  {
    _projeto.Enabled = _cliente.Text.Trim().Length > 0;
    PreencherProjetos();
    AtualizarDicas();
  }

  private void AtualizarDicas()
  {
    var cliente = ClienteDigitado();
    var textoCliente = _cliente.Text.Trim();
    var textoProjeto = _projeto.Text.Trim();
    _dicaCliente.Visible = textoCliente.Length > 0 && cliente == null;
    _dicaProjeto.Visible = textoCliente.Length > 0 && textoProjeto.Length > 0 &&
      (cliente == null || !cliente.Projetos.Any(p => VinculoCatalogo.NomeIgual(p.Nome, textoProjeto)));
  }

  public void Carregar(SessaoEvidencia s, IEnumerable<string> ambientes)
  {
    _catalogo = CatalogoClientes.Carregar();
    _tarefa.Text = s.Tarefa;
    var (cliente, projeto) = VinculoCatalogo.Nomes(s, _catalogo);
    PreencherClientes();
    _cliente.Text = cliente;
    PreencherProjetos();
    _projeto.Text = projeto;
    AoMudarCliente();
    _titulo.Text = s.Titulo;
    _requisito.Text = s.Requisito;
    _cenario.Text = s.Cenario;
    _ambiente.Items.Clear();
    _ambiente.Items.AddRange(ambientes.Cast<object>().ToArray());
    _ambiente.Text = s.Ambiente;
    _testador.Text = s.Testador;
    if (_etapas != null) _etapas.Text = string.Join(Environment.NewLine, s.Etapas);
  }

  /// <summary>Copia os campos para a sessão; cliente e projeto novos são criados no catálogo agora (nunca ao sair do campo).</summary>
  public void Aplicar(SessaoEvidencia s)
  {
    s.Tarefa = _tarefa.Text.Trim();
    if (!VinculoCatalogo.Vincular(s, _cliente.Text, _projeto.Text))
    {
      MessageBox.Show(FindForm(),
        "Não foi possível gravar o cliente/projeto no catálogo compartilhado. A evidência será salva só com os nomes e o vínculo será refeito na próxima gravação.",
        "Catálogo de clientes", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }
    s.Titulo = _titulo.Text.Trim();
    s.Requisito = _requisito.Text.Trim();
    s.Cenario = _cenario.Text.Trim();
    s.Ambiente = _ambiente.Text.Trim();
    s.Testador = _testador.Text.Trim();
    if (_etapas != null)
    {
      s.Etapas = _etapas.Lines.Select(l => l.Trim()).Where(l => l.Length > 0).Distinct().ToList();
      if (s.Etapas.Count == 0) s.Etapas.Add("Evidências");
      if (!s.Etapas.Contains(s.EtapaAtual)) s.EtapaAtual = s.Etapas[0];
    }
  }
}
