using System.Diagnostics;
using AppsCm.Compartilhado;
using TestEvidence.Models;
using TestEvidence.Services;

namespace TestEvidence.UI;

/// <summary>Fluxo de gerar o .docx (escolha do destino, geração, marcar como finalizada), compartilhado por Revisão e histórico.</summary>
internal static class GeradorDocumento
{
  public static bool Gerar(IWin32Window dono, SessaoEvidencia sessao, SessaoRepository repo, ConfiguracaoApp config)
  {
    const string titulo = "Evidência de Teste";

    if (sessao.Evidencias.Count == 0 &&
        MessageBox.Show(dono, "A sessão não tem evidências. Gerar o documento mesmo assim?", titulo,
          MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return false;

    var pasta = config.PastaSaidaEfetiva;
    try { Directory.CreateDirectory(pasta); }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { pasta = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments); }

    static string Sanitizar(string texto) => string.Concat(texto.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
    var nomeTarefa = Sanitizar(sessao.Tarefa);
    var nomeCliente = Sanitizar(VinculoCatalogo.Nomes(sessao, CatalogoClientes.Carregar()).cliente);
    using var dlg = new SaveFileDialog
    {
      Filter = "Documento Word (*.docx)|*.docx",
      InitialDirectory = pasta,
      FileName = nomeCliente.Length > 0
        ? $"Evidencia_{nomeCliente}_{nomeTarefa}_{DateTime.Now:yyyyMMdd}.docx"
        : $"Evidencia_{nomeTarefa}_{DateTime.Now:yyyyMMdd}.docx"
    };
    if (dlg.ShowDialog(dono) != DialogResult.OK) return false;

    var cursorAnterior = Cursor.Current;
    try
    {
      Cursor.Current = Cursors.WaitCursor;
      new DocumentoWordService().Gerar(sessao, repo.PastaDa(sessao), dlg.FileName);
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
      MessageBox.Show(dono, ex.Message, titulo, MessageBoxButtons.OK, MessageBoxIcon.Warning);
      return false;
    }
    finally
    {
      Cursor.Current = cursorAnterior;
    }

    sessao.Status = StatusSessao.Finalizada;
    sessao.Fim = DateTime.Now;
    sessao.UltimoDocumentoGerado = dlg.FileName;
    ControladorSessao.SalvarSeguro(repo, sessao);
    ControladorSessao.NotificarEstado();

    if (MessageBox.Show(dono, "Documento gerado. Abrir o documento?", titulo, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
    {
      try { Process.Start(new ProcessStartInfo(dlg.FileName) { UseShellExecute = true }); }
      catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or FileNotFoundException) { }
    }
    return true;
  }
}
