namespace TestEvidence.UI;

internal static class IconeApp
{
  private static readonly Icon? Instancia = CarregarIcone();

  public static Icon? Obter() => Instancia;

  private static Icon? CarregarIcone()
  {
    try
    {
      var arquivo = Path.Combine(AppContext.BaseDirectory, "Icone", "TestEvidence.ico");
      return File.Exists(arquivo) ? new Icon(arquivo) : null;
    }
    catch (Exception ex) when (ex is IOException or ArgumentException)
    {
      return null;
    }
  }
}
