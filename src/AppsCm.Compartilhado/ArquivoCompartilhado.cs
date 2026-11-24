using System.Text.Json;

namespace AppsCm.Compartilhado;

public static class PastaCompartilhada
{
  public static string Caminho => Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "apps-cm", "compartilhado");
}

/// <summary>Falha ao ler ou gravar a configuração compartilhada (lock não obtido, versão mais nova etc.).
/// A mensagem é própria para mostrar ao usuário e nunca contém segredos.</summary>
public sealed class ConfiguracaoCompartilhadaException(string mensagem) : Exception(mensagem);

/// <summary>Resultado da leitura de um arquivo JSON compartilhado.</summary>
internal sealed record Leitura<T>(T Dados, bool Invalido, bool VersaoMaisNova);

/// <summary>Leitura tolerante, lock entre processos e gravação atômica dos JSONs da pasta compartilhada.</summary>
internal static class ArquivoCompartilhado
{
  public const int VersaoConhecida = 1;
  private static readonly TimeSpan TimeoutLock = TimeSpan.FromSeconds(5);

  public static readonly JsonSerializerOptions Opcoes = new() { WriteIndented = true };

  /// <summary>Lê o arquivo; ausente = valor novo; JSON ilegível = valor novo marcado como inválido.</summary>
  public static Leitura<T> Ler<T>(string arquivo, Func<T, int> versao) where T : new()
  {
    var caminho = Path.Combine(PastaCompartilhada.Caminho, arquivo);
    try
    {
      if (!File.Exists(caminho)) return new(new T(), false, false);
      var dados = JsonSerializer.Deserialize<T>(File.ReadAllText(caminho), Opcoes);
      if (dados is null) return new(new T(), true, false);
      return new(dados, false, versao(dados) > VersaoConhecida);
    }
    catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
    {
      return new(new T(), ex is JsonException or NotSupportedException, false);
    }
  }

  /// <summary>Relê o arquivo sob lock, aplica a mudança e grava de forma atômica.</summary>
  public static void Transacao<T>(string arquivo, string nomeMutex, Func<T, int> versao, Action<T> alterar) where T : new()
  {
    using var mutex = new Mutex(false, $@"Local\apps-cm-compartilhado-{nomeMutex}");
    var obteve = false;
    try
    {
      try { obteve = mutex.WaitOne(TimeoutLock); }
      catch (AbandonedMutexException) { obteve = true; }
      if (!obteve)
        throw new ConfiguracaoCompartilhadaException("Outro processo do apps-cm está gravando esta configuração. Tente novamente.");

      var leitura = Ler<T>(arquivo, versao);
      if (leitura.VersaoMaisNova)
        throw new ConfiguracaoCompartilhadaException("Esta configuração foi criada por uma versão mais nova do apps-cm e não pode ser alterada por esta versão.");

      var caminho = Path.Combine(PastaCompartilhada.Caminho, arquivo);
      if (leitura.Invalido && File.Exists(caminho))
        File.Copy(caminho, $"{caminho}.invalido-{DateTime.Now:yyyyMMdd-HHmmss}", overwrite: true);

      alterar(leitura.Dados);
      Gravar(caminho, leitura.Dados);
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
      throw new ConfiguracaoCompartilhadaException($"Não foi possível gravar a configuração compartilhada: {ex.Message}");
    }
    finally
    {
      if (obteve) mutex.ReleaseMutex();
    }
  }

  private static void Gravar<T>(string caminho, T dados)
  {
    Directory.CreateDirectory(Path.GetDirectoryName(caminho)!);
    var tmp = caminho + ".tmp";
    File.WriteAllText(tmp, JsonSerializer.Serialize(dados, Opcoes));
    if (File.Exists(caminho)) File.Replace(tmp, caminho, null);
    else File.Move(tmp, caminho, overwrite: true);
  }
}
