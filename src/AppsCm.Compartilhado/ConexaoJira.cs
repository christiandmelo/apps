using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace AppsCm.Compartilhado;

/// <summary>Conexão com o Jira (jira.json). Editada só pelo shell; os apps apenas leem.</summary>
public sealed class ConexaoJira
{
  private const string Arquivo = "jira.json";
  private static readonly byte[] Entropia = Encoding.UTF8.GetBytes("apps-cm/jira/v1");

  public int Versao { get; set; } = 1;
  public string Url { get; set; } = "";
  public string TokenProtegido { get; set; } = "";
  public string HeadersExtras { get; set; } = "User-Agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/86.0.4240.183 Safari/537.36";
  public bool VerificarSsl { get; set; } = true;
  public int TimeoutSegundos { get; set; } = 30;

  /// <summary>Preenchido por Carregar quando o arquivo existe mas não pôde ser lido.</summary>
  [JsonIgnore] public bool ArquivoInvalido { get; private set; }

  /// <summary>Preenchido por Carregar quando o arquivo foi criado por uma versão mais nova.</summary>
  [JsonIgnore] public bool VersaoMaisNova { get; private set; }

  [JsonIgnore] public bool Configurada => UrlValida(Url) && Token() != null;

  /// <summary>Decifra o token; null se não houver ou se não for possível decifrar (outro usuário/máquina).</summary>
  public string? Token()
  {
    if (string.IsNullOrEmpty(TokenProtegido)) return null;
    try
    {
      var bytes = ProtectedData.Unprotect(Convert.FromBase64String(TokenProtegido), Entropia, DataProtectionScope.CurrentUser);
      var token = Encoding.UTF8.GetString(bytes);
      return token.Length == 0 ? null : token;
    }
    catch (Exception ex) when (ex is CryptographicException or FormatException or PlatformNotSupportedException)
    {
      return null;
    }
  }

  public void DefinirToken(string token)
  {
    TokenProtegido = string.IsNullOrEmpty(token)
      ? ""
      : Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(token), Entropia, DataProtectionScope.CurrentUser));
  }

  /// <summary>HeadersExtras (pares k=v separados por ';') mais o origin derivado da Url, que um origin explícito sobrepõe.</summary>
  public IReadOnlyDictionary<string, string> Headers()
  {
    var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    foreach (var par in HeadersExtras.Split(';', StringSplitOptions.RemoveEmptyEntries))
    {
      var i = par.IndexOf('=');
      if (i <= 0) continue;
      var chave = par[..i].Trim();
      if (chave.Length > 0) headers[chave] = par[(i + 1)..].Trim();
    }
    if (!headers.ContainsKey("origin") && UrlValida(Url))
      headers["origin"] = new Uri(Url).GetLeftPart(UriPartial.Authority);
    return headers;
  }

  public static bool UrlValida(string? url) =>
    Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

  /// <summary>Arquivo ausente ou ilegível = configuração nova (Jira desligado).</summary>
  public static ConexaoJira Carregar()
  {
    var leitura = ArquivoCompartilhado.Ler<ConexaoJira>(Arquivo, c => c.Versao);
    leitura.Dados.ArquivoInvalido = leitura.Invalido;
    leitura.Dados.VersaoMaisNova = leitura.VersaoMaisNova;
    return leitura.Dados;
  }

  /// <summary>Só o shell chama.</summary>
  public void Salvar()
  {
    var copia = this;
    ArquivoCompartilhado.Transacao<ConexaoJira>(Arquivo, "jira", c => c.Versao, atual =>
    {
      atual.Versao = ArquivoCompartilhado.VersaoConhecida;
      atual.Url = copia.Url;
      atual.TokenProtegido = copia.TokenProtegido;
      atual.HeadersExtras = copia.HeadersExtras;
      atual.VerificarSsl = copia.VerificarSsl;
      atual.TimeoutSegundos = copia.TimeoutSegundos;
    });
  }
}
