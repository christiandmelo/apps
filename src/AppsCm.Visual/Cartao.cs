namespace AppsCm.Visual;

/// <summary>Superfície branca com borda fina, usada para agrupar conteúdo.</summary>
public sealed class Cartao : Panel
{
  public Cartao()
  {
    BackColor = Tema.Superficie;
    Padding = new Padding(1);   // deixa a borda visível em volta do filho com Dock = Fill
    DoubleBuffered = true;
    ResizeRedraw = true;
  }

  protected override void OnPaint(PaintEventArgs e)
  {
    base.OnPaint(e);
    using var borda = new Pen(Tema.Borda);
    e.Graphics.DrawRectangle(borda, 0, 0, Width - 1, Height - 1);
  }
}
