namespace AppsCm.Visual;

/// <summary>Aviso em destaque: fundo amarelo-claro com uma barra de cor à esquerda.</summary>
public sealed class Aviso : Panel
{
  public Aviso()
  {
    BackColor = Tema.AlertaFundo;
    Padding = new Padding(16, 8, 10, 8);
    DoubleBuffered = true;
    ResizeRedraw = true;
  }

  protected override void OnPaint(PaintEventArgs e)
  {
    base.OnPaint(e);
    using var barra = new SolidBrush(Tema.AlertaBorda);
    e.Graphics.FillRectangle(barra, 0, 0, 4, Height);
    using var borda = new Pen(Color.FromArgb(250, 224, 160));
    e.Graphics.DrawRectangle(borda, 0, 0, Width - 1, Height - 1);
  }
}
