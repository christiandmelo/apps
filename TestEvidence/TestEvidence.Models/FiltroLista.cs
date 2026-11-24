using System.Globalization;

namespace TestEvidence.Models;

/// <summary>Filtro da lista de evidências do MainForm.</summary>
public sealed class FiltroLista
{
  public const string SemCliente = "-";

  public string ClienteId { get; set; } = "";   // vazio = todos; "-" = sem cliente
  public string ProjetoId { get; set; } = "";   // vazio = todos
  public StatusSessao? Status { get; set; }
  public ResultadoTeste? Resultado { get; set; }
  public string Texto { get; set; } = "";       // busca em Tarefa, Título, Cenário, Cliente e Projeto

  public bool Ativo => ClienteId.Length > 0 || ProjetoId.Length > 0 || Status != null || Resultado != null || Texto.Trim().Length > 0;

  /// <summary>Chave do cliente: o Id do catálogo, o nome (quando só há a cópia gravada) ou "-" (sem cliente).</summary>
  public static string ChaveCliente(SessaoEvidencia s) =>
    s.ClienteId.Length > 0 ? s.ClienteId : s.Cliente.Length > 0 ? "n:" + s.Cliente : SemCliente;

  public static string ChaveProjeto(SessaoEvidencia s) =>
    s.ProjetoId.Length > 0 ? s.ProjetoId : s.Projeto.Length > 0 ? "n:" + s.Projeto : "";

  public bool Aceita(SessaoEvidencia s, Func<SessaoEvidencia, (string cliente, string projeto)> nomes)
  {
    if (ClienteId.Length > 0 && ChaveCliente(s) != ClienteId) return false;
    if (ProjetoId.Length > 0 && ChaveProjeto(s) != ProjetoId) return false;
    if (Status != null && s.Status != Status) return false;
    if (Resultado != null && s.Resultado != Resultado) return false;

    var busca = Texto.Trim();
    if (busca.Length == 0) return true;
    var (cliente, projeto) = nomes(s);
    return new[] { s.Tarefa, s.Titulo, s.Cenario, cliente, projeto }.Any(campo => Contem(campo, busca));
  }

  private static bool Contem(string campo, string busca) =>
    CultureInfo.GetCultureInfo("pt-BR").CompareInfo.IndexOf(campo, busca, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;
}
