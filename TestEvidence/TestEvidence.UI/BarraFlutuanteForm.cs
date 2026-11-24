using TestEvidence.Models;
using TestEvidence.Services;
using TestEvidence.Services.Nativo;

namespace TestEvidence.UI;

public sealed class BarraFlutuanteForm : Form
{
  private const int IdAtalhoEvidenciar = 1;
  private const int IdAtalhoRepetir = 2;
  private const string NovaEtapa = "Nova etapa…";

  private readonly ControladorSessao _ctl;
  private readonly AtalhoGlobal _atalhos = new();
  private readonly FlowLayoutPanel _painel = new()
  {
    Dock = DockStyle.Fill,
    FlowDirection = FlowDirection.LeftToRight,
    WrapContents = false,
    Padding = new Padding(4, 6, 4, 4)
  };
  private readonly Label _lblTarefa = new() { AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = Color.White, Margin = new Padding(4, 7, 8, 0), Cursor = Cursors.SizeAll };
  private readonly ComboBox _cboEtapa = new() { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, Width = 190, BackColor = Color.FromArgb(55, 55, 55), ForeColor = Color.White, Margin = new Padding(0, 3, 6, 0) };
  private readonly Label _lblContador = new() { AutoSize = true, ForeColor = Color.White, Margin = new Padding(6, 7, 6, 0) };
  private readonly Label _lblAviso = new() { AutoSize = true, ForeColor = Color.LightSalmon, Margin = new Padding(4, 7, 4, 0), Visible = false };
  private readonly Button _btnPausar = Botao("Pausar");
  private readonly System.Windows.Forms.Timer _timerFlash = new() { Interval = 300 };
  private readonly Color _corNormal = Color.FromArgb(28, 28, 28);
  private static readonly Color CorBorda = Color.FromArgb(110, 110, 110);
  private const double OpacidadeNormal = 0.92;
  private const double OpacidadePausada = 0.5;
  private bool _fechandoDefinitivo;
  private bool _atualizandoEtapas;

  public BarraFlutuanteForm(ControladorSessao ctl)
  {
    _ctl = ctl;

    Text = $"Evidência {ctl.Sessao.Tarefa}";
    FormBorderStyle = FormBorderStyle.None;
    TopMost = true;
    ShowInTaskbar = true;
    StartPosition = FormStartPosition.Manual;
    BackColor = _corNormal;
    Opacity = OpacidadeNormal;
    Height = 44;
    Width = 560;
    Icon = IconeApp.Obter();

    var btnEvidenciar = Botao("Evidenciar");
    var btnRepetir = Botao("Repetir");
    var btnTela = Botao("Tela");
    var btnFinalizar = Botao("Finalizar");
    btnEvidenciar.Click += async (_, _) => await _ctl.EvidenciarAsync(TipoCaptura.Regiao);
    btnRepetir.Click += async (_, _) => await _ctl.EvidenciarAsync(TipoCaptura.RepetirRegiao);
    btnTela.Click += async (_, _) => await _ctl.EvidenciarAsync(TipoCaptura.TelaInteira);
    _btnPausar.Click += (_, _) => AlternarPausa();
    btnFinalizar.Click += (_, _) => _ctl.Finalizar();

    _lblTarefa.Text = $"≡ {ctl.Sessao.Tarefa}";
    _cboEtapa.SelectedIndexChanged += AoTrocarEtapa;

    _painel.Controls.AddRange(new Control[]
    {
      _lblTarefa, _cboEtapa, btnEvidenciar, btnRepetir, btnTela, _lblContador, _btnPausar, btnFinalizar, _lblAviso
    });
    Controls.Add(_painel);
    Padding = new Padding(1);

    foreach (var c in new Control[] { this, _painel, _lblTarefa, _lblContador, _lblAviso })
      c.MouseDown += AoArrastar;

    _timerFlash.Tick += (_, _) => { _timerFlash.Stop(); BackColor = _corNormal; };
    _ctl.SessaoAlterada += AoSessaoAlterada;

    CarregarEtapas();
    AtualizarContador();
    DefinirPosicaoInicial();
  }

  private static Button Botao(string texto) => new()
  {
    Text = texto,
    AutoSize = true,
    Height = 28,
    Margin = new Padding(2, 2, 2, 0),
    FlatStyle = FlatStyle.Flat,
    BackColor = Color.FromArgb(55, 55, 55),
    ForeColor = Color.White,
    FlatAppearance = { BorderColor = Color.FromArgb(120, 120, 120), BorderSize = 1 }
  };

  protected override void OnPaint(PaintEventArgs e)
  {
    base.OnPaint(e);
    using var caneta = new Pen(CorBorda);
    e.Graphics.DrawRectangle(caneta, 0, 0, Width - 1, Height - 1);
  }

  private void DefinirPosicaoInicial()
  {
    Point? pos = null;
    if (_ctl.Config.PosicaoBarra is { } p)
    {
      var rect = new Rectangle(p.X, p.Y, Width, Height);
      if (Screen.AllScreens.Any(s => s.WorkingArea.IntersectsWith(rect)))
        pos = new Point(p.X, p.Y);
    }
    if (pos == null)
    {
      var area = Screen.PrimaryScreen!.WorkingArea;
      pos = new Point(area.Left + (area.Width - Width) / 2, area.Top + 8);
    }
    Location = pos.Value;
  }

  protected override void OnShown(EventArgs e)
  {
    base.OnShown(e);
    _atalhos.Vincular(Handle);
    RegistrarAtalhos();
  }

  private void RegistrarAtalhos()
  {
    var falhas = new List<string>();
    if (!_atalhos.Registrar(IdAtalhoEvidenciar, _ctl.Config.AtalhoEvidenciar)) falhas.Add(_ctl.Config.AtalhoEvidenciar);
    if (!_atalhos.Registrar(IdAtalhoRepetir, _ctl.Config.AtalhoRepetirRegiao)) falhas.Add(_ctl.Config.AtalhoRepetirRegiao);

    _lblAviso.Visible = falhas.Count > 0;
    if (falhas.Count > 0)
      _lblAviso.Text = $"Atalho {string.Join(", ", falhas)} em uso por outro app — altere em Configurações";
    AjustarLargura();
  }

  private void AjustarLargura()
  {
    _painel.PerformLayout();
    var largura = _painel.Controls.Cast<Control>().Where(c => c.Visible).Sum(c => c.Width + c.Margin.Horizontal) + 16;
    Width = Math.Max(420, largura);
  }

  protected override void WndProc(ref Message m)
  {
    if (m.Msg == User32.WM_HOTKEY)
    {
      var id = m.WParam.ToInt32();
      if (id == IdAtalhoEvidenciar) _ = _ctl.EvidenciarAsync(TipoCaptura.Regiao);
      else if (id == IdAtalhoRepetir) _ = _ctl.EvidenciarAsync(TipoCaptura.RepetirRegiao);
      return;
    }
    base.WndProc(ref m);
  }

  private void AoArrastar(object? sender, MouseEventArgs e)
  {
    if (e.Button != MouseButtons.Left) return;
    User32.ReleaseCapture();
    User32.SendMessage(Handle, User32.WM_NCLBUTTONDOWN, new IntPtr(User32.HTCAPTION), IntPtr.Zero);
  }

  private void AoSessaoAlterada(object? sender, EventArgs e)
  {
    if (IsDisposed) return;
    if (InvokeRequired) { BeginInvoke(() => AoSessaoAlterada(sender, e)); return; }
    CarregarEtapas();
    AtualizarContador();
  }

  private void AtualizarContador() => _lblContador.Text = $"{_ctl.Sessao.Evidencias.Count} prints";

  private void CarregarEtapas()
  {
    _atualizandoEtapas = true;
    _cboEtapa.Items.Clear();
    _cboEtapa.Items.AddRange(_ctl.Sessao.Etapas.Cast<object>().ToArray());
    _cboEtapa.Items.Add(NovaEtapa);
    _cboEtapa.SelectedItem = _ctl.Sessao.EtapaAtual;
    _atualizandoEtapas = false;
  }

  private void AoTrocarEtapa(object? sender, EventArgs e)
  {
    if (_atualizandoEtapas || _cboEtapa.SelectedItem is not string etapa) return;
    if (etapa == NovaEtapa)
    {
      using var prompt = new PromptForm("Nova etapa", "Nome da etapa:");
      var nome = prompt.ShowDialog() == DialogResult.OK ? prompt.Valor : "";
      if (nome.Length == 0)
      {
        CarregarEtapas();
        return;
      }
      etapa = nome;
    }
    _ctl.TrocarEtapa(etapa);
  }

  private void AlternarPausa()
  {
    if (_ctl.Pausada)
    {
      _ctl.Retomar();
      RegistrarAtalhos();
      Opacity = OpacidadeNormal;
      _btnPausar.Text = "Pausar";
    }
    else
    {
      _ctl.Pausar();
      _atalhos.DesregistrarTodos();
      Opacity = OpacidadePausada;
      _btnPausar.Text = "Retomar";
    }
    AjustarLargura();
  }

  public void EsconderParaCaptura() => Opacity = 0;

  public void MostrarAposCaptura() => Opacity = _ctl.Pausada ? OpacidadePausada : OpacidadeNormal;

  public void Flash()
  {
    BackColor = Color.FromArgb(30, 100, 55);
    _timerFlash.Stop();
    _timerFlash.Start();
  }

  public void FecharDefinitivo()
  {
    _fechandoDefinitivo = true;
    Close();
  }

  protected override void OnFormClosing(FormClosingEventArgs e)
  {
    if (!_fechandoDefinitivo && e.CloseReason == CloseReason.UserClosing)
    {
      // Alt+F4 = pausar: a sessão continua em disco e pode ser retomada pelo histórico
      e.Cancel = true;
      _ctl.Encerrar();
      return;
    }
    base.OnFormClosing(e);
  }

  protected override void OnFormClosed(FormClosedEventArgs e)
  {
    _ctl.SessaoAlterada -= AoSessaoAlterada;
    _atalhos.Dispose();
    _timerFlash.Dispose();
    _ctl.SalvarPosicaoBarra(Location);
    base.OnFormClosed(e);
  }
}
