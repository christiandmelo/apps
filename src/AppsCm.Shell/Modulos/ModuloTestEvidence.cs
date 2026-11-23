using AppsCm.Shell.Contratos;

namespace AppsCm.Shell.Modulos;

internal sealed class ModuloTestEvidence : IAppModule
{
  public string Id => "test-evidence";
  public string NomeExibicao => "Evidência de Teste";
  public string? Versao => typeof(TestEvidence.UI.MainForm).Assembly.GetName().Version?.ToString();
  public Icon? Icone => IconeDoArquivo.Carregar("Icone", "TestEvidence.ico");

  public Control CriarView() => new TestEvidence.UI.MainForm();

  public bool PodeFechar(Control view)
  {
    if (view is not TestEvidence.UI.MainForm form || !form.EmSessaoAtiva)
      return true;

    var resposta = MessageBox.Show(
      "Há uma evidência em andamento. Ela fica salva e pode ser retomada depois. Fechar a aba mesmo assim?",
      "Evidência de Teste", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
    return resposta == DialogResult.Yes;
  }
}
