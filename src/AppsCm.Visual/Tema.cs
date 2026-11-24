using System.Runtime.InteropServices;

namespace AppsCm.Visual;

/// <summary>Tema visual do apps-cm (cores, fontes e estilos de controle), comum ao shell e a todos os apps.
/// As fontes são estáticas e compartilhadas: nunca chamar Dispose() nelas.</summary>
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

  // ---------- botões ----------

  public static Button BotaoPrimario(string texto) => ComoPrimario(new Button { Text = texto });
  public static Button BotaoSecundario(string texto) => ComoSecundario(new Button { Text = texto });
  public static Button BotaoPerigo(string texto) => ComoPerigo(new Button { Text = texto });

  /// <summary>Estiliza um botão existente (por exemplo, criado pelo Designer) como ação principal.</summary>
  /// <param name="tamanhoFixo">Para telas do Designer com posições absolutas: mantém o tamanho definido no Designer.</param>
  public static Button ComoPrimario(Button b, bool tamanhoFixo = false) => Estilizar(b, Primaria, Color.White, Primaria, PrimariaHover, tamanhoFixo);

  public static Button ComoSecundario(Button b, bool tamanhoFixo = false) => Estilizar(b, Superficie, Texto, Borda, Fundo, tamanhoFixo);

  public static Button ComoPerigo(Button b, bool tamanhoFixo = false) => Estilizar(b, Superficie, Perigo, Borda, PerigoSuave, tamanhoFixo);

  private static Button Estilizar(Button b, Color fundo, Color texto, Color borda, Color hover, bool tamanhoFixo = false)
  {
    var tamanho = b.Size;
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
    if (tamanhoFixo)
    {
      b.AutoSize = false;
      b.MinimumSize = Size.Empty;
      b.Padding = Padding.Empty;
      b.Margin = Padding.Empty;
      b.Size = tamanho;
    }

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

  // ---------- campos e rótulos ----------

  /// <summary>Campo de texto ou combo plano, com borda fina e fundo branco.</summary>
  public static void EstilizarCampo(Control campo)
  {
    campo.Font = Fonte;
    campo.ForeColor = Texto;
    switch (campo)
    {
      case TextBox t:
        t.BorderStyle = BorderStyle.FixedSingle;
        t.BackColor = t.ReadOnly ? Fundo : Superficie;
        break;
      case ComboBox c:
        c.FlatStyle = FlatStyle.Flat;
        c.BackColor = Superficie;
        break;
      case NumericUpDown n:
        n.BorderStyle = BorderStyle.FixedSingle;
        n.BackColor = Superficie;
        break;
    }
    campo.MinimumSize = new Size(0, 28);
  }

  /// <summary>Rótulo de campo de formulário, em cor secundária.</summary>
  public static Label Rotulo(string texto) => EstilizarRotulo(new Label { Text = texto }, TextoSecundario);

  public static Label RotuloSecundario(string texto) => EstilizarRotulo(new Label { Text = texto }, TextoSecundario);

  /// <summary>Estiliza um rótulo existente como rótulo de formulário.</summary>
  public static Label EstilizarRotulo(Label l, Color? cor = null)
  {
    l.Font = Fonte;
    l.ForeColor = cor ?? TextoSecundario;
    l.AutoSize = true;
    return l;
  }

  // ---------- abas, menus, grades ----------

  /// <summary>Abas planas, com sublinhado azul na aba ativa.</summary>
  public static void EstilizarAbas(TabControl abas)
  {
    abas.DrawMode = TabDrawMode.OwnerDrawFixed;
    abas.Font = Fonte;
    abas.Padding = new Point(18, 6);
    abas.SizeMode = TabSizeMode.Normal;
    abas.ItemSize = new Size(abas.ItemSize.Width, 32);
    foreach (TabPage p in abas.TabPages) p.BackColor = Fundo;
    abas.ControlAdded += (_, e) => { if (e.Control is TabPage p) p.BackColor = Fundo; };
    abas.DrawItem += (_, e) =>
    {
      if (e.Index < 0 || e.Index >= abas.TabPages.Count) return;
      var ativa = e.Index == abas.SelectedIndex;
      using (var fundo = new SolidBrush(Fundo)) e.Graphics.FillRectangle(fundo, e.Bounds);
      var texto = ativa ? Primaria : TextoSecundario;
      var pagina = abas.TabPages[e.Index];
      var area = e.Bounds;
      if (abas.ImageList is { } imagens && !string.IsNullOrEmpty(pagina.ImageKey) && imagens.Images.ContainsKey(pagina.ImageKey))
      {
        var img = imagens.Images[pagina.ImageKey];
        e.Graphics.DrawImage(img, area.Left + 8, area.Top + (area.Height - img.Height) / 2, img.Width, img.Height);
        area = new Rectangle(area.Left + 8 + img.Width, area.Top, area.Width - 8 - img.Width, area.Height);
      }
      TextRenderer.DrawText(e.Graphics, pagina.Text, ativa ? FonteSemibold : Fonte, area, texto,
        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
      if (ativa)
      {
        using var barra = new SolidBrush(Primaria);
        e.Graphics.FillRectangle(barra, e.Bounds.Left + 4, e.Bounds.Bottom - 3, e.Bounds.Width - 8, 3);
      }
    };
  }

  /// <summary>Menu ou barra de menus com a paleta do tema.</summary>
  public static void EstilizarMenu(ToolStrip menu)
  {
    menu.Font = Fonte;
    menu.Renderer = new ToolStripProfessionalRenderer(new PaletaMenu()) { RoundedEdges = false };
    menu.BackColor = Superficie;
    menu.ForeColor = Texto;
  }

  private sealed class PaletaMenu : ProfessionalColorTable
  {
    public PaletaMenu() { UseSystemColors = false; }
    public override Color ToolStripDropDownBackground => Superficie;
    public override Color ImageMarginGradientBegin => Superficie;
    public override Color ImageMarginGradientMiddle => Superficie;
    public override Color ImageMarginGradientEnd => Superficie;
    public override Color MenuBorder => Borda;
    public override Color MenuItemBorder => PrimariaSuave;
    public override Color MenuItemSelected => PrimariaSuave;
    public override Color MenuItemSelectedGradientBegin => PrimariaSuave;
    public override Color MenuItemSelectedGradientEnd => PrimariaSuave;
    public override Color MenuItemPressedGradientBegin => PrimariaSuave;
    public override Color MenuItemPressedGradientMiddle => PrimariaSuave;
    public override Color MenuItemPressedGradientEnd => PrimariaSuave;
    public override Color MenuStripGradientBegin => Superficie;
    public override Color MenuStripGradientEnd => Superficie;
    public override Color SeparatorDark => Borda;
    public override Color SeparatorLight => Superficie;
  }

  /// <summary>Grade com a mesma linguagem da lista: sem borda, linhas de 32 px, cabeçalho plano.</summary>
  public static void EstilizarGrade(DataGridView grade)
  {
    grade.BorderStyle = BorderStyle.None;
    grade.BackgroundColor = Superficie;
    grade.GridColor = Borda;
    grade.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
    grade.EnableHeadersVisualStyles = false;
    grade.RowHeadersVisible = false;
    grade.RowTemplate.Height = 32;
    grade.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
    grade.ColumnHeadersHeight = 32;
    grade.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
    grade.ColumnHeadersDefaultCellStyle.BackColor = Superficie;
    grade.ColumnHeadersDefaultCellStyle.ForeColor = TextoSecundario;
    grade.ColumnHeadersDefaultCellStyle.Font = FonteSemibold;
    grade.ColumnHeadersDefaultCellStyle.SelectionBackColor = Superficie;
    grade.ColumnHeadersDefaultCellStyle.SelectionForeColor = TextoSecundario;
    grade.DefaultCellStyle.BackColor = Superficie;
    grade.DefaultCellStyle.ForeColor = Texto;
    grade.DefaultCellStyle.Font = Fonte;
    grade.DefaultCellStyle.SelectionBackColor = PrimariaSuave;
    grade.DefaultCellStyle.SelectionForeColor = Texto;
  }

  // ---------- lista ----------

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

  // ---------- diálogos ----------

  /// <summary>Monta uma tela modal: corpo com padding 20/16 e rodapé com Cancelar e a ação principal à direita.
  /// Configura AcceptButton/CancelButton. O conteúdo deve ter sido criado sem pai.</summary>
  public static void Dialogo(Form form, Control corpo, Button principal, Button? cancelar = null)
  {
    Aplicar(form);
    form.FormBorderStyle = FormBorderStyle.FixedDialog;
    form.MaximizeBox = false;
    form.MinimizeBox = false;
    form.ShowInTaskbar = false;
    form.StartPosition = FormStartPosition.CenterParent;

    var area = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20, 16, 20, 16) };
    corpo.Dock = DockStyle.Fill;
    area.Controls.Add(corpo);

    var rodape = new FaixaComBorda { BordaEmCima = true, Dock = DockStyle.Bottom, Height = 62, Padding = new Padding(20, 14, 20, 0) };
    var botoes = new FlowLayoutPanel
    {
      Dock = DockStyle.Right,
      AutoSize = true,
      WrapContents = false,
      FlowDirection = FlowDirection.RightToLeft,
      BackColor = Superficie
    };
    principal.Margin = Padding.Empty;
    botoes.Controls.Add(principal);
    if (cancelar != null) botoes.Controls.Add(cancelar);
    rodape.Controls.Add(botoes);

    form.Controls.Add(area);
    form.Controls.Add(rodape);
    form.AcceptButton = principal;
    if (cancelar != null) form.CancelButton = cancelar;
  }

  [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
  private static extern int SetWindowTheme(IntPtr hwnd, string? appName, string? idList);
}
