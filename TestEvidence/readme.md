# TestEvidence

Ferramenta desktop (Windows Forms, .NET 8) para gerar evidência de teste sem sair do fluxo de teste: barra flutuante sempre por cima, captura por região/tela inteira com atalho global (`Ctrl+Shift+E`, repetir região `Ctrl+Shift+R`), legenda rápida e geração do `.docx` ao final.

Faz parte do [apps-cm](https://github.com/christiandmelo/apps) (submódulo, hospedado numa aba do `AppsCm.exe`) e também roda standalone:

```powershell
dotnet run --project TestEvidence.UI
```

Sessões ficam em `%LOCALAPPDATA%\apps-cm\test-evidence\sessoes`. Detalhes de projeto em `IMPLEMENTACAO.md`.
