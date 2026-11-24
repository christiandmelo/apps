namespace TestEvidence.UI;

public sealed class LegendaForm : Form
{
  private readonly TextBox _txtLegenda = new()
  {
    Multiline = true,
    AcceptsReturn = false,
    ScrollBars = ScrollBars.Vertical,
    Dock = DockStyle.Fill
  };
  private readonly ComboBox _cboEtapa = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Top };

  public string Legenda => _txtLegenda.Text.Trim();
  public string Etapa => _cboEtapa.Text;
  public bool Descartado { get; private set; }

  public LegendaForm(Bitmap print, IEnumerable<string> etapas, string etapaAtual)
  {
    Text = "Legenda da evidência";
    FormBorderStyle = FormBorderStyle.FixedToolWindow;
    StartPosition = FormStartPosition.Manual;
    TopMost = true;
    ShowInTaskbar = false;
    ClientSize = new Size(560, 170);

    var miniatura = new PictureBox
    {
      Dock = DockStyle.Left,
      Width = 220,
      SizeMode = PictureBoxSizeMode.Zoom,
      BorderStyle = BorderStyle.FixedSingle,
      Image = CriarMiniatura(print, 220, 150)
    };

    _cboEtapa.Items.AddRange(etapas.Cast<object>().ToArray());
    _cboEtapa.Text = etapaAtual;
    if (_cboEtapa.SelectedIndex < 0 && _cboEtapa.Items.Count > 0) _cboEtapa.SelectedIndex = 0;

    var dica = new Label
    {
      Dock = DockStyle.Bottom,
      Height = 40,
      ForeColor = SystemColors.GrayText,
      Text = "Enter salva • Shift+Enter quebra linha • Esc salva sem legenda"
    };
    var descartar = new Button { Text = "Descartar", Dock = DockStyle.Left, Width = 110 };
    descartar.Click += (_, _) => { Descartado = true; DialogResult = DialogResult.Cancel; };
    var salvar = new Button { Text = "Salvar", Dock = DockStyle.Right, Width = 110 };
    salvar.Click += (_, _) => DialogResult = DialogResult.OK;
    var botoes = new Panel { Dock = DockStyle.Bottom, Height = 30 };
    botoes.Controls.Add(salvar);
    botoes.Controls.Add(descartar);

    var direita = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8, 4, 4, 4) };
    direita.Controls.Add(_txtLegenda);
    direita.Controls.Add(dica);
    direita.Controls.Add(botoes);
    direita.Controls.Add(_cboEtapa);

    Controls.Add(direita);
    Controls.Add(miniatura);

    _txtLegenda.KeyDown += (_, e) =>
    {
      if (e.KeyCode == Keys.Enter)
      {
        e.SuppressKeyPress = true;
        if (e.Shift) _txtLegenda.SelectedText = Environment.NewLine;
        else DialogResult = DialogResult.OK;
      }
      else if (e.KeyCode == Keys.Escape)
      {
        e.SuppressKeyPress = true;
        _txtLegenda.Clear();
        DialogResult = DialogResult.OK;
      }
    };
    Shown += (_, _) => { Activate(); _txtLegenda.Focus(); };
    FormClosed += (_, _) => miniatura.Image?.Dispose();
  }

  public void PosicionarPerto(Rectangle referencia)
  {
    var area = Screen.FromRectangle(referencia).WorkingArea;
    var x = Math.Clamp(referencia.Left, area.Left, Math.Max(area.Left, area.Right - Width));
    var y = referencia.Bottom + 8;
    if (y + Height > area.Bottom) y = Math.Max(area.Top, referencia.Top - Height - 8);
    Location = new Point(x, y);
  }

  private static Bitmap CriarMiniatura(Bitmap origem, int maxL, int maxA)
  {
    var escala = Math.Min(1.0, Math.Min((double)maxL / origem.Width, (double)maxA / origem.Height));
    var l = Math.Max(1, (int)(origem.Width * escala));
    var a = Math.Max(1, (int)(origem.Height * escala));
    var bmp = new Bitmap(l, a);
    using var g = Graphics.FromImage(bmp);
    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
    g.DrawImage(origem, 0, 0, l, a);
    return bmp;
  }
}
