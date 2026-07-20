using AppsCm.Shell.Contratos;

namespace AppsCm.Shell.Modulos;

internal sealed class ModuloTestCoverage : IAppModule
{
  public string Id => "test-coverage";
  public string NomeExibicao => "Cobertura de Testes";
  public string? Versao => typeof(TestCoverageUI.UI.MainForm).Assembly.GetName().Version?.ToString();
  public Icon? Icone => IconeDoArquivo.Carregar("Icone", "Icone_Logo_ico.ico");

  public Control CriarView() => new TestCoverageUI.UI.MainForm();

  public bool PodeFechar(Control view) => true;
}
