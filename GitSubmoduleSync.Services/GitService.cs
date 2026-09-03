using System.Diagnostics;
using System.Text.RegularExpressions;
using GitSubmoduleSync.Models;

namespace GitSubmoduleSync.Services;

public sealed class GitService
{
  private readonly BranchResolver _branchResolver = new();

  public async Task<IReadOnlyList<SubmoduloInfo>> DescobrirAsync(string pastaRaiz, CancellationToken ct, IProgress<LogEvent>? log = null)
  {
    var declarados = LerGitmodules(pastaRaiz);
    log?.Report(new LogEvent(NivelLog.Detalhe, "verificando status dos submódulos (git submodule status)…"));
    var status = await LerSubmoduleStatusAsync(pastaRaiz, ct);

    return declarados
      .Select(d => new SubmoduloInfo(
        d.Nome, d.Path, d.Url, d.Branch,
        Inicializado: status.TryGetValue(d.Path, out var s) && s != '-'))
      .ToList();
  }

  public async Task<ResultadoEtapa> AtualizarPaiAsync(string pastaRaiz, IProgress<LogEvent> log, CancellationToken ct)
  {
    var sw = Stopwatch.StartNew();

    log.Report(new LogEvent(NivelLog.Detalhe, "verificando status do repositório pai…"));
    var statusResultado = await GitProcessRunner.ExecutarAsync(pastaRaiz, "status --porcelain", ct);
    if (!statusResultado.Sucesso)
    {
      sw.Stop();
      log.Report(new LogEvent(NivelLog.Erro, "não foi possível verificar o status do repositório pai."));
      return new ResultadoEtapa("Repositório pai", false, sw.Elapsed, statusResultado.Erro);
    }
    if (!string.IsNullOrWhiteSpace(statusResultado.Saida))
    {
      var caminhosSubmodulos = LerGitmodules(pastaRaiz).Select(d => d.Path).ToList();
      if (!ContemApenasSujeiraDeSubmodulos(statusResultado.Saida, caminhosSubmodulos))
      {
        sw.Stop();
        log.Report(new LogEvent(NivelLog.Aviso, "repositório pai com alterações não commitadas — pulado (nenhum arquivo local foi tocado)."));
        return new ResultadoEtapa("Repositório pai", false, sw.Elapsed, "WorkingTreeSujo");
      }
      log.Report(new LogEvent(NivelLog.Aviso, "repositório pai com ponteiro(s) de submódulo desatualizado(s) — prosseguindo com o pull."));
    }

    log.Report(new LogEvent(NivelLog.Detalhe, "atualizando repositório pai (git pull)…"));
    var pull = await GitProcessRunner.ExecutarAsync(pastaRaiz, "pull --ff-only", ct);
    sw.Stop();

    if (pull.Sucesso)
    {
      log.Report(new LogEvent(NivelLog.Sucesso, "repositório pai atualizado."));
      return new ResultadoEtapa("Repositório pai", true, sw.Elapsed);
    }

    log.Report(new LogEvent(NivelLog.Erro, $"falha ao atualizar o repositório pai: {pull.Erro.Trim()}"));
    return new ResultadoEtapa("Repositório pai", false, sw.Elapsed, pull.Erro);
  }

  public async Task<IReadOnlyList<ResultadoSubmodulo>> SincronizarAsync(
    SyncProfile perfil, IProgress<LogEvent> log, CancellationToken ct, IProgress<ProgressoGit>? progresso = null)
  {
    log.Report(new LogEvent(NivelLog.Detalhe, "descobrindo submódulos declarados no .gitmodules…"));
    var todos = await DescobrirAsync(perfil.PastaRaiz, ct, log);
    var alvo = todos
      .Where(s => !perfil.SubmodulosIgnorados.Contains(s.Nome, StringComparer.OrdinalIgnoreCase))
      .ToList();

    using var semaforo = new SemaphoreSlim(perfil.GrauParalelismoGit > 0 ? perfil.GrauParalelismoGit : 4);

    var concluidos = 0;
    var total = alvo.Count;
    progresso?.Report(new ProgressoGit(0, total));

    var tarefas = alvo.Select(async sub =>
    {
      await semaforo.WaitAsync(ct);
      try
      {
        return await SincronizarUmAsync(perfil, sub, log, ct);
      }
      finally
      {
        semaforo.Release();
        var c = Interlocked.Increment(ref concluidos);
        progresso?.Report(new ProgressoGit(c, total));
      }
    });

    return await Task.WhenAll(tarefas);
  }

  public async Task<IReadOnlyList<ResultadoPonteiroSubmodulo>> AtualizarPonteirosSubmodulosAsync(
    SyncProfile perfil, IProgress<LogEvent> log, CancellationToken ct, IProgress<ProgressoGit>? progresso = null)
  {
    log.Report(new LogEvent(NivelLog.Detalhe, "descobrindo submódulos declarados no .gitmodules…"));
    var todos = await DescobrirAsync(perfil.PastaRaiz, ct, log);
    var alvo = todos
      .Where(s => s.Inicializado)
      .Where(s => !perfil.SubmodulosIgnorados.Contains(s.Nome, StringComparer.OrdinalIgnoreCase))
      .ToList();

    using var semaforo = new SemaphoreSlim(perfil.GrauParalelismoGit > 0 ? perfil.GrauParalelismoGit : 4);

    var concluidos = 0;
    var total = alvo.Count;
    progresso?.Report(new ProgressoGit(0, total));

    var tarefas = alvo.Select(async proj =>
    {
      await semaforo.WaitAsync(ct);
      try
      {
        return await AtualizarPonteirosDeUmAsync(perfil, proj, log, ct);
      }
      finally
      {
        semaforo.Release();
        var c = Interlocked.Increment(ref concluidos);
        progresso?.Report(new ProgressoGit(c, total));
      }
    });

    return await Task.WhenAll(tarefas);
  }

  private async Task<ResultadoPonteiroSubmodulo> AtualizarPonteirosDeUmAsync(
    SyncProfile perfil, SubmoduloInfo proj, IProgress<LogEvent> log, CancellationToken ct)
  {
    var caminhoProjeto = Path.Combine(perfil.PastaRaiz, proj.Caminho);
    var filhos = LerGitmodules(caminhoProjeto);
    if (filhos.Count == 0)
    {
      log.Report(new LogEvent(NivelLog.Detalhe, "sem submódulos filhos — nada a atualizar.", Submodulo: proj.Nome));
      return new ResultadoPonteiroSubmodulo(proj.Nome, StatusPonteiroSubmodulo.SemSubmodulosFilhos, Array.Empty<string>(), null);
    }

    var filhosAtualizados = new List<string>();
    foreach (var filho in filhos)
    {
      ct.ThrowIfCancellationRequested();
      var caminhoFilho = Path.Combine(caminhoProjeto, filho.Path);
      if (!Directory.Exists(caminhoFilho))
      {
        log.Report(new LogEvent(NivelLog.Aviso, $"submódulo filho '{filho.Path}' não inicializado — pulado.", Submodulo: proj.Nome));
        continue;
      }

      // Tenta sempre checkout na branch "main" da skill; se não existir, loga erro e pula
      // esse filho (não tenta adivinhar outra branch).
      log.Report(new LogEvent(NivelLog.Detalhe, $"alternando '{filho.Path}' para a branch 'main'…", Submodulo: proj.Nome));
      var checkout = await GitProcessRunner.ExecutarAsync(caminhoFilho, "checkout main", ct);
      if (!checkout.Sucesso)
      {
        log.Report(new LogEvent(NivelLog.Erro, $"não foi possível fazer checkout na branch 'main' de '{filho.Path}': {checkout.Erro.Trim()}", Submodulo: proj.Nome));
        continue;
      }

      log.Report(new LogEvent(NivelLog.Detalhe, $"atualizando '{filho.Path}' (git pull)…", Submodulo: proj.Nome));
      var pull = await GitProcessRunner.ExecutarAsync(caminhoFilho, "pull --ff-only origin main", ct);
      if (!pull.Sucesso)
      {
        log.Report(new LogEvent(NivelLog.Erro, $"falha ao atualizar '{filho.Path}': {pull.Erro.Trim()}", Submodulo: proj.Nome));
        return new ResultadoPonteiroSubmodulo(proj.Nome, StatusPonteiroSubmodulo.Erro, filhosAtualizados, pull.Erro);
      }
      if (!pull.Saida.Contains("up to date", StringComparison.OrdinalIgnoreCase))
      {
        filhosAtualizados.Add(filho.Path);
      }
    }

    var statusResultado = await GitProcessRunner.ExecutarAsync(caminhoProjeto, "status --porcelain", ct);
    if (!statusResultado.Sucesso)
    {
      log.Report(new LogEvent(NivelLog.Erro, "não foi possível verificar o status do projeto após atualizar as skills.", Submodulo: proj.Nome));
      return new ResultadoPonteiroSubmodulo(proj.Nome, StatusPonteiroSubmodulo.Erro, filhosAtualizados, statusResultado.Erro);
    }
    if (string.IsNullOrWhiteSpace(statusResultado.Saida))
    {
      log.Report(new LogEvent(NivelLog.Info, "ponteiro(s) já estavam atualizados.", Submodulo: proj.Nome));
      return new ResultadoPonteiroSubmodulo(proj.Nome, StatusPonteiroSubmodulo.NadaAAtualizar, filhosAtualizados, null);
    }

    var caminhosFilhos = filhos.Select(f => f.Path).ToList();
    if (!ContemApenasSujeiraDeSubmodulos(statusResultado.Saida, caminhosFilhos))
    {
      log.Report(new LogEvent(NivelLog.Aviso,
        "projeto tem alterações locais além do(s) ponteiro(s) de submódulo — commit/push do ponteiro foi pulado (ação manual necessária).",
        Submodulo: proj.Nome));
      return new ResultadoPonteiroSubmodulo(proj.Nome, StatusPonteiroSubmodulo.AlteracaoRealDetectada, filhosAtualizados, statusResultado.Saida);
    }

    var argAdd = string.Join(' ', caminhosFilhos.Select(c => $"\"{c}\""));
    var add = await GitProcessRunner.ExecutarAsync(caminhoProjeto, $"add {argAdd}", ct);
    if (!add.Sucesso)
    {
      log.Report(new LogEvent(NivelLog.Erro, $"falha ao adicionar ponteiro(s) ao stage: {add.Erro.Trim()}", Submodulo: proj.Nome));
      return new ResultadoPonteiroSubmodulo(proj.Nome, StatusPonteiroSubmodulo.Erro, filhosAtualizados, add.Erro);
    }

    var commit = await GitProcessRunner.ExecutarAsync(caminhoProjeto, "commit -m \"Atualizando ponteiro\"", ct);
    if (!commit.Sucesso)
    {
      log.Report(new LogEvent(NivelLog.Erro, $"falha ao commitar ponteiro: {commit.Erro.Trim()}", Submodulo: proj.Nome));
      return new ResultadoPonteiroSubmodulo(proj.Nome, StatusPonteiroSubmodulo.Erro, filhosAtualizados, commit.Erro);
    }

    // Commit já é irreversível localmente: sempre tenta o push (CancellationToken.None) mesmo
    // se o usuário cancelar agora, para nunca deixar um commit órfão sem tentativa de envio.
    log.Report(new LogEvent(NivelLog.Detalhe, "enviando ponteiro atualizado ao remoto (git push)…", Submodulo: proj.Nome));
    var push = await GitProcessRunner.ExecutarAsync(caminhoProjeto, "push", CancellationToken.None);
    if (!push.Sucesso)
    {
      log.Report(new LogEvent(NivelLog.Erro, $"commit feito localmente, mas o push falhou: {push.Erro.Trim()}", Submodulo: proj.Nome));
      return new ResultadoPonteiroSubmodulo(proj.Nome, StatusPonteiroSubmodulo.FalhaNoPush, filhosAtualizados, push.Erro);
    }

    log.Report(new LogEvent(NivelLog.Sucesso, $"ponteiro(s) atualizado(s) e enviado(s): {string.Join(", ", filhosAtualizados)}.", Submodulo: proj.Nome));
    return new ResultadoPonteiroSubmodulo(proj.Nome, StatusPonteiroSubmodulo.PonteiroAtualizado, filhosAtualizados, null);
  }

  private async Task<ResultadoSubmodulo> SincronizarUmAsync(
    SyncProfile perfil, SubmoduloInfo sub, IProgress<LogEvent> log, CancellationToken ct)
  {
    var caminho = Path.Combine(perfil.PastaRaiz, sub.Caminho);

    if (!sub.Inicializado)
    {
      log.Report(new LogEvent(NivelLog.Info, "submódulo não inicializado — inicializando.", Submodulo: sub.Nome));
      var init = await GitProcessRunner.ExecutarAsync(perfil.PastaRaiz, $"submodule update --init \"{sub.Caminho}\"", ct);
      if (!init.Sucesso)
      {
        log.Report(new LogEvent(NivelLog.Erro, $"falha ao inicializar: {init.Erro.Trim()}", Submodulo: sub.Nome));
        return new ResultadoSubmodulo(sub.Nome, StatusSubmodulo.Erro, null, init.Erro);
      }
    }

    var projetoConfig = perfil.Projetos.FirstOrDefault(p => string.Equals(p.Submodulo, sub.Nome, StringComparison.OrdinalIgnoreCase));
    var overrideExplicito = projetoConfig?.Branch is not null;
    var branchDesejada = projetoConfig?.Branch ?? perfil.BranchBase;

    log.Report(new LogEvent(NivelLog.Detalhe, "consultando branches remotas (git ls-remote)…", Submodulo: sub.Nome));
    var resolucao = await _branchResolver.ResolverAsync(caminho, branchDesejada, overrideExplicito, perfil.BranchesFallback, ct);

    if (resolucao.Status == StatusSubmodulo.BranchNaoEncontrada)
    {
      log.Report(new LogEvent(NivelLog.Aviso, $"branch '{branchDesejada}' não encontrada no remoto — submódulo pulado.", Submodulo: sub.Nome));
      return new ResultadoSubmodulo(sub.Nome, StatusSubmodulo.BranchNaoEncontrada, null, branchDesejada);
    }
    if (resolucao.Status == StatusSubmodulo.Erro)
    {
      log.Report(new LogEvent(NivelLog.Erro, "não foi possível consultar o remoto (git ls-remote falhou).", Submodulo: sub.Nome));
      return new ResultadoSubmodulo(sub.Nome, StatusSubmodulo.Erro, null, null);
    }

    if (resolucao.CasouPorCase)
    {
      log.Report(new LogEvent(NivelLog.Aviso, $"branch '{branchDesejada}' não existe no remoto; usando '{resolucao.BranchResolvida}'.", Submodulo: sub.Nome));
    }
    if (resolucao.VeioDeFallback)
    {
      log.Report(new LogEvent(NivelLog.Aviso, $"branch '{branchDesejada}' não existe; usando fallback '{resolucao.BranchResolvida}'.", Submodulo: sub.Nome));
    }

    var branch = resolucao.BranchResolvida!;

    // I1 — nunca descartar trabalho do desenvolvedor: sem alteração não commitada,
    // nunca reset --hard / clean -fd / checkout --force.
    var statusResultado = await GitProcessRunner.ExecutarAsync(caminho, "status --porcelain", ct);
    if (!statusResultado.Sucesso)
    {
      log.Report(new LogEvent(NivelLog.Erro, "não foi possível verificar o status do submódulo.", Submodulo: sub.Nome));
      return new ResultadoSubmodulo(sub.Nome, StatusSubmodulo.Erro, branch, statusResultado.Erro);
    }
    if (!string.IsNullOrWhiteSpace(statusResultado.Saida))
    {
      var caminhosFilhos = LerGitmodules(caminho).Select(d => d.Path).ToList();
      if (!ContemApenasSujeiraDeSubmodulos(statusResultado.Saida, caminhosFilhos))
      {
        var arquivos = string.Join(", ", statusResultado.Saida.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(5));
        log.Report(new LogEvent(NivelLog.Erro, $"working tree com alterações não commitadas — pulado: {arquivos}", Submodulo: sub.Nome));
        return new ResultadoSubmodulo(sub.Nome, StatusSubmodulo.WorkingTreeSujo, branch, statusResultado.Saida);
      }
      log.Report(new LogEvent(NivelLog.Aviso, $"ponteiro(s) de submódulo desatualizado(s) ({string.Join(", ", caminhosFilhos)}) — prosseguindo com a sincronização.", Submodulo: sub.Nome));
    }

    log.Report(new LogEvent(NivelLog.Detalhe, "buscando atualizações do remoto (git fetch)…", Submodulo: sub.Nome));
    var fetch = await GitProcessRunner.ExecutarAsync(caminho, "fetch origin --prune", ct);
    if (!fetch.Sucesso)
    {
      log.Report(new LogEvent(NivelLog.Erro, $"falha no fetch: {fetch.Erro.Trim()}", Submodulo: sub.Nome));
      return new ResultadoSubmodulo(sub.Nome, StatusSubmodulo.Erro, branch, fetch.Erro);
    }

    log.Report(new LogEvent(NivelLog.Detalhe, $"alternando para a branch '{branch}'…", Submodulo: sub.Nome));
    var checkout = await GitProcessRunner.ExecutarAsync(caminho, $"checkout \"{branch}\"", ct);
    if (!checkout.Sucesso)
    {
      log.Report(new LogEvent(NivelLog.Erro, $"falha no checkout de '{branch}': {checkout.Erro.Trim()}", Submodulo: sub.Nome));
      return new ResultadoSubmodulo(sub.Nome, StatusSubmodulo.Erro, branch, checkout.Erro);
    }

    log.Report(new LogEvent(NivelLog.Detalhe, $"atualizando (git pull) em '{branch}'…", Submodulo: sub.Nome));
    var pull = await GitProcessRunner.ExecutarAsync(caminho, $"pull --ff-only origin \"{branch}\"", ct);
    if (!pull.Sucesso)
    {
      log.Report(new LogEvent(NivelLog.Erro, $"divergência de branch (pull --ff-only falhou) em '{branch}': {pull.Erro.Trim()}", Submodulo: sub.Nome));
      return new ResultadoSubmodulo(sub.Nome, StatusSubmodulo.DivergenciaDeBranch, branch, pull.Erro);
    }

    log.Report(new LogEvent(NivelLog.Sucesso, $"sincronizado em '{branch}'.", Submodulo: sub.Nome));
    return new ResultadoSubmodulo(sub.Nome, StatusSubmodulo.Sincronizado, branch, null);
  }

  /// <summary>
  /// true = toda a saída de "status --porcelain" é exclusivamente ponteiro (gitlink) de
  /// submódulo modificado no worktree e não staged (padrão " M &lt;path&gt;") — seguro para
  /// prosseguir com fetch/checkout/pull (o --ff-only continua sendo a rede de segurança
  /// real, I1). false = qualquer outra coisa (staged, rename, conflito, untracked, arquivo
  /// real) — deve bloquear como antes.
  /// </summary>
  private static bool ContemApenasSujeiraDeSubmodulos(string statusPorcelain, IReadOnlyCollection<string> caminhosSubmodulosDeclarados)
  {
    if (string.IsNullOrWhiteSpace(statusPorcelain)) return true;

    var declarados = new HashSet<string>(
      caminhosSubmodulosDeclarados.Select(c => c.Trim().TrimEnd('/')),
      StringComparer.OrdinalIgnoreCase);

    foreach (var linhaBruta in statusPorcelain.Split('\n', StringSplitOptions.RemoveEmptyEntries))
    {
      var linha = linhaBruta.TrimEnd('\r');
      if (linha.Length < 4) return false;
      if (linha[0] != ' ' || linha[1] != 'M') return false;

      var caminho = linha[3..].Trim().TrimEnd('/');
      if (!declarados.Contains(caminho)) return false;
    }
    return true;
  }

  // --- .gitmodules ---

  private sealed record SubmoduloDeclarado(string Nome, string Path, string Url, string? Branch);

  private static readonly Regex RegexCabecalho = new(@"^\[submodule\s+""(?<nome>[^""]+)""\]\s*$", RegexOptions.Compiled);
  private static readonly Regex RegexChaveValor = new(@"^\s*(?<chave>\w+)\s*=\s*(?<valor>.+?)\s*$", RegexOptions.Compiled);

  private static List<SubmoduloDeclarado> LerGitmodules(string pastaRaiz)
  {
    var caminho = Path.Combine(pastaRaiz, ".gitmodules");
    if (!File.Exists(caminho)) return new List<SubmoduloDeclarado>();

    var resultado = new List<SubmoduloDeclarado>();
    string? nomeAtual = null;
    string? pathAtual = null;
    string? urlAtual = null;
    string? branchAtual = null;

    void Fechar()
    {
      if (nomeAtual is not null && pathAtual is not null)
      {
        resultado.Add(new SubmoduloDeclarado(nomeAtual, pathAtual, urlAtual ?? "", branchAtual));
      }
    }

    foreach (var linhaBruta in File.ReadAllLines(caminho))
    {
      var cabecalho = RegexCabecalho.Match(linhaBruta);
      if (cabecalho.Success)
      {
        Fechar();
        nomeAtual = cabecalho.Groups["nome"].Value;
        pathAtual = null;
        urlAtual = null;
        branchAtual = null;
        continue;
      }

      var kv = RegexChaveValor.Match(linhaBruta);
      if (kv.Success && nomeAtual is not null)
      {
        switch (kv.Groups["chave"].Value.ToLowerInvariant())
        {
          case "path": pathAtual = kv.Groups["valor"].Value; break;
          case "url": urlAtual = kv.Groups["valor"].Value; break;
          case "branch": branchAtual = kv.Groups["valor"].Value; break;
        }
      }
    }
    Fechar();

    return resultado;
  }

  // --- git submodule status ---

  private static async Task<Dictionary<string, char>> LerSubmoduleStatusAsync(string pastaRaiz, CancellationToken ct)
  {
    var resultado = new Dictionary<string, char>(StringComparer.OrdinalIgnoreCase);
    var status = await GitProcessRunner.ExecutarAsync(pastaRaiz, "submodule status", ct);
    if (!status.Sucesso) return resultado;

    foreach (var linha in status.Saida.Split('\n', StringSplitOptions.RemoveEmptyEntries))
    {
      if (linha.Length < 2) continue;
      var statusChar = linha[0];
      var resto = linha[1..].Trim();
      var partes = resto.Split(' ', StringSplitOptions.RemoveEmptyEntries);
      if (partes.Length < 2) continue;
      var path = partes[1];
      resultado[path] = statusChar;
    }

    return resultado;
  }
}
