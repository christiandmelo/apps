# Fase 3: subtarefa de defeito no Jira com a evidência

Terceira fase do [plano de evolução](../PLANO-EVOLUCAO.md). Depende da [fase 2](FASE-2-JIRA-ZEPHYR.md) (`JiraClient`, `JiraChave` na sessão). A conexão com o Jira continua sendo a do apps-cm ([configuração compartilhada](../../docs/CONFIGURACAO-COMPARTILHADA.md)); aqui só entram opções do próprio TestEvidence.

Regras do `CLAUDE.md` valem: camadas UI → Services → Models, pt-BR, 2 espaços, services sem `MessageBox`, falha de rede nunca derruba o app nem perde a sessão.

## Decisões

- Tipo da subtarefa: **`Defeito (Sub-tarefa)`** (configurável).
- **Campos obrigatórios são descobertos na tentativa**, como no Worker - IA (`debt_closer.py`): tenta criar; se o Jira recusar com 400, o app lê quais campos faltaram, pergunta à QA só esses e tenta de novo. As respostas ficam salvas por projeto para a próxima vez.
- **Prints: a QA marca na hora**, com os da etapa "Resultado" (e o último "Passo N") pré-marcados.
- O defeito é subtarefa da **tarefa** (`JiraChave`), não do caso de teste do Zephyr.

## Models

`OpcoesJiraEvidencia` (da fase 2, em `ConfiguracaoApp.Jira`, no `configs.json` do TestEvidence), campos novos. São opções do TestEvidence, não da conexão, por isso não vão para o `jira.json` compartilhado:
```csharp
public string TipoSubtarefaDefeito { get; set; } = "Defeito (Sub-tarefa)";
public List<string> LabelsDefeito { get; set; } = new() { "TestEvidence" };
public bool AnexarDocumentoWord { get; set; } = true;
// chave = "{projectKey}|{issuetype}"; valor = JSON do trecho de "fields" que a QA já respondeu
public Dictionary<string, string> CamposSalvosDefeito { get; set; } = new();
```

`SessaoEvidencia.Defeitos`: `List<DefeitoRegistrado>`.
```csharp
public sealed class DefeitoRegistrado
{
  public string Chave { get; set; } = "";
  public string Url { get; set; } = "";
  public DateTime CriadoEm { get; set; }
  public List<string> ArquivosPendentes { get; set; } = new();  // caminhos relativos à pasta da sessão
  public List<string> ArquivosEnviados { get; set; } = new();
}
```

## Services

### `JiraClient`, métodos novos
- `Task<IssueCriada> CriarIssueAsync(JsonObject fields)` → `POST /rest/api/2/issue` com `{"fields": ...}`; devolve `key`, `id`, `self`.
- `Task AnexarAsync(string chave, string caminho, string nomeNoJira)` → `POST /rest/api/2/issue/{chave}/attachments`, `MultipartFormDataContent` com o campo **`file`** e o header **`X-Atlassian-Token: no-check`** (sem ele o Jira responde 403). Um arquivo por chamada, para o reenvio ser granular.
- `Task<IReadOnlyList<CampoMeta>> CreateMetaAsync(string projectKey, string issuetype)`:
  - Primeiro `GET /rest/api/2/issue/createmeta/{projectKey}/issuetypes` (Jira 8.4+/DC) para achar o `id` do tipo e depois `GET .../issuetypes/{id}` para os campos.
  - Fallback para Jira antigo: `GET /rest/api/2/issue/createmeta?projectKeys=X&issuetypeNames=Y&expand=projects.issuetypes.fields`.
  - `CampoMeta { Id, Nome, Obrigatorio, Tipo (schema.type), ItemTipo (schema.items), Custom (schema.custom), ValoresPermitidos: List<(Id, Texto)> }`.
- `Task<IReadOnlyList<string>> TiposSubtarefaAsync(string projectKey)` → para a `ConfigForm` listar os tipos existentes (os com `subtask == true`).

### `Jira/ModeloDefeito` (funções puras)
- `Resumo(sessao)` → `"Defeito - {Título} - {nome do caso ou Cenário}"`, máx. 255 caracteres.
- `Descricao(sessao, evidenciasMarcadas, nomesNoJira)` → wiki markup:
  ```
  h3. Caso de teste
  {CasoTesteChave} - {Cenário}

  h3. Passos executados
  # {Passos[i].Descricao}  (ou, sem Passos, as legendas das evidências em ordem)

  h3. Resultado esperado
  {ResultadoEsperado do passo que falhou, se houver}

  h3. Resultado obtido
  {Observações da sessão}

  h3. Ambiente
  ||Ambiente||Testador||Data||
  |{ambiente}|{testador}|{data}|

  h3. Evidências
  *{Etapa}* - {Legenda}
  !evidencia-001.png|thumbnail!
  ...
  ```
  Escapar caracteres de wiki nas legendas (`{`, `}`, `[`, `]`, `|`, `!`, `*` no início de linha) para não quebrar a formatação.
- Nome dos anexos: `evidencia-{Ordem:000}.png`, para os `!...!` resolverem sem depender do nome do arquivo local.

### `Jira/RegistradorDefeito`
`Task<ResultadoRegistro> RegistrarAsync(sessao, PedidoDefeito pedido, IProgress<string> progresso, CancellationToken ct)`, onde `PedidoDefeito = { Resumo, Descricao, Evidencias marcadas, AnexarDocx, CamposExtras (JsonObject) }`.

1. Monta `fields`: `project.key` (prefixo da `JiraChave`), `parent.key = JiraChave`, `issuetype.name`, `summary`, `description`, `labels`, mais os `CamposSalvosDefeito` do projeto/tipo e os `CamposExtras` do pedido.
2. `CriarIssueAsync`.
   - **400 com `errors`** → devolve `ResultadoRegistro.FaltamCampos(errors)` **sem** lançar; a UI resolve (abaixo) e chama de novo.
   - Outros erros → `JiraException` normal.
3. **Logo após criar**: adiciona `DefeitoRegistrado` com todos os arquivos em `ArquivosPendentes` e `SessaoRepository.Salvar(sessao)`. Assim a chave nunca se perde, mesmo se o app cair nos anexos.
4. Para cada pendente: `AnexarAsync` → move de `ArquivosPendentes` para `ArquivosEnviados` → `Salvar`. Falha de um anexo não aborta os outros; no fim devolve `ResultadoRegistro.Parcial(pendentes)`.
5. `ReenviarPendentesAsync(sessao, defeito)` → repete o passo 4 só com os pendentes.

O `.docx` (se `AnexarDocx`) é gerado antes do passo 1 pelo `DocumentoWordService` numa pasta temporária da sessão e anexado com o nome `Evidencia_{tarefa}.docx`.

## Campos obrigatórios descobertos na tentativa

Fluxo quando o create volta 400 (padrão do `debt_closer.py` do Worker - IA, que lê o `required` da tela e para com o nome do campo que não conhece):

1. `errors` vem como `{"customfield_10500": "Campo X é obrigatório.", "components": "..."}`. Separar: campos que o app sabe preencher vs. desconhecidos.
2. `CreateMetaAsync` → para cada campo de `errors`, pegar `CampoMeta`.
3. `CamposObrigatoriosForm` monta um controle por campo, conforme o tipo:
   | schema | Controle | Valor enviado |
   |---|---|---|
   | `string` | `TextBox` (multilinha se `textarea`) | `"texto"` |
   | `number` | `NumericUpDown` | `123` |
   | `date` / `datetime` | `DateTimePicker` | `"2026-11-23"` / ISO 8601 |
   | `option` (select) | `ComboBox` com `ValoresPermitidos` | `{"id": "..."}` |
   | `array` de `option`/`component`/`version` | `CheckedListBox` | `[{"id": "..."}]` |
   | `priority` | `ComboBox` | `{"id": "..."}` |
   | `user` | `TextBox` (login) | `{"name": "login"}` |
   | `option-with-child` (cascata) | dois `ComboBox` | `{"id": "pai", "child": {"id": "filho"}}` |
   | outro / sem `CampoMeta` | não suportado | — |
4. Campo de tipo não suportado ou que não aparece no createmeta: o form mostra o nome do campo e a mensagem do Jira e **não** tenta de novo ("O Jira exige o campo '{nome}', que o TestEvidence ainda não sabe preencher. Crie o defeito manualmente ou peça a inclusão desse tipo de campo."). Esse é o caso provável do **Catálogo TOTVS** (Produto/Módulo/Rotina), que no Worker - IA só é gravável pela rota `/rest/pluginTotvs/1.0/issue/catalog` depois da criação; se aparecer, vira a fase 3.1.
5. Checkbox "Lembrar estes valores para o projeto {X}" (marcado por padrão) → grava em `CamposSalvosDefeito["{projeto}|{tipo}"]`.
6. Nova tentativa com os valores. Repetir no máximo 3 vezes (o Jira às vezes revela campos em sequência); depois disso, mostrar os erros e parar.

Na `ConfigForm` do TestEvidence (grupo Jira): lista dos projetos com campos salvos e botão "Esquecer campos salvos", para quando a configuração do projeto mudar no Jira.

## UI

### Entradas
- `RevisaoForm`: botão **Registrar defeito** (habilitado se `JiraChave` preenchida e Jira configurado). Ao salvar com resultado **Reprovado** e sem defeito registrado: "Registrar um defeito no Jira para esta evidência?" (Sim abre o `DefeitoForm`).
- `MainForm`: ação **Registrar defeito** para a sessão selecionada, com a mesma regra de habilitação. Coluna nova **Defeitos** com as chaves (ex.: `X-123, X-130`).
- Sessão que já tem defeito: antes de abrir, perguntar "Esta evidência já tem o defeito X-123. Criar outro?". Isso protege contra duplicidade por duplo clique.

### `DefeitoForm`
- Resumo (`TextBox`, contador de caracteres, máx. 255).
- Descrição (`TextBox` multilinha, fonte monoespaçada), pré-preenchida pelo `ModeloDefeito`; botão "Restaurar modelo".
- Prints: `ListView` com miniaturas (`LargeIcon`, `ImageList` 128×) e checkbox; pré-marcados os da etapa "Resultado" e os do último "Passo N"; botões "Marcar todos"/"Desmarcar todos". Ao mudar a seleção, a seção "Evidências" da descrição é regenerada (só essa seção, preservando o que a QA editou no resto; delimitar com `h3. Evidências` até o fim).
- Checkbox "Anexar o documento Word".
- **Criar defeito** → progresso na própria janela ("Criando subtarefa...", "Enviando 2 de 5..."), botões desabilitados, `await`.
- Sucesso: "Defeito X-123 criado" com link (abre `{Url}/browse/X-123` no navegador) e botão "Copiar chave". Parcial: lista dos anexos que falharam e botão **Reenviar anexos pendentes**.
- Fechar o form no meio do envio: perguntar e cancelar via `CancellationToken`; o que já foi enviado fica registrado.

### `DocumentoWordService`
- Seção final "Defeitos registrados" com as chaves como hyperlink, quando houver.

## Testes (em `TestEvidence.Tests`, criado na fase 2)
- `ModeloDefeito.Resumo` (limite de 255) e `Descricao` (ordem dos passos, escape de wiki, nomes `evidencia-NNN.png`).
- Conversão `CampoMeta` + valor digitado → JSON do campo, para cada linha da tabela de tipos.
- Leitura do corpo de erro 400 (`errors`) → lista de campos faltantes.

## Aceite
- [ ] Defeito criado como subtarefa da tarefa certa, tipo `Defeito (Sub-tarefa)`, com os prints marcados aparecendo inline na descrição.
- [ ] Projeto com campo obrigatório: o app pergunta só esse campo, cria na segunda tentativa e, no próximo defeito do mesmo projeto, cria direto.
- [ ] Campo obrigatório de tipo não suportado: mensagem com o nome do campo, sem loop de tentativas.
- [ ] Derrubar a rede depois do create: a sessão guarda a chave, e "Reenviar anexos pendentes" completa sem criar outro defeito.
- [ ] Clicar duas vezes em Registrar defeito não cria dois defeitos.
- [ ] Token não aparece em nenhuma mensagem, log ou na descrição do defeito.
- [ ] Documento Word lista os defeitos registrados.
- [ ] `dotnet build` do `TestEvidence.sln` e do `AppsCm.sln` sem erros novos; `dotnet test TestEvidence.sln` passando.
