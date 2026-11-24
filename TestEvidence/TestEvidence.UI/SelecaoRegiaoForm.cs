using System.Drawing.Drawing2D;
using TestEvidence.Models;
using TestEvidence.Services;
using TestEvidence.Services.Nativo;

namespace TestEvidence.UI;

/// <summary>
/// Overlay "congelar e recortar": mostra o bitmap da tela virtual já capturado e deixa o usuário
/// arrastar um retângulo. Deve ser criado com o contexto de DPI per-monitor v2 ativo na thread.
/// </summary>
public sealed class SelecaoRegiaoForm : Form
{
  private readonly Bitmap _tela;
  private readonly RegiaoTela _telaVirtual;
  private readonly RegiaoTela? _ultimaRegiao;
  private Point _inicio;
  private Point _atual;
  private bool _arrastando;

  public RegiaoTela? Resultado { get; private set; }
  public TipoCaptura TipoResultado { get; private set; } = TipoCaptura.Regiao;

  public SelecaoRegiaoForm(Bitmap tela, RegiaoTela telaVirtual, RegiaoTela? ultimaRegiao)
  {
    _tela = tela;
    _telaVirtual = telaVirtual;
    _ultimaRegiao = ultimaRegiao;

    AutoScaleMode = AutoScaleMode.None;
    FormBorderStyle = FormBorderStyle.None;
    StartPosition = FormStartPosition.Manual;
    Bounds = new Rectangle(telaVirtual.X, telaVirtual.Y, telaVirtual.Largura, telaVirtual.Altura);
    TopMost = true;
    ShowInTaskbar = false;
    DoubleBuffered = true;
    Cursor = Cursors.Cross;
    KeyPreview = true;
  }

  protected override void OnShown(EventArgs e)
  {
    base.OnShown(e);
    // garante os bounds em pixels físicos, independente de escalas aplicadas pelo WinForms
    User32.SetWindowPos(Handle, User32.HWND_TOPMOST, _telaVirtual.X, _telaVirtual.Y,
      _telaVirtual.Largura, _telaVirtual.Altura, User32.SWP_SHOWWINDOW);
    Activate();
  }

  private Rectangle Selecao => new(
    Math.Min(_inicio.X, _atual.X), Math.Min(_inicio.Y, _atual.Y),
    Math.Abs(_inicio.X - _atual.X), Math.Abs(_inicio.Y - _atual.Y));

  protected override void OnPaint(PaintEventArgs e)
  {
    var g = e.Graphics;
    g.DrawImage(_tela, 0, 0, _tela.Width, _tela.Height);

    using var escuro = new SolidBrush(Color.FromArgb(120, 0, 0, 0));
    if (!_arrastando)
    {
      g.FillRectangle(escuro, ClientRectangle);
      return;
    }

    var s = Selecao;
    g.FillRectangle(escuro, 0, 0, Width, s.Top);
    g.FillRectangle(escuro, 0, s.Bottom, Width, Math.Max(0, Height - s.Bottom));
    g.FillRectangle(escuro, 0, s.Top, s.Left, s.Height);
    g.FillRectangle(escuro, s.Right, s.Top, Math.Max(0, Width - s.Right), s.Height);

    using var caneta = new Pen(Color.DeepSkyBlue, 2);
    g.DrawRectangle(caneta, s);

    var texto = $"{s.Width} × {s.Height}";
    using var fonte = new Font("Segoe UI", 10f, FontStyle.Bold);
    var tam = g.MeasureString(texto, fonte);
    var x = Math.Min(_atual.X + 14, Width - (int)tam.Width - 8);
    var y = Math.Min(_atual.Y + 14, Height - (int)tam.Height - 8);
    using var fundo = new SolidBrush(Color.FromArgb(200, 20, 20, 20));
    g.FillRectangle(fundo, x - 4, y - 2, tam.Width + 8, tam.Height + 4);
    g.DrawString(texto, fonte, Brushes.White, x, y);
  }

  protected override void OnMouseDown(MouseEventArgs e)
  {
    if (e.Button == MouseButtons.Right)
    {
      DialogResult = DialogResult.Cancel;
      return;
    }
    if (e.Button != MouseButtons.Left) return;
    _inicio = _atual = e.Location;
    _arrastando = true;
    Invalidate();
  }

  protected override void OnMouseMove(MouseEventArgs e)
  {
    if (!_arrastando) return;
    _atual = e.Location;
    Invalidate();
  }

  protected override void OnMouseUp(MouseEventArgs e)
  {
    if (!_arrastando || e.Button != MouseButtons.Left) return;
    _arrastando = false;
    _atual = e.Location;
    var s = Selecao;
    if (s.Width < 5 || s.Height < 5)
    {
      Invalidate();
      return;
    }
    Resultado = new RegiaoTela(_telaVirtual.X + s.X, _telaVirtual.Y + s.Y, s.Width, s.Height);
    TipoResultado = TipoCaptura.Regiao;
    DialogResult = DialogResult.OK;
  }

  protected override void OnKeyDown(KeyEventArgs e)
  {
    switch (e.KeyCode)
    {
      case Keys.Escape:
        DialogResult = DialogResult.Cancel;
        break;
      case Keys.Enter when !_arrastando:
        Resultado = CapturaService.MonitorDoCursor();
        TipoResultado = TipoCaptura.TelaInteira;
        DialogResult = DialogResult.OK;
        break;
      case Keys.Space when !_arrastando && _ultimaRegiao != null:
        Resultado = _ultimaRegiao;
        TipoResultado = TipoCaptura.RepetirRegiao;
        DialogResult = DialogResult.OK;
        break;
    }
  }
}
