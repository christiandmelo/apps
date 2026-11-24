using System.Diagnostics;
using AppsCm.Visual;
using TestEvidence.Models;
using TestEvidence.Services;

namespace TestEvidence.UI;

public sealed class RevisaoForm : Form
{
  private readonly SessaoEvidencia _sessao;
  private readonly SessaoRepository _repo;
  private readonly ConfiguracaoApp _config;
  private readonly Form? _shell;

  private readonly CabecalhoSessaoPanel _cabecalho = new(comEtapas: true);
  private readonly RadioButton _rbAprovado = new() { Text = "Aprovado", AutoSize = true };
  private readonly RadioButton _rbReprovado = new() { Text = "Reprovado", AutoSize = true };
  private readonly RadioButton _rbRessalvas = new() { Text = "Com ressalvas", AutoSize = true };
  private readonly TextBox _observacoes = new() { Multiline = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, AcceptsReturn = true };
  private readonly FlowLayoutPanel _lista = new()
  {
    Dock = DockStyle.Fill,
    FlowDirection = FlowDirection.TopDown,
    WrapContents = false,
    AutoScroll = true,
    Padding = new Padding(0, 8, 20, 8)
  };
  private readonly List<Image> _miniaturas = new();
  private bool _reconstruindo;

  public RevisaoForm(SessaoEvidencia sessao, SessaoRepository repo, ConfiguracaoApp config, Form? shell)
  {
    _sessao = sessao;
    _repo = repo;
    _config = config;
    _shell = shell;

    Text = $"Revisão — {sessao.Tarefa}";
    Icon = IconeApp.Obter();
    StartPosition = FormStartPosition.CenterScreen;
    ClientSize = new Size(1100, 720);
    MinimumSize = new Size(900, 520);
    Tema.Aplicar(this);

    _cabecalho.Carregar(sessao, config.AmbientesUsados);
    _rbAprovado.Checked = sessao.Resultado == ResultadoTeste.Aprovado;
    _rbReprovado.Checked = sessao.Resultado == ResultadoTeste.Reprovado;
    _rbRessalvas.Checked = sessao.Resultado == ResultadoTeste.ComRessalvas;
    _observacoes.Text = sessao.Observacoes;

    var resultado = new GroupBox { Text = "Resultado", Dock = DockStyle.Top, Height = 56 };
    var fluxo = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(6, 2, 0, 0) };
    fluxo.Controls.AddRange(new Control[] { _rbAprovado, _rbReprovado, _rbRessalvas });
    resultado.Controls.Add(fluxo);

    var obs = new GroupBox { Text = "Observações", Dock = DockStyle.Fill, Padding = new Padding(6) };
    obs.Controls.Add(_observacoes);

    var esquerda = new Panel { Dock = DockStyle.Left, Width = 520, Padding = new Padding(20, 16, 8, 16) };
    var topoEsquerda = new Panel { Dock = DockStyle.Top, Height = 440 };
    topoEsquerda.Controls.Add(_cabecalho);
    esquerda.Controls.Add(obs);
    esquerda.Controls.Add(resultado);
    esquerda.Controls.Add(topoEsquerda);

    var btnVoltar = Tema.BotaoSecundario("Voltar a evidenciar");
    var btnSalvar = Tema.BotaoSecundario("Salvar");
    var btnGerar = Tema.BotaoPrimario("Gerar documento");
    btnGerar.Margin = Padding.Empty;
    btnVoltar.Click += (_, _) => VoltarAEvidenciar();
    btnSalvar.Click += (_, _) => { SalvarTudo(); MessageBox.Show(this, "Sessão salva.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information); };
    btnGerar.Click += (_, _) => GerarDocumento();
    var rodape = new FaixaComBorda { BordaEmCima = true, Dock = DockStyle.Bottom, Height = 62, Padding = new Padding(20, 14, 20, 0) };
    var botoesDireita = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, WrapContents = false, BackColor = Tema.Superficie };
    botoesDireita.Controls.AddRange(new Control[] { btnSalvar, btnGerar });
    var botoesEsquerda = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, BackColor = Tema.Superficie };
    botoesEsquerda.Controls.Add(btnVoltar);
    rodape.Controls.Add(botoesEsquerda);
    rodape.Controls.Add(botoesDireita);

    Controls.Add(_lista);
    Controls.Add(esquerda);
    Controls.Add(rodape);

    _lista.SizeChanged += (_, _) => AjustarLarguraItens();
    FormClosing += (_, _) => SalvarTudo(silencioso: true);
    FormClosed += (_, _) =>
    {
      LimparMiniaturas();
      ControladorSessao.NotificarEstado();
    };

    Reconstruir();
  }

  private ResultadoTeste ResultadoSelecionado =>
    _rbAprovado.Checked ? ResultadoTeste.Aprovado :
    _rbReprovado.Checked ? ResultadoTeste.Reprovado :
    _rbRessalvas.Checked ? ResultadoTeste.ComRessalvas : ResultadoTeste.Pendente;

  private bool AplicarCampos()
  {
    if (_cabecalho.Tarefa.Length == 0)
    {
      MessageBox.Show(this, "A tarefa é obrigatória.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
      return false;
    }
    _cabecalho.Aplicar(_sessao);
    _sessao.Resultado = ResultadoSelecionado;
    _sessao.Observacoes = _observacoes.Text.Trim();
    return true;
  }

  private bool SalvarTudo(bool silencioso = false)
  {
    if (!AplicarCampos() && !silencioso) return false;
    try
    {
      _repo.Salvar(_sessao);
      return true;
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
      MessageBox.Show(this, $"Não foi possível salvar a sessão: {ex.Message}", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
      return false;
    }
  }

  // ---------- lista de evidências ----------

  private void LimparMiniaturas()
  {
    foreach (var m in _miniaturas) m.Dispose();
    _miniaturas.Clear();
  }

  private void Reconstruir()
  {
    _reconstruindo = true;
    _lista.SuspendLayout();
    var antigos = _lista.Controls.Cast<Control>().ToList();
    _lista.Controls.Clear();
    foreach (var c in antigos) c.Dispose();
    LimparMiniaturas();

    var etapas = _sessao.Etapas.Concat(_sessao.Evidencias.Select(e => e.Etapa).Except(_sessao.Etapas)).ToList();
    foreach (var etapa in etapas)
    {
      var itens = _sessao.Evidencias.Where(e => e.Etapa == etapa).OrderBy(e => e.Ordem).ToList();
      if (itens.Count == 0) continue;
      _lista.Controls.Add(new Label
      {
        Text = etapa,
        Font = Tema.FonteSemibold,
        AutoSize = true,
        Margin = new Padding(4, 10, 0, 4)
      });
      foreach (var ev in itens)
        _lista.Controls.Add(CriarItem(ev, itens, etapas));
    }
    if (_sessao.Evidencias.Count == 0)
      _lista.Controls.Add(new Label { Text = "Nenhuma evidência capturada.", AutoSize = true, Margin = new Padding(8) });

    _lista.ResumeLayout();
    _reconstruindo = false;
    AjustarLarguraItens();
  }

  private void AjustarLarguraItens()
  {
    if (_reconstruindo) return;
    var largura = Math.Max(300, _lista.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 8);
    foreach (Control c in _lista.Controls)
      if (c is Panel) c.Width = largura;
  }

  private Control CriarItem(Evidencia ev, List<Evidencia> grupo, List<string> etapas)
  {
    var painel = new Cartao { Height = 128, Margin = new Padding(0, 2, 0, 8) };

    var figura = new PictureBox
    {
      Left = 8, Top = 8, Width = 176, Height = 112,
      SizeMode = PictureBoxSizeMode.Zoom,
      BorderStyle = BorderStyle.None,
      BackColor = Tema.Fundo,
      Cursor = Cursors.Hand
    };
    var arquivo = _repo.CaminhoImagem(_sessao, ev);
    var miniatura = CarregarMiniatura(arquivo, 176, 112);
    if (miniatura != null) { figura.Image = miniatura; _miniaturas.Add(miniatura); }
    figura.DoubleClick += (_, _) => AbrirArquivo(arquivo);

    var cboEtapa = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Left = 196, Top = 8, Width = 150 };
    Tema.EstilizarCampo(cboEtapa);
    cboEtapa.Items.AddRange(etapas.Cast<object>().ToArray());
    cboEtapa.SelectedItem = ev.Etapa;
    cboEtapa.SelectedIndexChanged += (_, _) =>
    {
      if (_reconstruindo || cboEtapa.SelectedItem is not string nova || nova == ev.Etapa) return;
      ev.Etapa = nova;
      ev.Ordem = _sessao.Evidencias.Max(e => e.Ordem) + 1;
      SalvarTudo(silencioso: true);
      BeginInvoke(Reconstruir);
    };

    var lblHora = Tema.Rotulo($"{ev.CapturadoEm:dd/MM HH:mm:ss}");
    lblHora.Left = 356; lblHora.Top = 13;

    var idx = grupo.IndexOf(ev);
    var btnSobe = PequenoSecundario("↑", 196, 42); btnSobe.Enabled = idx > 0;
    var btnDesce = PequenoSecundario("↓", 236, 42); btnDesce.Enabled = idx < grupo.Count - 1;
    var btnExcluir = Tema.BotaoPerigo("Excluir");
    btnExcluir.AutoSize = false; btnExcluir.Size = new Size(76, 30); btnExcluir.MinimumSize = Size.Empty;
    btnExcluir.Location = new Point(280, 42);
    btnSobe.Click += (_, _) => Mover(grupo, idx, -1);
    btnDesce.Click += (_, _) => Mover(grupo, idx, +1);
    btnExcluir.Click += (_, _) => Excluir(ev);

    var txtLegenda = new TextBox
    {
      Left = 196, Top = 78, Height = 42,
      Multiline = true, ScrollBars = ScrollBars.Vertical, AcceptsReturn = true,
      Text = ev.Legenda,
      Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
    };
    txtLegenda.TextChanged += (_, _) => ev.Legenda = txtLegenda.Text.Trim();
    Tema.EstilizarCampo(txtLegenda);
    txtLegenda.MinimumSize = new Size(0, 42);
    painel.Resize += (_, _) => txtLegenda.Width = Math.Max(100, painel.ClientSize.Width - 196 - 8);

    painel.Controls.AddRange(new Control[] { figura, cboEtapa, lblHora, btnSobe, btnDesce, btnExcluir, txtLegenda });
    painel.Width = 700;
    txtLegenda.Width = 500;
    return painel;
  }

  private static Button PequenoSecundario(string texto, int x, int y)
  {
    var b = Tema.BotaoSecundario(texto);
    b.AutoSize = false;
    b.MinimumSize = Size.Empty;
    b.Size = new Size(32, 30);
    b.Padding = Padding.Empty;
    b.Location = new Point(x, y);
    return b;
  }

  private void Mover(List<Evidencia> grupo, int idx, int delta)
  {
    var a = grupo[idx];
    var b = grupo[idx + delta];
    (a.Ordem, b.Ordem) = (b.Ordem, a.Ordem);
    SalvarTudo(silencioso: true);
    BeginInvoke(Reconstruir);
  }

  private void Excluir(Evidencia ev)
  {
    if (MessageBox.Show(this, "Excluir esta evidência e o print correspondente?", Text,
          MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
    try
    {
      _repo.ExcluirEvidencia(_sessao, ev);
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
      MessageBox.Show(this, $"Não foi possível excluir: {ex.Message}", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }
    BeginInvoke(Reconstruir);
  }

  private static Bitmap? CarregarMiniatura(string arquivo, int maxL, int maxA)
  {
    try
    {
      using var fs = File.OpenRead(arquivo);
      using var origem = new Bitmap(fs);
      var escala = Math.Min(1.0, Math.Min((double)maxL / origem.Width, (double)maxA / origem.Height));
      var l = Math.Max(1, (int)(origem.Width * escala));
      var a = Math.Max(1, (int)(origem.Height * escala));
      var bmp = new Bitmap(l, a);
      using var g = Graphics.FromImage(bmp);
      g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
      g.DrawImage(origem, 0, 0, l, a);
      return bmp;
    }
    catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException)
    {
      return null;
    }
  }

  private static void AbrirArquivo(string caminho)
  {
    try { Process.Start(new ProcessStartInfo(caminho) { UseShellExecute = true }); }
    catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or FileNotFoundException) { }
  }

  // ---------- ações ----------

  private void VoltarAEvidenciar()
  {
    if (!SalvarTudo()) return;
    var ctl = ControladorSessao.Iniciar(_sessao, _config, _repo, _shell);
    if (ctl != null) Close();
  }

  private void GerarDocumento()
  {
    if (!SalvarTudo()) return;
    GeradorDocumento.Gerar(this, _sessao, _repo, _config);
  }
}
