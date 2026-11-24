using System.Diagnostics;
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
    AutoScroll = true
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

    var esquerda = new Panel { Dock = DockStyle.Left, Width = 440 };
    var topoEsquerda = new Panel { Dock = DockStyle.Top, Height = 410 };
    topoEsquerda.Controls.Add(_cabecalho);
    esquerda.Controls.Add(obs);
    esquerda.Controls.Add(resultado);
    esquerda.Controls.Add(topoEsquerda);

    var btnVoltar = new Button { Text = "Voltar a evidenciar", Width = 140, Left = 8, Top = 10 };
    var btnSalvar = new Button { Text = "Salvar", Width = 90, Left = 800, Top = 10, Anchor = AnchorStyles.Top | AnchorStyles.Right };
    var btnGerar = new Button { Text = "Gerar documento", Width = 140, Left = 900, Top = 10, Anchor = AnchorStyles.Top | AnchorStyles.Right };
    btnVoltar.Click += (_, _) => VoltarAEvidenciar();
    btnSalvar.Click += (_, _) => { SalvarTudo(); MessageBox.Show(this, "Sessão salva.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information); };
    btnGerar.Click += (_, _) => GerarDocumento();
    var rodape = new Panel { Dock = DockStyle.Bottom, Height = 48 };
    rodape.Controls.AddRange(new Control[] { btnVoltar, btnSalvar, btnGerar });

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
        Font = new Font("Segoe UI", 11f, FontStyle.Bold),
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
    var painel = new Panel { Height = 124, BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(4, 2, 4, 4) };

    var figura = new PictureBox
    {
      Left = 4, Top = 4, Width = 176, Height = 112,
      SizeMode = PictureBoxSizeMode.Zoom,
      BorderStyle = BorderStyle.FixedSingle,
      Cursor = Cursors.Hand
    };
    var arquivo = _repo.CaminhoImagem(_sessao, ev);
    var miniatura = CarregarMiniatura(arquivo, 176, 112);
    if (miniatura != null) { figura.Image = miniatura; _miniaturas.Add(miniatura); }
    figura.DoubleClick += (_, _) => AbrirArquivo(arquivo);

    var cboEtapa = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Left = 188, Top = 6, Width = 150 };
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

    var lblHora = new Label { Left = 346, Top = 9, AutoSize = true, Text = $"{ev.CapturadoEm:dd/MM HH:mm:ss}" };

    var idx = grupo.IndexOf(ev);
    var btnSobe = new Button { Text = "↑", Width = 30, Left = 188, Top = 36, Enabled = idx > 0 };
    var btnDesce = new Button { Text = "↓", Width = 30, Left = 222, Top = 36, Enabled = idx < grupo.Count - 1 };
    var btnExcluir = new Button { Text = "Excluir", Width = 70, Left = 258, Top = 36 };
    btnSobe.Click += (_, _) => Mover(grupo, idx, -1);
    btnDesce.Click += (_, _) => Mover(grupo, idx, +1);
    btnExcluir.Click += (_, _) => Excluir(ev);

    var txtLegenda = new TextBox
    {
      Left = 188, Top = 68, Height = 48,
      Multiline = true, ScrollBars = ScrollBars.Vertical, AcceptsReturn = true,
      Text = ev.Legenda,
      Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
    };
    txtLegenda.TextChanged += (_, _) => ev.Legenda = txtLegenda.Text.Trim();
    painel.Resize += (_, _) => txtLegenda.Width = Math.Max(100, painel.ClientSize.Width - 196);

    painel.Controls.AddRange(new Control[] { figura, cboEtapa, lblHora, btnSobe, btnDesce, btnExcluir, txtLegenda });
    painel.Width = 700;
    txtLegenda.Width = 500;
    return painel;
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
