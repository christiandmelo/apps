using System.Reflection;

namespace AppsCm.Shell.Atualizacao;

internal static class VersionHelper
{
  // Assembly.GetEntryAssembly() (não GetExecutingAssembly()) — com tudo centralizado num único
  // exe, esta é a única versão que importa comparar; não há mais o risco do TestCoverageUI antigo,
  // em que a versão lida vinha do assembly errado (Services em vez de UI).
  public static Version VersaoAtual() =>
    Normalizar(Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0, 0, 0));

  public static Version Normalizar(string versao) => Normalizar(Version.Parse(versao));

  // "1.0.4" e "1.0.4.0" são a mesma versão, mas Version.Parse os deixa com Revision -1 vs 0,
  // fazendo -1 < 0 comparar como "mais antigo" por acidente. Normalizar para 4 componentes
  // sempre torna a comparação exata em vez de correta só por sorte de arredondamento.
  private static Version Normalizar(Version v) =>
    new(Math.Max(v.Major, 0), Math.Max(v.Minor, 0), Math.Max(v.Build, 0), Math.Max(v.Revision, 0));
}
