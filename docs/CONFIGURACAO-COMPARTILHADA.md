# Configuração compartilhada do apps-cm (Jira, clientes e projetos)

Fase 0 do [plano de evolução do TestEvidence](../TestEvidence/PLANO-EVOLUCAO.md), mas é do **shell**. Pressupõe a [migração para monorepo](MIGRACAO-MONOREPO.md) já feita. Ela cria um lugar comum onde ficam a conexão com o Jira e o cadastro de clientes e projetos, para qualquer app do bundle usar.

## Decisões

- **Projeto comum `src/AppsCm.Compartilhado`** (class library `net8.0-windows`), referenciado pelo shell e pelos apps que precisarem (`ProjectReference` relativo, ex.: `..\..\src\AppsCm.Compartilhado\AppsCm.Compartilhado.csproj` a partir de `TestEvidence.Models`). Ele não depende do shell nem de WinForms, então cada app continua compilando standalone pelo próprio `.sln` (adicionar o projeto também a esse `.sln`). Os dados ficam em arquivos JSON numa pasta fixa, com formato versionado, porque os apps também rodam standalone, em processos separados.
- **Pasta:** `%LOCALAPPDATA%\apps-cm\compartilhado\`. Ela é irmã das pastas de cada app (`test-evidence`, `test-coverage`...), então funciona igual sob o shell e standalone.
- **Jira:** a conexão (URL, token, headers) é editada **só nas configurações gerais do shell**. Os apps só leem.
- **Clientes e projetos:** o shell tem a tela de cadastro completa, e **qualquer app pode criar** cliente ou projeto (o TestEvidence cria direto na tela de nova evidência). Tudo é gravado no mesmo arquivo comum.

## Arquivos

### `compartilhado\jira.json`
```json
{
  "Versao": 1,
  "Url": "https://jiraproducao.totvs.com.br",
  "TokenProtegido": "AQAAANCMnd8BFdERjHoAwE/Cl+sBAAAA...",
  "HeadersExtras": "User-Agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/86.0.4240.183 Safari/537.36",
  "VerificarSsl": true,
  "TimeoutSegundos": 30
}
```
- `Url` começa **vazia** (Jira desligado) e é preenchida pela QA. Nenhum código tem o host escrito à mão; o `jiraproducao` é só o placeholder da tela.
- O header `origin` (anti-WAF do jiraproducao) **não** é gravado: quem chama o Jira calcula a partir da `Url` (`new Uri(Url).GetLeftPart(UriPartial.Authority)`). Um `origin` informado em `HeadersExtras` prevalece.
- `HeadersExtras` usa pares `k=v` separados por `;` (o User-Agent tem vírgulas).
- `TokenProtegido` = `ProtectedData.Protect(UTF8(token), entropia, DataProtectionScope.CurrentUser)` em base64, com a entropia fixa `UTF8("apps-cm/jira/v1")`. Qualquer app do mesmo usuário do Windows decifra; outro usuário ou outra máquina, não. Falha ao decifrar = "token não configurado", nunca exceção.

### `compartilhado\clientes.json`
```json
{
  "Versao": 1,
  "Clientes": [
    {
      "Id": "6f1c...",
      "Nome": "Cliente X",
      "Ativo": true,
      "CriadoPor": "test-evidence",
      "Projetos": [
        { "Id": "a93e...", "Nome": "RM Folha", "JiraProjetoChave": "CESBHPLATSEGE", "Ativo": true, "CriadoPor": "shell" }
      ]
    }
  ]
}
```
- `Id` é um GUID (`N`). Os apps guardam o **Id** e uma cópia do **nome** (para exibir mesmo se o cliente for excluído do catálogo).
- `JiraProjetoChave` (opcional) liga o projeto a um projeto do Jira: ao importar uma tarefa, o app acha cliente e projeto sozinho pela chave.
- Não existe exclusão física: "excluir" é `Ativo = false`. O item some dos combos, mas os Ids antigos gravados nos apps continuam resolvendo. O shell não sabe quem usa cada cliente, então apagar de verdade quebraria referências.
- Nomes são únicos sem diferenciar maiúsculas nem acentos (cliente entre clientes; projeto dentro do cliente). Um projeto pode ter o mesmo nome em clientes diferentes.

## Projeto `AppsCm.Compartilhado`

Fica em `src/AppsCm.Compartilhado/` (namespace `AppsCm.Compartilhado`). Depende só de `System.Text.Json` e do pacote `System.Security.Cryptography.ProtectedData`. O `IAppModule` continua no shell: só os dados comuns vão para esse projeto.

```csharp
public static class PastaCompartilhada
{
  public static string Caminho => Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "apps-cm", "compartilhado");
}

public sealed class ConexaoJira            // jira.json
{
  public int Versao { get; set; } = 1;
  public string Url { get; set; } = "";
  public string TokenProtegido { get; set; } = "";
  public string HeadersExtras { get; set; } = "User-Agent=...";
  public bool VerificarSsl { get; set; } = true;
  public int TimeoutSegundos { get; set; } = 30;

  public bool Configurada => ...;          // Url válida e token que decifra
  public string? Token();                  // decifra; null se não houver/não decifrar
  public void DefinirToken(string token);  // cifra e guarda
  public IReadOnlyDictionary<string, string> Headers(); // HeadersExtras + origin derivado da Url

  public static ConexaoJira Carregar();    // arquivo ausente/ilegível = new()
  public void Salvar();                    // só o shell chama
}

public sealed class CatalogoClientes       // clientes.json
{
  public IReadOnlyList<Cliente> Clientes { get; }
  public static CatalogoClientes Carregar();

  // Operações de gravação: cada uma relê o arquivo sob lock, aplica a mudança e grava.
  // Nunca grava a lista inteira a partir de uma cópia antiga em memória.
  public static Cliente ObterOuCriarCliente(string nome, string criadoPor);
  public static Projeto ObterOuCriarProjeto(string clienteId, string nome, string criadoPor, string? jiraChave = null);
  public static void Renomear(...); public static void Desativar(...); public static void DefinirChaveJira(...);

  public Cliente? ClientePorId(string id);
  public (Cliente, Projeto)? PorChaveJira(string projetoChave);   // só ativos
  public static event EventHandler? Alterado;   // FileSystemWatcher com debounce de 300 ms
}
```

### Concorrência
O shell e os módulos rodam no mesmo processo, mas os apps também rodam standalone, em processos separados. Por isso:
- **Lock entre processos:** `Mutex` nomeado `Local\apps-cm-compartilhado-clientes` (e `...-jira`) em volta de cada leitura-modificação-gravação, com timeout de 5 s. Sem conseguir o lock, a operação falha com mensagem e não grava.
- **Gravação atômica:** grava em `clientes.json.tmp` e depois `File.Replace` (ou `File.Move(..., overwrite: true)` na primeira vez), para que um leitor nunca veja um JSON pela metade.
- **Leitura tolerante:** arquivo ausente = catálogo vazio; JSON inválido = catálogo vazio + backup `clientes.json.invalido-{data}` antes da próxima gravação (nunca sobrescrever silenciosamente o que não deu para ler).
- `Versao` maior do que a conhecida: ler o que der e **não gravar** (avisar "configuração criada por uma versão mais nova do apps-cm").
- `Alterado`: `FileSystemWatcher` na pasta, filtro `*.json`, com debounce, disparado na thread de UI pelo consumidor (`BeginInvoke`). Os combos abertos se atualizam sozinhos quando outro app cria um cliente.

## Shell: tela de configurações gerais

- Na aba fixa **Apps**, um tile a mais no fim, **Configurações** (ícone de engrenagem do sistema, `SystemIcons`/imagem embutida), que abre `ConfiguracoesGeraisForm` (modal). Ele não é um `IAppModule`; é tratado à parte no `CriarAbaApps`.
- `ConfiguracoesGeraisForm`, com duas abas:
  - **Jira:** URL (placeholder `ex.: https://jiraproducao.totvs.com.br`, validada com `Uri.TryCreate` absoluto http/https, sem `/` final), Token (`UseSystemPasswordChar`; com token salvo mostra "(token salvo, digite para trocar)"), Headers extras (um por linha na tela), Verificar SSL, Timeout, e o botão **Testar conexão** (`GET {Url}/rest/api/2/myself` com Bearer + headers, que mostra "Conectado como {displayName}" ou o erro: 401/403 token, 404 URL errada, HTML no corpo = WAF/headers). O teste usa o `HttpClient` do próprio shell; não precisa de cliente Jira completo.
  - **Clientes e projetos:** `TreeView` cliente → projetos (inativos em cinza, com o filtro "mostrar inativos"), botões Novo cliente, Novo projeto, Renomear, Desativar/Reativar e Chave do Jira (no projeto). Mostra quem criou (`CriadoPor`). Atualiza sozinha no `Alterado`.
- Nada de token em log, mensagem ou exceção.

## Aceite
- [ ] Sem os arquivos, o shell abre normalmente; a tela mostra o Jira desligado e o catálogo vazio.
- [ ] Testar conexão com token válido mostra o nome; com token inválido, a mensagem de credencial (sem vazar o token).
- [ ] `jira.json` não tem o token legível; copiar o arquivo para outro usuário resulta em "token não configurado".
- [ ] Criar um cliente no TestEvidence (aberto numa aba) aparece na tela do shell sem reabrir, e vice-versa.
- [ ] Dois processos (shell + TestEvidence standalone) criando clientes ao mesmo tempo: nenhum se perde.
- [ ] `clientes.json` corrompido à mão: app abre, avisa e guarda o backup antes de gravar de novo.
- [ ] `grep -i jiraproducao` no C# só encontra o placeholder.
