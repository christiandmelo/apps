# Fase 2: conexão com o Jira e importação do caso de teste (Zephyr Scale)

Segunda fase do [plano de evolução](../PLANO-EVOLUCAO.md). Depende da [fase 1](FASE-1-AGRUPAR-FILTRAR.md) (cliente/projeto) e da [configuração compartilhada](../../docs/CONFIGURACAO-COMPARTILHADA.md) do shell (conexão com o Jira). Sem Jira configurado, o app continua 100% manual.

Regras do `CLAUDE.md` valem: camadas UI → Services → Models, pt-BR, 2 espaços, services sem `MessageBox`, falha de rede nunca derruba o app nem perde a sessão.

## Decisões

- **A conexão com o Jira (URL, token, headers) é a do apps-cm**, gravada em `%LOCALAPPDATA%\apps-cm\compartilhado\jira.json` e editada só nas configurações gerais do shell. O TestEvidence **só lê**, pela `ConexaoJira` do projeto `AppsCm.Compartilhado`. A URL é configurável: o time usa hoje o `https://jiraproducao.totvs.com.br` (on-premise/Data Center, o mesmo do Worker - IA), mas nenhum trecho do código pode montar URL ou header com esse host escrito à mão; tudo deriva de `ConexaoJira.Url`.
- Os cenários vêm do **Zephyr Scale** (casos de teste com chave `PROJ-T123`, vinculados à tarefa).
- **Uma sessão = um caso de teste.** Tarefa com 3 casos → a QA escolhe 1 por evidência.
- **Cliente não vem do Jira.** Ao importar a tarefa, o app procura no catálogo compartilhado um projeto com `JiraProjetoChave` igual a `fields.project.key`; achou, preenche Cliente e Projeto sozinho. Não achou, a QA escolhe ou digita o cliente, o nome do projeto vem sugerido com `fields.project.name`, e ao confirmar o projeto é criado no catálogo **já com a chave do Jira**. Assim, a próxima tarefa do mesmo projeto preenche tudo automaticamente.

## Referência: Worker - IA

`C:\Projetos\Worker - IA\automation\tools\jira_tool.py`. Reaproveitar a lógica, portando para C#:

- **Autenticação:** `Authorization: Bearer <PAT>` (Personal Access Token do Jira, gerado em Perfil → Personal Access Tokens). A API do Zephyr Scale no Data Center aceita o mesmo token.
- **Headers anti-WAF obrigatórios** no jiraproducao (sem eles o WAF devolve 403/HTML):
  ```
  origin: {esquema + host da URL configurada}     ex.: https://jiraproducao.totvs.com.br
  User-Agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/86.0.4240.183 Safari/537.36
  Accept: application/json
  ```
  O `origin` é **calculado a partir da `Url` configurada** (`new Uri(Url).GetLeftPart(UriPartial.Authority)`), e não fica gravado. Se a QA informar um `origin` em `HeadersExtras`, esse valor prevalece.
- **Erros** (`_http_error` e `_jira_reason`): 401/403 → credencial; 404 → não encontrado; 429/5xx/timeout → indisponível; outros 4xx → junta `errorMessages` e `errors{campo: msg}` do corpo na mensagem.
- Resposta que não é JSON (página HTML do WAF/login) → erro "Resposta inesperada do Jira (verifique URL e headers)", nunca exceção de parse crua.

## Etapa 0: descoberta (fazer antes de codar a UI)

A API do Zephyr Scale varia por versão do plugin. Antes de tudo, com uma tarefa real que tenha casos vinculados, validar com o `JiraClient` (ou com `curl`/Postman usando os headers acima):

1. `GET /rest/api/2/myself` → 200 com `displayName`.
2. `GET /rest/api/2/issue/{TAREFA}?fields=summary,project,description,status,issuetype` → 200.
3. `GET /rest/atm/1.0/testcase/search?query=issueKeys IN ("{TAREFA}")&fields=key,name,objective,precondition,status,testScript` → lista de casos vinculados.
4. `GET /rest/atm/1.0/testcase/{PROJ-T123}` → caso com `testScript`.

Se o passo 3 não funcionar na versão instalada, plano B: `GET /rest/tests/1.0/issuelink/{issueId}/testcases` (API interna usada pelo painel da issue; `issueId` numérico vem do passo 2) para obter as chaves e depois o passo 4 para cada uma. Registrar no código qual rota funcionou, com comentário.

Guardar as respostas reais (sem o token) em `docs/exemplos-jira/` para servirem de fixture de teste.

## Models

Conexão: `ConexaoJira` do `AppsCm.Compartilhado` (já referenciado na fase 1). Ela já resolve o token (DPAPI), os headers e o `origin` derivado da URL. O TestEvidence não tem campo de URL nem de token próprio.

Opções que são só do TestEvidence, em `ConfiguracaoApp.Jira` (padrão `new()`):
```csharp
public sealed class OpcoesJiraEvidencia
{
  public bool UsarPassosComoEtapas { get; set; } = true;
}
```

`SessaoEvidencia`, campos novos:
```csharp
public string JiraChave { get; set; } = "";         // tarefa, ex.: CESBHPLATSEGE-2295
public string JiraUrlIssue { get; set; } = "";      // {Url}/browse/{chave}
public string CasoTesteChave { get; set; } = "";    // Zephyr, ex.: CESBHPLATSEGE-T45
public List<PassoCasoTeste> Passos { get; set; } = new();
public DateTime? JiraImportadoEm { get; set; }
```

`PassoCasoTeste` (novo): `int Ordem`, `string Descricao`, `string DadosTeste`, `string ResultadoEsperado`.

## Segurança do token

- A cifra e a decifra (DPAPI, `CurrentUser`, entropia do contrato) ficam na `ConexaoJira`. O pacote `System.Security.Cryptography.ProtectedData` fica no `AppsCm.Compartilhado`.
- Token que não decifra (configuração de outro usuário/máquina) = "Jira não configurado", sem exceção.
- O token **nunca** aparece em log, mensagem de erro, documento, `ToString()` ou exceção. O `JiraClient` recebe o token já decifrado e não o expõe.

## Services

### `Jira/JiraClient`
- Construído a partir de uma `ConexaoJira` (`JiraClient.De(conexao)`). Um `HttpClient` por conexão, reaproveitado e recriado quando o `jira.json` muda (o evento de alteração da pasta compartilhada, ou a comparação de `Url`+token a cada uso). `HttpClientHandler` com `ServerCertificateCustomValidationCallback` liberado **somente** se `VerificarSsl == false`.
- Métodos `async` com `CancellationToken` e o timeout da config:
  - `Task<UsuarioJira> MyselfAsync()`
  - `Task<IssueJira> ObterIssueAsync(string chave)`
  - `Task<IReadOnlyList<CasoTesteResumo>> CasosDaIssueAsync(string chave, long issueId)` (rota da etapa 0, com fallback)
  - `Task<CasoTeste> ObterCasoTesteAsync(string chaveCaso)`
  - (fase 3 adiciona `CriarIssueAsync`, `AnexarAsync`, `CreateMetaAsync`.)
- Falhas viram `JiraException { Tipo: Credencial | NaoEncontrado | Indisponivel | Validacao | RespostaInvalida; Mensagem amigável; Detalhe (corpo do erro, sem headers) }`.
- Headers vêm de `ConexaoJira.Headers()` (`HeadersExtras` + `origin` derivado) + `Authorization: Bearer` + `Accept: application/json`.

### `Jira/ImportadorTarefa`
`Task<TarefaImportada> ImportarAsync(string chave)`:
1. `ObterIssueAsync` → título (`summary`), projeto (`project.name`), descrição, `id`.
2. `CasosDaIssueAsync` → lista de casos (chave, nome, status).
3. Devolve `TarefaImportada { Chave, Titulo, Projeto, Descricao, Url, Casos }`.

`Task<CasoTeste> DetalharCasoAsync(string chaveCaso)` → `CasoTeste { Chave, Nome, Objetivo, PreCondicao, Passos }`:
- `testScript.type == "STEP_BY_STEP"` → `steps[]` (`description`, `testData`, `expectedResult`) → `Passos`.
- `PLAIN_TEXT` → `testScript.text` vira um passo único.
- `BDD` → cada linha `Given/When/Then/Dado/Quando/Então` vira um passo.

Também aceitar que a QA digite direto a chave do caso (`...-T123`): `ImportadorTarefa` detecta pelo padrão `^[A-Z][A-Z0-9_]*-T\d+$`, busca o caso e, se ele tiver `issueLinks`, usa o primeiro como tarefa.

### `Jira/TextoJira` (funções puras)
- `HtmlParaTexto`: o Zephyr Scale guarda os textos dos passos em **HTML** (`<p>`, `<br>`, `<ul><li>`, `<strong>`, entidades `&nbsp;` `&amp;`). Converter para texto: `<br>`/`</p>` → quebra, `<li>` → "- ", remover demais tags, `WebUtility.HtmlDecode`, colapsar linhas vazias.
- `WikiParaTexto`: a descrição da issue vem em wiki markup do Jira: tirar `h1.`..`h6.`, `*negrito*`, `_itálico_`, `{code}`/`{noformat}`/`{panel}`, `[texto|url]` → `texto`, `!imagem!` → vazio, `||`/`|` de tabela → " | ".

## UI

### `ConfigForm`
Grupo **Jira**, só leitura para a conexão:
- Status: "Conectado a {Url}" (com `ConexaoJira.Configurada`) ou "Jira não configurado. Configure em apps-cm → Configurações." Sob o shell, o texto vira um link que abre as configurações gerais, se o shell expuser essa ação (não é obrigatório nesta fase).
- Botão **Testar conexão** → `MyselfAsync` → "Conectado como {displayName}" ou a mensagem do erro. Testar aqui ajuda a QA a ver que o TestEvidence enxerga a mesma configuração do shell.
- Opção própria: "Usar passos do caso de teste como etapas".

### Nova evidência (`CabecalhoSessaoPanel` + `NovaSessaoForm`)
- Botão **Buscar no Jira** à direita do campo Tarefa, visível só quando `ConexaoJira.Carregar().Configurada` (relido ao abrir o form, para pegar uma configuração feita no shell com o app aberto). Enter no campo Tarefa também dispara.
- Durante a busca: botão e campo Tarefa desabilitados, `UseWaitCursor = true`, `await` (UI não trava). Repetir clique não dispara duas buscas.
- Resultado:
  - Cliente/Projeto conforme as Decisões: projeto do catálogo com `JiraProjetoChave == fields.project.key` → preenche os dois; senão, sugere o nome do projeto do Jira e, ao confirmar, cria o projeto no catálogo com essa chave (sob o cliente escolhido).
  - Preenche **Título** e **Requisito** (descrição convertida, até ~2000 caracteres). Campo que a QA já digitou é sobrescrito só depois de confirmação ("Substituir os campos já preenchidos pelos do Jira?").
  - Casos de teste:
    - 0 casos → aviso "Nenhum caso de teste do Zephyr vinculado a esta tarefa"; segue manual.
    - 1 caso → usa direto.
    - Mais de 1 → `EscolherCasoTesteForm`: lista (chave, nome, status) à esquerda e, à direita, o detalhe do selecionado (objetivo, pré-condição e passos), carregado sob demanda com `DetalharCasoAsync`. Duplo clique ou "Usar este caso".
  - Caso escolhido → **Cenário / Pré-condições** = `"{chave} - {nome}"` + objetivo + pré-condição; `Passos` gravados na sessão.
  - Se `UsarPassosComoEtapas` e o caso tem passos: **Etapas** = `"Passo 1"`, `"Passo 2"`, ... + `"Resultado"` no final (nome curto para caber na barra flutuante; o texto completo do passo fica em `Passos`). Senão, mantém as etapas padrão.
- A sessão grava `JiraChave`, `JiraUrlIssue`, `CasoTesteChave`, `JiraImportadoEm`.

### Barra flutuante
- Quando a etapa atual é `"Passo N"` e existe `Passos[N-1]`, o tooltip da etapa mostra a descrição e o resultado esperado do passo (ajuda a QA a saber o que capturar). Nada além disso muda na barra.

### Revisão
- Botão **Atualizar do Jira** (quando há `JiraChave`): rebusca título e detalhe do caso, com a mesma confirmação de sobrescrita. Não mexe nas evidências.

### `DocumentoWordService`
- Tarefa vira hyperlink para `JiraUrlIssue` quando houver; linha "Caso de teste" com `CasoTesteChave`.
- Se houver `Passos`: tabela "Passos do caso de teste" (Nº, Descrição, Dados, Resultado esperado) antes das evidências, e os prints da etapa "Passo N" ficam sob o título do respectivo passo.

## Testes

Criar `TestEvidence.Tests` (xUnit, `net8.0-windows`) só para as funções puras: `TextoJira.HtmlParaTexto`, `TextoJira.WikiParaTexto`, o parser de `testScript` (STEP_BY_STEP, PLAIN_TEXT, BDD) usando os JSONs reais de `docs/exemplos-jira/`, e o vínculo projeto do Jira → catálogo (`PorChaveJira`) com um catálogo em pasta temporária. Incluir no `TestEvidence.sln`, **não** no `AppsCm.sln`. Atualizar a linha "Sem projetos de teste no MVP" do `CLAUDE.md` para `dotnet test TestEvidence.sln`.

## Aceite
- [ ] Etapa 0 documentada no código (qual rota do Zephyr funcionou).
- [ ] `grep -i jiraproducao` no código C# não encontra nada (só no placeholder da tela e nos docs); trocar a URL na configuração muda todas as chamadas, links e o `origin`.
- [ ] Testar conexão (no TestEvidence) com token válido mostra o nome; com token inválido mostra "token inválido ou sem permissão" sem vazar o token.
- [ ] O TestEvidence não grava URL nem token em nenhum arquivo próprio; configurar o Jira no shell com o TestEvidence aberto faz o botão Buscar no Jira aparecer na próxima nova evidência.
- [ ] Primeira tarefa de um projeto do Jira cria o projeto no catálogo com a chave; a segunda tarefa do mesmo projeto preenche Cliente e Projeto sozinha.
- [ ] Tarefa com vários casos → escolhe 1 → Título, Projeto, Cenário e etapas "Passo N" preenchidos.
- [ ] Digitar direto a chave `...-T123` também funciona.
- [ ] Sem rede/VPN: mensagem clara em até `TimeoutSegundos`, e a evidência segue manual.
- [ ] Documento mostra o link da tarefa e a tabela de passos.
- [ ] `dotnet build` do `TestEvidence.sln` e do `AppsCm.sln` sem erros novos; `dotnet test TestEvidence.sln` passando.
