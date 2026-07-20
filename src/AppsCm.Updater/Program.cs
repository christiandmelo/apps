using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;

namespace AppsCm.Updater;

// Processo separado, disparado pelo AppsCm.exe antes de ele se encerrar (Environment.Exit),
// porque o updater precisa sobrescrever os binários da aplicação principal enquanto ela está
// fechando. Uso: Updater.exe <zipPath> <targetExePath> <callerProcessId> [expectedSha256]
internal static class Program
{
  private static readonly List<string> _log = new();

  [STAThread]
  static void Main(string[] args)
  {
    Log("=== Iniciando processo de atualização ===");

    if (args.Length < 3)
    {
      Log("Uso incorreto: Updater.exe <zipPath> <targetExePath> <callerProcessId> [expectedSha256]");
      Falhar("Parâmetros inválidos ao iniciar o Updater.");
      return;
    }

    string zipPath = args[0];
    string targetExePath = args[1];
    string targetDir = Path.GetDirectoryName(targetExePath)!;
    string? expectedSha256 = args.Length > 3 ? args[3] : null;

    Log($"ZIP recebido: {zipPath}");
    Log($"Pasta de destino: {targetDir}");

    try
    {
      if (int.TryParse(args[2], out var callerPid))
        AguardarProcessoChamador(callerPid);

      if (!File.Exists(zipPath))
      {
        Log("ERRO: Arquivo ZIP não encontrado.");
        Falhar("Arquivo de atualização não encontrado.");
        return;
      }

      if (!VerificarZip(zipPath, expectedSha256, out var motivoFalha))
      {
        Log($"ERRO: {motivoFalha}");
        Falhar($"O pacote de atualização parece inválido ou corrompido ({motivoFalha}). Nenhum arquivo foi alterado.");
        return;
      }

      string tempExtractPath = Path.Combine(Path.GetTempPath(), "apps-cm_extract");
      if (Directory.Exists(tempExtractPath))
        Directory.Delete(tempExtractPath, true);
      Directory.CreateDirectory(tempExtractPath);

      Log("Extraindo atualização para pasta temporária...");
      ZipFile.ExtractToDirectory(zipPath, tempExtractPath, true);

      PrepararAutoatualizacaoDoUpdater();

      int falhas = CopiarArquivosAtualizados(tempExtractPath, targetDir);

      Directory.Delete(tempExtractPath, true);

      Log(falhas > 0
        ? $"Atualização concluída com {falhas} arquivo(s) não atualizado(s) (veja avisos acima)."
        : "Atualização concluída com sucesso.");

      Log("Iniciando aplicação atualizada...");
      Process.Start(new ProcessStartInfo(targetExePath)
      {
        WorkingDirectory = targetDir,
        UseShellExecute = true
      });
    }
    catch (Exception ex)
    {
      Log($"ERRO durante atualização: {ex.Message}");
      Log(ex.StackTrace ?? "");
      Falhar($"Falha inesperada durante a atualização: {ex.Message}");
      return;
    }

    GravarLog();
  }

  private static void AguardarProcessoChamador(int callerPid)
  {
    Log($"Aguardando encerramento do processo chamador (PID {callerPid})...");
    try
    {
      using var callerProcess = Process.GetProcessById(callerPid);
      if (!callerProcess.WaitForExit(30_000))
        Log("[AVISO] Tempo esgotado aguardando o processo chamador; prosseguindo mesmo assim.");
    }
    catch (ArgumentException)
    {
      // Processo já havia encerrado — nada a esperar.
    }
  }

  private static bool VerificarZip(string zipPath, string? expectedSha256, out string motivo)
  {
    motivo = "";

    try
    {
      if (!string.IsNullOrWhiteSpace(expectedSha256))
      {
        using var stream = File.OpenRead(zipPath);
        var hash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        if (!string.Equals(hash, expectedSha256, StringComparison.OrdinalIgnoreCase))
        {
          motivo = "hash SHA-256 do zip não confere";
          return false;
        }
      }

      using var zip = ZipFile.OpenRead(zipPath);
      if (zip.Entries.Count == 0)
      {
        motivo = "zip vazio";
        return false;
      }
      if (!zip.Entries.Any(e => e.FullName.Equals("AppsCm.exe", StringComparison.OrdinalIgnoreCase)))
      {
        motivo = "zip não contém AppsCm.exe";
        return false;
      }

      return true;
    }
    catch (Exception ex) when (ex is InvalidDataException or IOException)
    {
      motivo = $"zip corrompido ou ilegível ({ex.Message})";
      return false;
    }
  }

  // Windows permite renomear um .exe em execução (só bloqueia deletar), então o próprio Updater
  // consegue se autoatualizar: sai do caminho antes da cópia, sem precisar pular o próprio nome.
  private static void PrepararAutoatualizacaoDoUpdater()
  {
    var meuExe = Environment.ProcessPath;
    if (string.IsNullOrEmpty(meuExe)) return;

    try
    {
      var antigo = Path.ChangeExtension(meuExe, ".old");
      if (File.Exists(antigo)) File.Delete(antigo);
      File.Move(meuExe, antigo);
      Log($"Updater renomeado para {Path.GetFileName(antigo)} para permitir autoatualização.");
    }
    catch (IOException ex)
    {
      Log($"[AVISO] Não foi possível preparar a autoatualização do Updater: {ex.Message}");
    }
  }

  private static int CopiarArquivosAtualizados(string tempExtractPath, string targetDir)
  {
    int falhas = 0;

    foreach (var file in Directory.GetFiles(tempExtractPath, "*", SearchOption.AllDirectories))
    {
      string relativePath = Path.GetRelativePath(tempExtractPath, file);
      string destFile = Path.Combine(targetDir, relativePath);

      try
      {
        Directory.CreateDirectory(Path.GetDirectoryName(destFile)!);

        // Um arquivo antigo marcado como somente-leitura impede a sobrescrita mesmo com overwrite:true
        if (File.Exists(destFile))
          File.SetAttributes(destFile, FileAttributes.Normal);

        File.Copy(file, destFile, true);
        Log($"Atualizado: {relativePath}");
      }
      catch (Exception ex)
      {
        falhas++;
        Log($"[AVISO] Falha ao atualizar {relativePath}: {ex.Message}");
      }
    }

    return falhas;
  }

  private static void Log(string mensagem) => _log.Add($"[{DateTime.Now:HH:mm:ss}] {mensagem}");

  private static void Falhar(string mensagemUsuario)
  {
    var caminhoLog = GravarLog();
    MessageBox.Show(
      $"{mensagemUsuario}\n\nDetalhes em: {caminhoLog}",
      "apps-cm — Atualização", MessageBoxButtons.OK, MessageBoxIcon.Error);
  }

  private static string GravarLog()
  {
    var pastaLogs = Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "apps-cm", "logs");
    Directory.CreateDirectory(pastaLogs);

    var caminho = Path.Combine(pastaLogs, $"updater-{DateTime.Now:yyyyMMdd-HHmmss}.log");
    File.WriteAllLines(caminho, _log);

    var antigos = Directory.GetFiles(pastaLogs, "updater-*.log").OrderByDescending(f => f).ToList();
    foreach (var antigo in antigos.Skip(20)) File.Delete(antigo);

    return caminho;
  }
}
