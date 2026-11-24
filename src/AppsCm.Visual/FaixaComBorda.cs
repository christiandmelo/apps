namespace AppsCm.Visual;

/// <summary>Painel com uma linha fina em cima ou embaixo (cabeçalho e rodapé das telas).</summary>
public sealed class FaixaComBorda : Panel
{
  public bool BordaEmCima { get; init; }

  public FaixaComBorda()
  {
    BackColor = Tema.Superficie;
    DoubleBuffered = true;
    ResizeRedraw = true;
  }

  protected override void OnPaint(PaintEventArgs e)
  {
    base.OnPaint(e);
    using var borda = new Pen(Tema.Borda);
    var y = BordaEmCima ? 0 : Height - 1;
    e.Graphics.DrawLine(borda, 0, y, Width, y);
  }
}
