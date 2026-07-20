using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;

namespace AppsCm.Shell.Atualizacao;

internal sealed class UpdateInfo
{
  public string version { get; set; } = string.Empty;
  public string url { get; set; } = string.Empty;
  public string? sha256 { get; set; }
}

internal sealed class UpdateService
{
  // Override por variável de ambiente: permite testar o fluxo de update inteiro apontando para
  // um servidor local, sem precisar de uma release real no GitHub.
  private static string VersionApiUrl =>
    Environment.GetEnvironmentVariable("APPSCM_VERSION_URL")
    ?? "https://raw.githubusercontent.com/christiandmelo/apps/main/version.json";

  public async Task<UpdateInfo?> CheckForUpdateAsync()
  {
    try
    {
      using var http = new HttpClient();
      var json = await http.GetStringAsync(VersionApiUrl);
      var latest = JsonSerializer.Deserialize<UpdateInfo>(json);

      if (latest is null || string.IsNullOrWhiteSpace(latest.version))
        return null;

      var atual = VersionHelper.VersaoAtual();
      var novo = VersionHelper.Normalizar(latest.version);

      return novo > atual ? latest : null;
    }
    catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
    {
      RegistrarFalha(ex);
      return null;
    }
  }

  public async Task DownloadAndRunUpdaterAsync(UpdateInfo info)
  {
    string tempZip = Path.Combine(Path.GetTempPath(), "apps-cm_update.zip");

    using (var http = new HttpClient())
    {
      var data = await http.GetByteArrayAsync(info.url);
      await File.WriteAllBytesAsync(tempZip, data);
    }

    string updaterPath = Path.Combine(AppContext.BaseDirectory, "Updater.exe");
    if (!File.Exists(updaterPath))
      throw new FileNotFoundException($"Updater.exe não encontrado no caminho esperado: {updaterPath}");

    string exePath = Environment.ProcessPath!;

    Process.Start(new ProcessStartInfo
    {
      FileName = updaterPath,
      Arguments = $"\"{tempZip}\" \"{exePath}\" \"{Environment.ProcessId}\" \"{info.sha256}\"",
      UseShellExecute = true
    });

    Environment.Exit(0);
  }

  // Verificação de update roda em todo startup; falha de rede não pode virar diálogo — só log,
  // para diferenciar "offline" de "version.json malformado" sem incomodar o usuário em ambos os casos.
  private static void RegistrarFalha(Exception ex)
  {
    try
    {
      var pastaLogs = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "apps-cm", "logs");
      Directory.CreateDirectory(pastaLogs);
      File.AppendAllText(Path.Combine(pastaLogs, "update-check.log"),
        $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex.GetType().Name}: {ex.Message}{Environment.NewLine}");
    }
    catch (IOException)
    {
      // Log é best-effort.
    }
  }
}
