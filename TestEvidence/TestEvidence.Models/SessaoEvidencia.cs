namespace TestEvidence.Models;

public sealed class SessaoEvidencia
{
  public string Id { get; set; } = "";              // = nome da pasta da sessão
  public string Tarefa { get; set; } = "";
  public string Titulo { get; set; } = "";
  public string Requisito { get; set; } = "";
  public string Cenario { get; set; } = "";
  public string Ambiente { get; set; } = "";
  public string Testador { get; set; } = "";
  public DateTime Inicio { get; set; }
  public DateTime? Fim { get; set; }
  public StatusSessao Status { get; set; }
  public ResultadoTeste Resultado { get; set; } = ResultadoTeste.Pendente;
  public string Observacoes { get; set; } = "";
  public List<string> Etapas { get; set; } = new() { "Antes", "Execução", "Resultado" };
  public string EtapaAtual { get; set; } = "Antes";
  public List<Evidencia> Evidencias { get; set; } = new();
  public RegiaoTela? UltimaRegiao { get; set; }
  public string? UltimoDocumentoGerado { get; set; }
}
