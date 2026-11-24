using DocumentFormat.OpenXml;
using AppsCm.Compartilhado;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;
using TestEvidence.Models;
using Color = DocumentFormat.OpenXml.Wordprocessing.Color;

namespace TestEvidence.Services;

public sealed class DocumentoWordService
{
  private const long EmuPorCm = 360000;
  private const long EmuPorPixel = 9525;
  private const long LarguraMaximaEmu = 17 * EmuPorCm;
  private const long AlturaMaximaEmu = 22 * EmuPorCm;

  private uint _proximoIdImagem = 1;

  public void Gerar(SessaoEvidencia sessao, string pastaSessao, string caminhoDocx)
  {
    _proximoIdImagem = 1;
    try
    {
      using var doc = WordprocessingDocument.Create(caminhoDocx, WordprocessingDocumentType.Document);
      var main = doc.AddMainDocumentPart();
      main.Document = new Document();
      var body = main.Document.AppendChild(new Body());

      CriarEstilos(main);
      var settings = main.AddNewPart<DocumentSettingsPart>();
      settings.Settings = new Settings(new UpdateFieldsOnOpen { Val = true });
      settings.Settings.Save();
      LayoutPadraoDocumento(main, body, sessao, pastaSessao);
      body.AppendChild(CriarSecao(main));
      main.Document.Save();
    }
    catch (IOException ex)
    {
      throw new IOException("Não foi possível gravar o documento. Feche o documento no Word e tente de novo.", ex);
    }
  }

  // ---------- layout padrão (a fase de template customizado troca só esta parte) ----------

  private void LayoutPadraoDocumento(MainDocumentPart main, Body body, SessaoEvidencia sessao, string pastaSessao)
  {
    body.AppendChild(Paragrafo("EVIDÊNCIA DE TESTE", "Title"));
    body.AppendChild(CriarTabelaCabecalho(sessao));

    var etapasComEvidencia = EtapasEmOrdem(sessao)
      .Where(et => sessao.Evidencias.Any(e => e.Etapa == et))
      .ToList();
    var temObservacoes = !string.IsNullOrWhiteSpace(sessao.Observacoes);
    var titulos = etapasComEvidencia.Select((et, i) => $"{i + 1}. {et}").ToList();
    if (temObservacoes) titulos.Add("Observações");
    if (titulos.Count > 0) AdicionarIndice(body, titulos);

    var numero = 0;
    var indiceEtapa = 0;
    foreach (var etapa in EtapasEmOrdem(sessao))
    {
      var itens = sessao.Evidencias.Where(e => e.Etapa == etapa).OrderBy(e => e.Ordem).ToList();
      if (itens.Count == 0) continue;
      indiceEtapa++;
      body.AppendChild(ParagrafoComMarcador($"{indiceEtapa}. {etapa}", "Heading1", NomeMarcador(indiceEtapa - 1)));

      foreach (var ev in itens)
      {
        numero++;
        body.AppendChild(Paragrafo($"Evidência {numero} — {ev.CapturadoEm:HH:mm:ss}", null, negrito: true, tamanhoMeiosPonto: 20, keepNext: true));
        if (!string.IsNullOrWhiteSpace(ev.Legenda))
        {
          foreach (var linha in ev.Legenda.Replace("\r", "").Split('\n'))
            body.AppendChild(Paragrafo(linha, null, keepNext: true));
        }

        var arquivo = Path.Combine(pastaSessao, ev.ArquivoImagem);
        if (File.Exists(arquivo))
          body.AppendChild(CriarParagrafoImagem(main, arquivo));
        else
          body.AppendChild(Paragrafo("[imagem não encontrada]", null, cor: "C00000"));
      }
    }

    if (!string.IsNullOrWhiteSpace(sessao.Observacoes))
    {
      body.AppendChild(ParagrafoComMarcador("Observações", "Heading1", NomeMarcador(etapasComEvidencia.Count)));
      foreach (var linha in sessao.Observacoes.Replace("\r", "").Split('\n'))
        body.AppendChild(Paragrafo(linha, null));
    }
  }

  private static IEnumerable<string> EtapasEmOrdem(SessaoEvidencia sessao)
  {
    foreach (var e in sessao.Etapas) yield return e;
    // etapas removidas da lista mas ainda com evidências
    foreach (var e in sessao.Evidencias.Select(x => x.Etapa).Distinct().Except(sessao.Etapas))
      yield return e;
  }

  private static Table CriarTabelaCabecalho(SessaoEvidencia sessao)
  {
    var fim = sessao.Fim ?? DateTime.Now;
    var periodo = sessao.Inicio.Date == fim.Date
      ? $"{sessao.Inicio:dd/MM/yyyy HH:mm} – {fim:HH:mm}"
      : $"{sessao.Inicio:dd/MM/yyyy HH:mm} – {fim:dd/MM/yyyy HH:mm}";

    var (textoResultado, corResultado) = sessao.Resultado switch
    {
      ResultadoTeste.Aprovado => ("APROVADO", "2E7D32"),
      ResultadoTeste.Reprovado => ("REPROVADO", "C62828"),
      ResultadoTeste.ComRessalvas => ("COM RESSALVAS", "EF6C00"),
      _ => ("PENDENTE", "757575")
    };

    var borda = new EnumValue<BorderValues>(BorderValues.Single);
    var tabela = new Table(
      new TableProperties(
        new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
        new TableBorders(
          new TopBorder { Val = borda, Size = 4, Color = "BFBFBF" },
          new LeftBorder { Val = borda, Size = 4, Color = "BFBFBF" },
          new BottomBorder { Val = borda, Size = 4, Color = "BFBFBF" },
          new RightBorder { Val = borda, Size = 4, Color = "BFBFBF" },
          new InsideHorizontalBorder { Val = borda, Size = 4, Color = "BFBFBF" },
          new InsideVerticalBorder { Val = borda, Size = 4, Color = "BFBFBF" }),
        new TableCellMarginDefault(
          new TopMargin { Width = "40", Type = TableWidthUnitValues.Dxa },
          new BottomMargin { Width = "40", Type = TableWidthUnitValues.Dxa })),
      new TableGrid(new GridColumn { Width = "1900" }, new GridColumn { Width = "7700" }));

    void Linha(string rotulo, string valor, bool resultado = false)
    {
      var celulaValor = new TableCell(new TableCellProperties(new TableCellWidth { Width = "7700", Type = TableWidthUnitValues.Dxa }));
      var linhas = (string.IsNullOrEmpty(valor) ? "-" : valor).Replace("\r", "").Split('\n');
      foreach (var l in linhas)
        celulaValor.AppendChild(Paragrafo(l, null, negrito: resultado, cor: resultado ? corResultado : null, espacoApos: 0));

      var celulaRotulo = new TableCell(
        new TableCellProperties(
          new TableCellWidth { Width = "1900", Type = TableWidthUnitValues.Dxa },
          new Shading { Val = ShadingPatternValues.Clear, Fill = "F2F2F2", Color = "auto" }),
        Paragrafo(rotulo, null, negrito: true, espacoApos: 0));

      tabela.AppendChild(new TableRow(celulaRotulo, celulaValor));
    }

    Linha("Tarefa", sessao.Tarefa);
    var (cliente, projeto) = VinculoCatalogo.Nomes(sessao, CatalogoClientes.Carregar());
    if (cliente.Length > 0) Linha("Cliente", cliente);
    if (projeto.Length > 0) Linha("Projeto", projeto);
    Linha("Título", sessao.Titulo);
    Linha("Requisito", sessao.Requisito);
    Linha("Cenário", sessao.Cenario);
    Linha("Ambiente", sessao.Ambiente);
    Linha("Testador", sessao.Testador);
    Linha("Data/Hora", periodo);
    Linha("Resultado", textoResultado, resultado: true);
    return tabela;
  }

  private Paragraph CriarParagrafoImagem(MainDocumentPart main, string arquivo)
  {
    int larguraPx, alturaPx;
    using (var img = System.Drawing.Image.FromFile(arquivo))
    {
      larguraPx = img.Width;
      alturaPx = img.Height;
    }

    double largura = larguraPx * EmuPorPixel;
    double altura = alturaPx * EmuPorPixel;
    if (largura > LarguraMaximaEmu)
    {
      altura *= LarguraMaximaEmu / largura;
      largura = LarguraMaximaEmu;
    }
    if (altura > AlturaMaximaEmu)
    {
      largura *= AlturaMaximaEmu / altura;
      altura = AlturaMaximaEmu;
    }
    long cx = Math.Max(1, (long)largura), cy = Math.Max(1, (long)altura);

    var imagePart = main.AddImagePart(ImagePartType.Png);
    using (var fs = File.OpenRead(arquivo))
      imagePart.FeedData(fs);
    var relId = main.GetIdOfPart(imagePart);
    var id = _proximoIdImagem++;
    var nome = Path.GetFileName(arquivo);

    var drawing = new Drawing(
      new DW.Inline(
        new DW.Extent { Cx = cx, Cy = cy },
        new DW.EffectExtent { LeftEdge = 0, TopEdge = 0, RightEdge = 0, BottomEdge = 0 },
        new DW.DocProperties { Id = id, Name = $"Evidencia {id}", Description = nome },
        new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = true }),
        new A.Graphic(
          new A.GraphicData(
            new PIC.Picture(
              new PIC.NonVisualPictureProperties(
                new PIC.NonVisualDrawingProperties { Id = 0U, Name = nome },
                new PIC.NonVisualPictureDrawingProperties()),
              new PIC.BlipFill(
                new A.Blip { Embed = relId },
                new A.Stretch(new A.FillRectangle())),
              new PIC.ShapeProperties(
                new A.Transform2D(new A.Offset { X = 0L, Y = 0L }, new A.Extents { Cx = cx, Cy = cy }),
                new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle },
                new A.Outline(new A.SolidFill(new A.RgbColorModelHex { Val = "BFBFBF" })) { Width = 9525 })))
          { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }))
      { DistanceFromTop = 0U, DistanceFromBottom = 0U, DistanceFromLeft = 0U, DistanceFromRight = 0U });

    return new Paragraph(
      new ParagraphProperties(new SpacingBetweenLines { After = "240" }),
      new Run(drawing));
  }

  // ---------- partes comuns ----------

  private static string NomeMarcador(int indice) => $"_Toc{100000 + indice}";

  private static Paragraph ParagrafoComMarcador(string texto, string estilo, string marcador)
  {
    var p = Paragrafo(texto, estilo);
    var id = marcador.GetHashCode() & 0x7FFFFFFF;
    p.InsertAfter(new BookmarkStart { Id = id.ToString(), Name = marcador }, p.GetFirstChild<ParagraphProperties>());
    p.AppendChild(new BookmarkEnd { Id = id.ToString() });
    return p;
  }

  // Campo TOC real do Word (clicável). As entradas já vêm preenchidas e os números de página
  // são atualizados pelo Word ao abrir o documento (updateFields).
  private static void AdicionarIndice(Body body, List<string> titulos)
  {
    const string corIndice = "1F3864";
    body.AppendChild(Paragrafo("Índice", null, negrito: true, tamanhoMeiosPonto: 28, keepNext: true, cor: corIndice));

    Run Fld(FieldCharValues tipo) => new(new FieldChar { FieldCharType = tipo });

    for (var i = 0; i < titulos.Count; i++)
    {
      var marcador = NomeMarcador(i);
      var pPr = new ParagraphProperties(
        new Tabs(new TabStop { Val = TabStopValues.Right, Leader = TabStopLeaderCharValues.Dot, Position = 9638 }),
        new SpacingBetweenLines { After = "60" });
      var p = new Paragraph(pPr);

      if (i == 0)
      {
        p.AppendChild(Fld(FieldCharValues.Begin));
        p.AppendChild(new Run(new FieldCode(" TOC \\o \"1-1\" \\h \\z \\u ") { Space = SpaceProcessingModeValues.Preserve }));
        p.AppendChild(Fld(FieldCharValues.Separate));
      }

      p.AppendChild(new Hyperlink(
        new Run(new Text(titulos[i]) { Space = SpaceProcessingModeValues.Preserve }),
        new Run(new TabChar()),
        Fld(FieldCharValues.Begin),
        new Run(new FieldCode($" PAGEREF {marcador} \\h ") { Space = SpaceProcessingModeValues.Preserve }),
        Fld(FieldCharValues.Separate),
        new Run(new Text("1")),
        Fld(FieldCharValues.End))
      { Anchor = marcador, History = true });

      if (i == titulos.Count - 1) p.AppendChild(Fld(FieldCharValues.End));
      body.AppendChild(p);
    }
  }

  private static Paragraph Paragrafo(string texto, string? estilo, bool negrito = false, int? tamanhoMeiosPonto = null,
    bool keepNext = false, string? cor = null, int espacoApos = 120)
  {
    var pPr = new ParagraphProperties();
    if (estilo != null) pPr.AppendChild(new ParagraphStyleId { Val = estilo });
    if (keepNext) pPr.AppendChild(new KeepNext());
    if (estilo == null) pPr.AppendChild(new SpacingBetweenLines { After = espacoApos.ToString() });

    var rPr = new RunProperties();
    if (negrito) rPr.AppendChild(new Bold());
    if (cor != null) rPr.AppendChild(new Color { Val = cor });
    if (tamanhoMeiosPonto != null) rPr.AppendChild(new FontSize { Val = tamanhoMeiosPonto.Value.ToString() });

    var run = new Run();
    if (rPr.HasChildren) run.AppendChild(rPr);
    run.AppendChild(new Text(texto) { Space = SpaceProcessingModeValues.Preserve });
    return new Paragraph(pPr, run);
  }

  private static void CriarEstilos(MainDocumentPart main)
  {
    var part = main.AddNewPart<StyleDefinitionsPart>();
    part.Styles = new Styles(
      new DocDefaults(
        new RunPropertiesDefault(new RunPropertiesBaseStyle(
          new RunFonts { Ascii = "Calibri", HighAnsi = "Calibri", ComplexScript = "Calibri" },
          new FontSize { Val = "22" },
          new Languages { Val = "pt-BR" }))),
      new Style(
        new StyleName { Val = "Normal" },
        new PrimaryStyle())
      { Type = StyleValues.Paragraph, StyleId = "Normal", Default = true },
      new Style(
        new StyleName { Val = "Title" },
        new BasedOn { Val = "Normal" },
        new NextParagraphStyle { Val = "Normal" },
        new PrimaryStyle(),
        new StyleParagraphProperties(new SpacingBetweenLines { After = "240" }),
        new StyleRunProperties(new Bold(), new FontSize { Val = "32" }))
      { Type = StyleValues.Paragraph, StyleId = "Title" },
      new Style(
        new StyleName { Val = "heading 1" },
        new BasedOn { Val = "Normal" },
        new NextParagraphStyle { Val = "Normal" },
        new PrimaryStyle(),
        new StyleParagraphProperties(
          new KeepNext(),
          new SpacingBetweenLines { Before = "360", After = "120" },
          new OutlineLevel { Val = 0 }),
        new StyleRunProperties(new Bold(), new Color { Val = "1F3864" }, new FontSize { Val = "28" }))
      { Type = StyleValues.Paragraph, StyleId = "Heading1" });
  }

  private static SectionProperties CriarSecao(MainDocumentPart main)
  {
    var footerPart = main.AddNewPart<FooterPart>();
    footerPart.Footer = new Footer(
      new Paragraph(
        new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
        new Run(new RunProperties(new Color { Val = "757575" }, new FontSize { Val = "18" }),
          new Text("Página ") { Space = SpaceProcessingModeValues.Preserve }),
        new Run(new RunProperties(new Color { Val = "757575" }, new FontSize { Val = "18" }),
          new FieldChar { FieldCharType = FieldCharValues.Begin }),
        new Run(new RunProperties(new Color { Val = "757575" }, new FontSize { Val = "18" }),
          new FieldCode(" PAGE ") { Space = SpaceProcessingModeValues.Preserve }),
        new Run(new RunProperties(new Color { Val = "757575" }, new FontSize { Val = "18" }),
          new FieldChar { FieldCharType = FieldCharValues.Separate }),
        new Run(new RunProperties(new Color { Val = "757575" }, new FontSize { Val = "18" }), new Text("1")),
        new Run(new RunProperties(new Color { Val = "757575" }, new FontSize { Val = "18" }),
          new FieldChar { FieldCharType = FieldCharValues.End })));
    footerPart.Footer.Save();

    // A4 retrato, margens de 2 cm (1134 twips)
    return new SectionProperties(
      new FooterReference { Type = HeaderFooterValues.Default, Id = main.GetIdOfPart(footerPart) },
      new PageSize { Width = 11906U, Height = 16838U },
      new PageMargin { Top = 1134, Right = 1134U, Bottom = 1134, Left = 1134U, Header = 708U, Footer = 567U, Gutter = 0U });
  }
}
