using System.Text.Json;

namespace TestEvidence.Models;

public sealed class ConfiguracaoApp
{
  public string AtalhoEvidenciar { get; set; } = "Ctrl+Shift+E";
  public string AtalhoRepetirRegiao { get; set; } = "Ctrl+Shift+R";
  public bool PedirLegendaAposCaptura { get; set; } = true;
  public bool MinimizarShellAoIniciar { get; set; } = true;
  public string PastaSaidaDocumentos { get; set; } = "";   // vazio = Documentos\Evidências
  public string UltimoTestador { get; set; } = "";
  public string UltimoAmbiente { get; set; } = "";
  public List<string> AmbientesUsados { get; set; } = new();
  public List<string> EtapasPadrao { get; set; } = new() { "Antes", "Execução", "Resultado" };
  public PosicaoTela? PosicaoBarra { get; set; }

  private static readonly JsonSerializerOptions Opcoes = new() { WriteIndented = true };

  public string PastaSaidaEfetiva => string.IsNullOrWhiteSpace(PastaSaidaDocumentos)
    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Evidências")
    : PastaSaidaDocumentos;

  public static ConfiguracaoApp Carregar()
  {
    try
    {
      if (File.Exists(CaminhoConfig.Arquivo))
      {
        var cfg = JsonSerializer.Deserialize<ConfiguracaoApp>(File.ReadAllText(CaminhoConfig.Arquivo));
        if (cfg != null)
        {
          if (cfg.EtapasPadrao.Count == 0) cfg.EtapasPadrao = new() { "Antes", "Execução", "Resultado" };
          return cfg;
        }
      }
    }
    catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
    {
      // arquivo ilegível: segue com os padrões
    }

    var novo = new ConfiguracaoApp();
    novo.Salvar();
    return novo;
  }

  public void Salvar()
  {
    try
    {
      Directory.CreateDirectory(CaminhoConfig.Pasta);
      File.WriteAllText(CaminhoConfig.Arquivo, JsonSerializer.Serialize(this, Opcoes));
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
      // não derruba o app por falha ao gravar configurações
    }
  }
}
