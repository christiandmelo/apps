using AppsCm.Shell.Contratos;

namespace AppsCm.Shell.Modulos;

internal sealed class ModuloGitSubmoduleSync : IAppModule
{
  public string Id => "git-submodule-sync";
  public string NomeExibicao => "Sincronização de Submódulos";
  public string? Versao => typeof(GitSubmoduleSync.UI.MainForm).Assembly.GetName().Version?.ToString();
  public Icon? Icone => IconeDoArquivo.Carregar("Icone", "GitSubmoduleSync.ico");

  public Control CriarView() => new GitSubmoduleSync.UI.MainForm();

  public bool PodeFechar(Control view)
  {
    if (view is not GitSubmoduleSync.UI.MainForm form || !form.EmExecucao)
      return true;

    var resultado = MessageBox.Show(
      "Uma execução está em andamento nesta aba. Feche pela própria aba (botão Cancelar) antes de encerrá-la.",
      "GitSubmoduleSync", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    _ = resultado;
    return false;
  }
}
