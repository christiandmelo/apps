namespace TestEvidence.Models;

public sealed class Evidencia
{
  public string Id { get; set; } = Guid.NewGuid().ToString("N");
  public int Ordem { get; set; }
  public string Etapa { get; set; } = "";
  public string Legenda { get; set; } = "";
  public string ArquivoImagem { get; set; } = "";   // relativo à pasta da sessão: "img\001.png"
  public DateTime CapturadoEm { get; set; }
  public TipoCaptura Tipo { get; set; }
}
