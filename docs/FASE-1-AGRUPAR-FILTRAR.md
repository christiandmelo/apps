# Fase 1: agrupar por cliente e projeto, com filtros

Primeira fase do TestEvidence no [plano de evolução](../PLANO-EVOLUCAO.md). Depende da **fase 0 do shell**, a [configuração compartilhada](../../docs/CONFIGURACAO-COMPARTILHADA.md) (catálogo de clientes e projetos). Não depende do Jira.

Regras do `CLAUDE.md` valem: camadas UI → Services → Models, pt-BR, 2 espaços, services sem `MessageBox`, falha de I/O nunca derruba o app nem perde a sessão.

## Decisões

- Clientes e projetos vêm do **catálogo compartilhado** (`%LOCALAPPDATA%\apps-cm\compartilhado\clientes.json`). O TestEvidence **pode criar** cliente e projeto direto na tela de nova evidência, e eles ficam gravados no catálogo comum, disponíveis para os outros apps e para a tela de configurações do shell.
- O Cliente é sempre escolhido ou digitado pela QA (não vem do Jira). Na fase 2, o Projeto (e com ele o Cliente) pode ser achado pela chave do projeto no Jira, se o projeto do catálogo tiver `JiraProjetoChave`.
- Sessões antigas não são migradas: sem cliente, caem no grupo "(sem cliente)".

## Models

### Projeto comum
`TestEvidence.Models` passa a referenciar `..\..\src\AppsCm.Compartilhado\AppsCm.Compartilhado.csproj` (`CatalogoClientes`, `Cliente`, `Projeto`; a `ConexaoJira` entra em uso na fase 2). Adicionar esse projeto também ao `TestEvidence.sln`, para o build standalone continuar funcionando.

### `SessaoEvidencia`
```csharp
public string ClienteId { get; set; } = "";
public string Cliente { get; set; } = "";     // cópia do nome no momento em que foi escolhido
public string ProjetoId { get; set; } = "";
public string Projeto { get; set; } = "";
```
- Exibição e agrupamento usam o **nome atual do catálogo** quando o Id resolve (rename no shell reflete aqui); senão, a cópia gravada na sessão.
- Ao abrir e salvar uma sessão cujo nome mudou no catálogo, atualizar a cópia.

### `ConfiguracaoApp`
```csharp
public bool AgruparLista { get; set; } = true;
public FiltroLista Filtro { get; set; } = new();
public string UltimoClienteId { get; set; } = "";
```
Nada de lista de clientes aqui: o histórico é o próprio catálogo.

`FiltroLista` (novo, em Models):
```csharp
public sealed class FiltroLista
{
  public string ClienteId { get; set; } = "";   // vazio = todos; "-" = sem cliente
  public string ProjetoId { get; set; } = "";   // vazio = todos
  public StatusSessao? Status { get; set; }
  public ResultadoTeste? Resultado { get; set; }
  public string Texto { get; set; } = "";       // busca em Tarefa, Título, Cenário, Cliente e Projeto

  public bool Aceita(SessaoEvidencia s, Func<SessaoEvidencia, (string cliente, string projeto)> nomes) { ... }
}
```
Comparação de texto sem diferenciar maiúsculas nem acentos: `CultureInfo.GetCultureInfo("pt-BR").CompareInfo.IndexOf(..., CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace)`. O `Carregar()` precisa tolerar `Filtro` nulo vindo de JSON antigo (`??= new()`).

## UI

### `CabecalhoSessaoPanel`
- Duas linhas novas logo abaixo de **Tarefa**: **Cliente** e **Projeto**, `ComboBox` editáveis (`DropDown`, `AutoCompleteMode = SuggestAppend`) com os itens **ativos** do catálogo.
- Projeto lista só os projetos do cliente escolhido; sem cliente escolhido, fica desabilitado.
- Texto digitado que não existe no catálogo aparece com a dica "(novo, será criado)" ao lado. **A criação acontece só ao confirmar** a nova evidência ou salvar a revisão, nunca ao sair do campo (para não sujar o catálogo com algo digitado pela metade).
- Ao confirmar: `CatalogoClientes.ObterOuCriarCliente(nome, "test-evidence")` e depois `ObterOuCriarProjeto(cliente.Id, nome, "test-evidence")`. O "obter ou criar" acha o existente sem diferenciar maiúsculas nem acentos, então "cliente x" reaproveita "Cliente X". Grava `ClienteId`/`ProjetoId` e os nomes na sessão.
- Falha ao gravar no catálogo (lock ocupado, disco): a sessão é criada mesmo assim, só com os **nomes** (Ids vazios), e aparece um aviso. Na próxima gravação da sessão, o app tenta vincular de novo.
- Assinar `CatalogoClientes.Alterado` enquanto o form estiver aberto, para que um cliente criado no shell apareça no combo sem fechar a tela. Desassinar no `Dispose`.
- Pré-selecionar `UltimoClienteId` na nova evidência.
- Aumentar o `ClientSize` do `NovaSessaoForm` para caber as duas linhas (+64 px).

### `MainForm`
Layout de cima para baixo: topo (botões) → faixa de "em andamento" → **barra de filtros (nova)** → lista → ações.

Barra de filtros (`FlowLayoutPanel`, altura ~36):
| Controle | Conteúdo |
|---|---|
| Cliente | `DropDownList`: "(Todos)" + clientes que **aparecem nas sessões** (não o catálogo inteiro), ordenados; "(sem cliente)" se houver |
| Projeto | "(Todos)" + projetos das sessões do cliente escolhido |
| Status | (Todos), Em andamento/Interrompida, Pausada, Finalizada |
| Resultado | (Todos), Pendente, Aprovado, Reprovado, Com ressalvas |
| Buscar | `TextBox` com placeholder "Buscar..."; filtra com `Timer` de 300 ms |
| Agrupar por cliente/projeto | `CheckBox` |
| Limpar | `Button` |

Lista:
- Colunas novas **Cliente** e **Projeto** depois de Tarefa.
- `Atualizar()`: `_repo.Listar()` → `Where(Filtro.Aceita)` → itens. Os combos de filtro são repopulados com base em **todas** as sessões, preservando o valor selecionado.
- Agrupamento ligado: `ShowGroups = true`, com um `ListViewGroup` por cliente/projeto (chave = Ids; texto = nomes), cabeçalho com contagem (`"Cliente X / RM Folha (12)"`) e `CollapsedState = Expanded` para poder recolher. Grupos ordenados por nome, "(sem cliente)" por último; itens por `Inicio` decrescente.
- Guardar em memória os grupos recolhidos e reaplicar no `Atualizar()`, senão eles reabrem a cada print capturado.
- `CatalogoClientes.Alterado` (rename no shell) → `Atualizar()`.
- A faixa "Evidência em andamento" ignora o filtro.
- Com filtro ativo, mostrar "Mostrando X de Y".
- Mudança de filtro/agrupamento → `_config.Salvar()` (o Timer da busca evita gravar a cada tecla).

### `RevisaoForm`
Cliente e Projeto editáveis pelo mesmo `CabecalhoSessaoPanel`, com a mesma regra de criação ao salvar.

### Documento
- `DocumentoWordService` (`Linha(...)`, ~linha 150): Cliente e Projeto logo após Tarefa, só quando preenchidos.
- `GeradorDocumento` (linha 27): nome sugerido `Evidencia_{cliente}_{tarefa}_{yyyyMMdd}.docx` quando houver cliente, sanitizado com `Path.GetInvalidFileNameChars()` como a tarefa.

## Aceite
- [ ] Sessão antiga abre normalmente e aparece em "(sem cliente)"; escolher um cliente na revisão e salvar move para o grupo certo.
- [ ] Digitar um cliente novo e iniciar a evidência cria o cliente em `compartilhado\clientes.json` com `CriadoPor = "test-evidence"`, e ele aparece na tela de configurações do shell.
- [ ] Digitar "cliente x" quando já existe "Cliente X" não duplica.
- [ ] Cliente criado no shell aparece no combo do TestEvidence sem reabrir.
- [ ] Renomear o cliente no shell muda o nome no grupo da lista.
- [ ] Filtrar por cliente restringe o combo de projetos; "Limpar" volta tudo.
- [ ] Busca "acao" encontra "Ação".
- [ ] Recolher um grupo e capturar um print não reabre o grupo.
- [ ] Fechar e abrir o app mantém filtro e agrupamento.
- [ ] TestEvidence standalone (fora do shell) lê e grava o mesmo catálogo.
- [ ] `dotnet build` do `TestEvidence.sln` e do `AppsCm.sln` sem erros novos.
