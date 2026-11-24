namespace TestEvidence.Models;

public static class CaminhoConfig
{
  private const string AppId = "test-evidence";

  public static string Pasta => Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "apps-cm", AppId);

  public static string Arquivo => Path.Combine(Pasta, "configs.json");
  public static string PastaSessoes => Path.Combine(Pasta, "sessoes");
}
