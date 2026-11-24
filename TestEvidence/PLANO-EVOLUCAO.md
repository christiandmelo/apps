# TestEvidence: plano de evolução (sugestões da QA)

Antes de tudo, a [migração para monorepo](../docs/MIGRACAO-MONOREPO.md) (os apps deixam de ser submódulos). Depois, quatro entregas, nesta ordem. Cada uma é um commit/PR próprio, tem um documento só dela e deixa os apps funcionando.

| Fase | Repositório | Documento | O que entrega | Depende de |
|---|---|---|---|---|
| 0 | apps-cm (shell) | [docs/CONFIGURACAO-COMPARTILHADA.md](../docs/CONFIGURACAO-COMPARTILHADA.md) | Configurações gerais do apps-cm: conexão com o Jira e catálogo de clientes/projetos, gravados num local comum que qualquer app usa | nada |
| 1 | TestEvidence | [docs/FASE-1-AGRUPAR-FILTRAR.md](docs/FASE-1-AGRUPAR-FILTRAR.md) | Cliente e Projeto na evidência (do catálogo comum, podendo criar na hora); tela principal agrupada e com filtros | 0 |
| 2 | TestEvidence | [docs/FASE-2-JIRA-ZEPHYR.md](docs/FASE-2-JIRA-ZEPHYR.md) | Importar título e caso de teste do Zephyr Scale pela chave da tarefa, usando a conexão do apps-cm | 0, 1 |
| 3 | TestEvidence | [docs/FASE-3-DEFEITO-JIRA.md](docs/FASE-3-DEFEITO-JIRA.md) | Subtarefa de defeito no Jira com os prints na descrição | 2 |

## Decisões já tomadas

| Tema | Decisão |
|---|---|
| Onde fica a configuração | `%LOCALAPPDATA%\apps-cm\compartilhado\` (`jira.json`, `clientes.json`), formato versionado, lidos e gravados pelo projeto comum `src/AppsCm.Compartilhado` |
| Jira | URL **configurável** nas configurações gerais do shell (hoje o time usa o `jiraproducao.totvs.com.br`, o mesmo do Worker - IA). Token pessoal (Bearer) criptografado com DPAPI do usuário. O `origin` anti-WAF sai da URL configurada. Os apps só leem |
| Clientes e projetos | Catálogo comum. O shell tem a tela de cadastro, e **qualquer app pode criar** (o TestEvidence cria ao confirmar a evidência). Projeto pode ter a chave do projeto no Jira, o que permite preencher cliente e projeto ao importar a tarefa |
| Cenários de teste | Vêm do **Zephyr Scale** (casos `PROJ-T123` vinculados à tarefa) |
| Cenários por evidência | **Um caso de teste por sessão**; a QA escolhe qual |
| Tipo do defeito | `Defeito (Sub-tarefa)`, configurável no TestEvidence |
| Campos obrigatórios do defeito | Descobertos na tentativa, como no Worker - IA: tenta criar, pergunta só o que o Jira pediu, lembra por projeto |
| Prints no defeito | A QA marca na hora; os de "Resultado" vêm pré-marcados |

## Pendências conhecidas

- **API do Zephyr Scale:** a rota varia por versão do plugin. A fase 2 começa com uma etapa de descoberta contra uma tarefa real.
- **Catálogo TOTVS** (Produto/Módulo/Rotina): se o projeto exigir isso para criar o defeito, a fase 3 mostra o campo e para. A gravação via `/rest/pluginTotvs/1.0/issue/catalog` fica para uma fase 3.1.

Referência de integração com o Jira: `C:\Projetos\Worker - IA\automation\tools\jira_tool.py` (cliente) e `debt_closer.py` (campos obrigatórios).
