using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;

namespace AppsCm.Compartilhado;

public sealed class Projeto
{
  public string Id { get; set; } = Guid.NewGuid().ToString("N");
  public string Nome { get; set; } = "";
  public string? JiraProjetoChave { get; set; }
  public bool Ativo { get; set; } = true;
  public string CriadoPor { get; set; } = "";
}

public sealed class Cliente
{
  public string Id { get; set; } = Guid.NewGuid().ToString("N");
  public string Nome { get; set; } = "";
  public bool Ativo { get; set; } = true;
  public string CriadoPor { get; set; } = "";
  public List<Projeto> Projetos { get; set; } = new();
}

/// <summary>Conteúdo de clientes.json.</summary>
public sealed class ArquivoClientes
{
  public int Versao { get; set; } = 1;
  public List<Cliente> Clientes { get; set; } = new();
}

/// <summary>Catálogo de clientes e projetos (clientes.json), compartilhado entre o shell e os apps.
/// Não há exclusão física: "excluir" é Ativo = false, para os Ids já gravados nos apps continuarem resolvendo.</summary>
public sealed class CatalogoClientes
{
  private const string Arquivo = "clientes.json";
  private const string NomeMutex = "clientes";

  private static readonly object TravaWatcher = new();
  private static FileSystemWatcher? _watcher;
  private static System.Threading.Timer? _debounce;
  private static EventHandler? _alterado;

  private CatalogoClientes(List<Cliente> clientes, string? aviso)
  {
    Clientes = clientes;
    Aviso = aviso;
  }

  public IReadOnlyList<Cliente> Clientes { get; }

  /// <summary>Texto para mostrar ao usuário quando o arquivo estava ilegível ou é de uma versão mais nova.</summary>
  public string? Aviso { get; }

  public static CatalogoClientes Carregar()
  {
    var leitura = ArquivoCompartilhado.Ler<ArquivoClientes>(Arquivo, a => a.Versao);
    string? aviso = null;
    if (leitura.Invalido)
      aviso = "O arquivo de clientes está ilegível. Ele será guardado como backup (clientes.json.invalido-*) antes da próxima gravação.";
    else if (leitura.VersaoMaisNova)
      aviso = "A configuração de clientes foi criada por uma versão mais nova do apps-cm; ela é só leitura nesta versão.";
    return new CatalogoClientes(leitura.Dados.Clientes, aviso);
  }

  public Cliente? ClientePorId(string id) => Clientes.FirstOrDefault(c => c.Id == id);

  public Projeto? ProjetoPorId(string clienteId, string projetoId) =>
    ClientePorId(clienteId)?.Projetos.FirstOrDefault(p => p.Id == projetoId);

  /// <summary>Acha cliente e projeto ativos pela chave do projeto do Jira.</summary>
  public (Cliente, Projeto)? PorChaveJira(string projetoChave)
  {
    foreach (var cliente in Clientes.Where(c => c.Ativo))
      foreach (var projeto in cliente.Projetos.Where(p => p.Ativo))
        if (string.Equals(projeto.JiraProjetoChave, projetoChave, StringComparison.OrdinalIgnoreCase))
          return (cliente, projeto);
    return null;
  }

  // ---------- gravação: cada operação relê o arquivo sob lock e grava só a mudança ----------

  public static Cliente ObterOuCriarCliente(string nome, string criadoPor)
  {
    nome = ValidarNome(nome);
    Cliente? resultado = null;
    Gravar(a =>
    {
      resultado = a.Clientes.FirstOrDefault(c => Igual(c.Nome, nome));
      if (resultado == null)
      {
        resultado = new Cliente { Nome = nome, CriadoPor = criadoPor };
        a.Clientes.Add(resultado);
      }
      else if (!resultado.Ativo)
      {
        resultado.Ativo = true;
      }
    });
    return resultado!;
  }

  public static Projeto ObterOuCriarProjeto(string clienteId, string nome, string criadoPor, string? jiraChave = null)
  {
    nome = ValidarNome(nome);
    Projeto? resultado = null;
    Gravar(a =>
    {
      var cliente = ObterCliente(a, clienteId);
      resultado = cliente.Projetos.FirstOrDefault(p => Igual(p.Nome, nome));
      if (resultado == null)
      {
        resultado = new Projeto { Nome = nome, CriadoPor = criadoPor, JiraProjetoChave = LimparChave(jiraChave) };
        cliente.Projetos.Add(resultado);
      }
      else
      {
        resultado.Ativo = true;
        if (resultado.JiraProjetoChave == null && LimparChave(jiraChave) is { } chave) resultado.JiraProjetoChave = chave;
      }
    });
    return resultado!;
  }

  /// <summary>Renomeia o cliente ou, se projetoId for informado, o projeto. Nomes continuam únicos.</summary>
  public static void Renomear(string clienteId, string? projetoId, string novoNome)
  {
    novoNome = ValidarNome(novoNome);
    Gravar(a =>
    {
      var cliente = ObterCliente(a, clienteId);
      if (projetoId == null)
      {
        if (a.Clientes.Any(c => c.Id != clienteId && Igual(c.Nome, novoNome)))
          throw new ConfiguracaoCompartilhadaException($"Já existe um cliente chamado \"{novoNome}\".");
        cliente.Nome = novoNome;
      }
      else
      {
        var projeto = ObterProjeto(cliente, projetoId);
        if (cliente.Projetos.Any(p => p.Id != projetoId && Igual(p.Nome, novoNome)))
          throw new ConfiguracaoCompartilhadaException($"Já existe um projeto chamado \"{novoNome}\" neste cliente.");
        projeto.Nome = novoNome;
      }
    });
  }

  public static void Desativar(string clienteId, string? projetoId = null) => DefinirAtivo(clienteId, projetoId, false);

  public static void Reativar(string clienteId, string? projetoId = null) => DefinirAtivo(clienteId, projetoId, true);

  public static void DefinirAtivo(string clienteId, string? projetoId, bool ativo)
  {
    Gravar(a =>
    {
      var cliente = ObterCliente(a, clienteId);
      if (projetoId == null) cliente.Ativo = ativo;
      else ObterProjeto(cliente, projetoId).Ativo = ativo;
    });
  }

  public static void DefinirChaveJira(string clienteId, string projetoId, string? chave)
  {
    Gravar(a => ObterProjeto(ObterCliente(a, clienteId), projetoId).JiraProjetoChave = LimparChave(chave));
  }

  private static void Gravar(Action<ArquivoClientes> alterar)
  {
    ArquivoCompartilhado.Transacao<ArquivoClientes>(Arquivo, NomeMutex, a => a.Versao, a =>
    {
      a.Versao = ArquivoCompartilhado.VersaoConhecida;
      alterar(a);
    });
  }

  private static Cliente ObterCliente(ArquivoClientes a, string id) =>
    a.Clientes.FirstOrDefault(c => c.Id == id) ?? throw new ConfiguracaoCompartilhadaException("O cliente não existe mais no catálogo.");

  private static Projeto ObterProjeto(Cliente c, string id) =>
    c.Projetos.FirstOrDefault(p => p.Id == id) ?? throw new ConfiguracaoCompartilhadaException("O projeto não existe mais no catálogo.");

  private static string ValidarNome(string nome)
  {
    nome = (nome ?? "").Trim();
    if (nome.Length == 0) throw new ConfiguracaoCompartilhadaException("Informe o nome.");
    return nome;
  }

  private static string? LimparChave(string? chave) => string.IsNullOrWhiteSpace(chave) ? null : chave.Trim();

  // ---------- nomes únicos sem diferenciar maiúsculas nem acentos ----------

  private static bool Igual(string a, string b) => Normalizar(a) == Normalizar(b);

  private static string Normalizar(string texto)
  {
    var sb = new StringBuilder();
    foreach (var c in texto.Trim().Normalize(NormalizationForm.FormD))
      if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
    return sb.ToString().ToUpperInvariant();
  }

  // ---------- notificação de mudanças feitas por outros processos ----------

  /// <summary>Disparado (em thread de segundo plano, com debounce de 300 ms) quando um JSON da pasta muda;
  /// o consumidor deve usar BeginInvoke para voltar à thread de UI.</summary>
  public static event EventHandler? Alterado
  {
    add
    {
      lock (TravaWatcher)
      {
        _alterado += value;
        IniciarWatcher();
      }
    }
    remove
    {
      lock (TravaWatcher)
      {
        _alterado -= value;
        if (_alterado == null) PararWatcher();
      }
    }
  }

  private static void IniciarWatcher()
  {
    if (_watcher != null) return;
    try
    {
      Directory.CreateDirectory(PastaCompartilhada.Caminho);
      _debounce = new System.Threading.Timer(_ => _alterado?.Invoke(null, EventArgs.Empty), null, Timeout.Infinite, Timeout.Infinite);
      _watcher = new FileSystemWatcher(PastaCompartilhada.Caminho, "*.json") { EnableRaisingEvents = true };
      FileSystemEventHandler aoMudar = (_, _) => _debounce?.Change(300, Timeout.Infinite);
      _watcher.Changed += aoMudar;
      _watcher.Created += aoMudar;
      _watcher.Deleted += aoMudar;
      _watcher.Renamed += (_, _) => _debounce?.Change(300, Timeout.Infinite);
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
    {
      // sem watcher, os combos só atualizam ao reabrir; não é crítico
    }
  }

  private static void PararWatcher()
  {
    _watcher?.Dispose();
    _watcher = null;
    _debounce?.Dispose();
    _debounce = null;
  }
}
