using System.Runtime.InteropServices;

namespace TestEvidence.UI.Visual;

/// <summary>Protótipo do tema visual do apps-cm (cores, fontes e estilos de controle).
/// Validado aqui na tela principal; depois vai para um projeto comum de UI, usado por todos os apps.</summary>
public static class Tema
{
  // paleta: azul da logo como destaque, cinzas neutros, superfícies brancas sobre fundo claro
  public static readonly Color Fundo = Color.FromArgb(245, 247, 250);
  public static readonly Color Superficie = Color.White;
  public static readonly Color Borda = Color.FromArgb(222, 226, 231);
  public static readonly Color Texto = Color.FromArgb(31, 41, 51);
  public static readonly Color TextoSecundario = Color.FromArgb(97, 110, 124);
  public static readonly Color Primaria = Color.FromArgb(17, 112, 198);
  public static readonly Color PrimariaHover = Color.FromArgb(14, 95, 168);
  public static readonly Color PrimariaSuave = Color.FromArgb(232, 242, 252);
  public static readonly Color Sucesso = Color.FromArgb(46, 125, 50);
  public static readonly Color Perigo = Color.FromArgb(198, 40, 40);
  public static readonly Color PerigoSuave = Color.FromArgb(253, 236, 236);
  public static readonly Color Alerta = Color.FromArgb(166, 100, 0);
  public static readonly Color AlertaFundo = Color.FromArgb(255, 248, 225);
  public static readonly Color AlertaBorda = Color.FromArgb(245, 180, 0);

  public static readonly Font Fonte = new("Segoe UI", 9.75f);
  public static readonly Font FonteSemibold = new("Segoe UI Semibold", 9.75f);
  public static readonly Font FonteTitulo = new("Segoe UI Semibold", 15f);

  /// <summary>Fonte, cor de texto e fundo da tela; os filhos herdam a fonte.</summary>
  public static void Aplicar(Control raiz)
  {
    raiz.Font = Fonte;
    raiz.ForeColor = Texto;
    raiz.BackColor = Fundo;
  }

  public static Button BotaoPrimario(string texto) =>
    Estilizar(new Button { Text = texto }, Primaria, Color.White, Primaria, PrimariaHover);

  public static Button BotaoSecundario(string texto) =>
    Estilizar(new Button { Text = texto }, Superficie, Texto, Borda, Fundo);

  public static Button BotaoPerigo(string texto) =>
    Estilizar(new Button { Text = texto }, Superficie, Perigo, Borda, PerigoSuave);

  private static Button Estilizar(Button b, Color fundo, Color texto, Color borda, Color hover)
  {
    b.FlatStyle = FlatStyle.Flat;
    b.UseVisualStyleBackColor = false;
    b.FlatAppearance.BorderSize = 1;
    b.FlatAppearance.MouseOverBackColor = hover;
    b.FlatAppearance.MouseDownBackColor = hover;
    b.Font = FonteSemibold;
    b.AutoSize = true;
    b.AutoSizeMode = AutoSizeMode.GrowOnly;
    b.MinimumSize = new Size(0, 34);
    b.Padding = new Padding(10, 0, 10, 0);
    b.Margin = new Padding(0, 0, 8, 0);
    b.Cursor = Cursors.Hand;

    // o WinForms desenha botão Flat desabilitado com o texto cinza sobre a cor original
    // (cinza sobre azul fica ilegível): no desabilitado, todos viram neutros
    void Cores()
    {
      b.BackColor = b.Enabled ? fundo : Fundo;
      b.ForeColor = b.Enabled ? texto : TextoSecundario;
      b.FlatAppearance.BorderColor = b.Enabled ? borda : Borda;
      b.Cursor = b.Enabled ? Cursors.Hand : Cursors.Default;
    }
    Cores();
    b.EnabledChanged += (_, _) => Cores();
    return b;
  }

  /// <summary>Lista em detalhes com linhas mais altas, cabeçalho plano e seleção no estilo do Explorer.</summary>
  public static void EstilizarLista(ListView lista)
  {
    lista.BorderStyle = BorderStyle.None;
    lista.BackColor = Superficie;
    lista.ForeColor = Texto;
    // a altura da linha do ListView segue a altura da SmallImageList
    lista.SmallImageList = new ImageList { ImageSize = new Size(1, 32) };

    lista.OwnerDraw = true;
    lista.DrawColumnHeader += (_, e) =>
    {
      using var fundo = new SolidBrush(Superficie);
      e.Graphics.FillRectangle(fundo, e.Bounds);
      using var linha = new Pen(Borda);
      e.Graphics.DrawLine(linha, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
      var area = new Rectangle(e.Bounds.X + 6, e.Bounds.Y, e.Bounds.Width - 10, e.Bounds.Height);
      TextRenderer.DrawText(e.Graphics, e.Header?.Text?.ToUpperInvariant(), FonteSemibold, area, TextoSecundario,
        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    };
    lista.DrawItem += (_, e) => e.DrawDefault = true;
    lista.DrawSubItem += (_, e) => e.DrawDefault = true;

    // última coluna ocupa o resto da largura, para o cabeçalho não terminar num pedaço cinza nativo
    void Esticar() { if (lista.Columns.Count > 0) lista.Columns[^1].Width = -2; }
    lista.Resize += (_, _) => Esticar();
    lista.HandleCreated += (_, _) =>
    {
      SetWindowTheme(lista.Handle, "Explorer", null);
      Esticar();
    };
  }

  [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
  private static extern int SetWindowTheme(IntPtr hwnd, string? appName, string? idList);
}

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
