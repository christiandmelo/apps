# CLAUDE.md

Orientações para agentes neste repositório. Especificação completa em `IMPLEMENTACAO.md`.

- Camadas: `TestEvidence.UI` → `TestEvidence.Services` → `TestEvidence.Models` (sem container de DI).
- pt-BR em código, comentários, UI e mensagens; indentação de 2 espaços.
- Services não mostram `MessageBox`; erros de I/O nunca podem derrubar o app nem perder a sessão.
- Inicialização no `MainForm` (idempotente), não no `Program.Main`: sob o shell o `Main` não roda.
- A barra flutuante é `Form` top-level sem owner. Captura e overlay rodam com DPI per-monitor v2 na thread (`ContextoDpi`).
- `AppId` em `CaminhoConfig` tem que ser igual ao `Id` do módulo no shell (`test-evidence`).
- Build: `dotnet build TestEvidence.sln`. Sem projetos de teste no MVP.
