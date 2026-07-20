namespace AppsCm.Shell.Modulos;

/// <summary>Carrega o ícone de um módulo a partir de um arquivo solto copiado para a saída do
/// shell (ver os itens CopyToOutputDirectory nos csproj do TestCoverageUI.UI e do
/// GitSubmoduleSync.UI). Retorna null em vez de lançar — a aba/menu simplesmente fica sem ícone
/// se o arquivo não estiver lá (ex.: primeiro F5 antes do build copiar os arquivos).</summary>
internal static class IconeDoArquivo
{
  public static Icon? Carregar(params string[] caminhoRelativo)
  {
    var caminho = Path.Combine(AppContext.BaseDirectory, Path.Combine(caminhoRelativo));

    try
    {
      return File.Exists(caminho) ? new Icon(caminho) : null;
    }
    catch (Exception ex) when (ex is IOException or ArgumentException)
    {
      return null;
    }
  }
}
