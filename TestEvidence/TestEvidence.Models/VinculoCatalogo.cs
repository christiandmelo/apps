using System.Globalization;
using AppsCm.Compartilhado;

namespace TestEvidence.Models;

/// <summary>Liga a sessão ao catálogo compartilhado de clientes e projetos.</summary>
public static class VinculoCatalogo
{
  public const string Origem = "test-evidence";

  public static bool NomeIgual(string a, string b) =>
    CultureInfo.GetCultureInfo("pt-BR").CompareInfo.Compare(a.Trim(), b.Trim(), CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) == 0;

  /// <summary>Nomes para exibir: o atual do catálogo quando o Id resolve; senão a cópia gravada na sessão.</summary>
  public static (string cliente, string projeto) Nomes(SessaoEvidencia s, CatalogoClientes catalogo)
  {
    var cliente = s.ClienteId.Length > 0 ? catalogo.ClientePorId(s.ClienteId) : null;
    var projeto = cliente != null && s.ProjetoId.Length > 0 ? catalogo.ProjetoPorId(cliente.Id, s.ProjetoId) : null;
    return (cliente?.Nome ?? s.Cliente, projeto?.Nome ?? s.Projeto);
  }

  /// <summary>
  /// Grava cliente e projeto na sessão, criando no catálogo o que ainda não existe.
  /// Se o catálogo estiver indisponível, guarda só os nomes (Ids vazios) e retorna false.
  /// </summary>
  public static bool Vincular(SessaoEvidencia s, string cliente, string projeto)
  {
    cliente = cliente.Trim();
    projeto = projeto.Trim();
    s.ClienteId = s.ProjetoId = "";
    s.Cliente = cliente;
    s.Projeto = cliente.Length == 0 ? "" : projeto;
    if (cliente.Length == 0) return true;

    try
    {
      var c = CatalogoClientes.ObterOuCriarCliente(cliente, Origem);
      s.ClienteId = c.Id;
      s.Cliente = c.Nome;
      if (projeto.Length > 0)
      {
        var p = CatalogoClientes.ObterOuCriarProjeto(c.Id, projeto, Origem);
        s.ProjetoId = p.Id;
        s.Projeto = p.Nome;
      }
      return true;
    }
    catch (ConfiguracaoCompartilhadaException)
    {
      s.ClienteId = s.ProjetoId = "";
      return false;
    }
  }

  /// <summary>Antes de gravar: tenta vincular sessões sem Id e atualiza a cópia dos nomes quando foram renomeados.</summary>
  public static void Sincronizar(SessaoEvidencia s)
  {
    if (s.Cliente.Length == 0 && s.ClienteId.Length == 0) return;
    if (s.ClienteId.Length == 0)
    {
      Vincular(s, s.Cliente, s.Projeto);
      return;
    }
    var (cliente, projeto) = Nomes(s, CatalogoClientes.Carregar());
    s.Cliente = cliente;
    s.Projeto = projeto;
  }
}
