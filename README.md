# apps-cm

Shell centralizador das ferramentas do time: um único `AppsCm.exe`, com um menu **Apps** que abre cada ferramenta numa aba, e um único fluxo de atualização automática para tudo (`AppsCm.Updater`).

## Estrutura

```
apps-cm/
  src/
    Directory.Build.props     <- versão única do bundle (Version/AssemblyVersion/FileVersion)
    AppsCm.Shell/              <- WinForms, referencia os módulos abaixo via ProjectReference
    AppsCm.Updater/             <- processo separado que aplica a atualização
  TestCoverageUI/               <- submódulo git
  git-submodule-sync/            <- submódulo git
  TestEvidence/                  <- submódulo git
  version.json                  <- servido via raw.githubusercontent.com, checado pelo AppsCm.exe no startup
  Release.ps1                   <- script que gera uma nova versão (ver abaixo)
```

Cada ferramenta (`TestCoverageUI`, `git-submodule-sync`, `TestEvidence`) continua sendo um repositório próprio, adicionado aqui como submódulo. O `AppsCm.Shell` referencia o projeto `.UI` de cada um via `ProjectReference` e hospeda o `MainForm` deles reparenteado numa aba — não é uma cópia de código, é o mesmo projeto compilado junto.

## Desenvolvimento

```powershell
dotnet build AppsCm.sln -c Debug
dotnet run --project src\AppsCm.Shell
```

Cada submódulo também continua compilando e rodando standalone (útil para desenvolver só uma ferramenta sem abrir o shell inteiro):

```powershell
dotnet run --project TestCoverageUI\TestCoverageUI.UI
dotnet run --project git-submodule-sync\GitSubmoduleSync.UI
dotnet run --project TestEvidence\TestEvidence.UI
```

## Publicando uma nova versão

A versão do bundle mora **só** em `src\Directory.Build.props`. Todo o resto (publish, zip, hash, `version.json`) é gerado pelo `Release.ps1` — não edite `version.json` à mão.

### 1. Se você mudou algo dentro de um submódulo (`TestCoverageUI` ou `git-submodule-sync`)

Commit e push **dentro da pasta do submódulo**, no repositório dele:

```powershell
cd TestCoverageUI          # ou git-submodule-sync
git add -A
git commit -m "Descrição da mudança"
git push origin main
cd ..
```

Depois, grave o novo ponteiro do submódulo no apps-cm:

```powershell
git add TestCoverageUI git-submodule-sync
git commit -m "Atualiza ponteiro dos submódulos"
```

Pule esta etapa se você só mudou algo dentro do próprio `apps-cm` (`src\`, `Release.ps1`, etc.) — nesse caso os submódulos não têm nada novo para apontar.

### 2. Gerar a versão

```powershell
.\Release.ps1 -Version 1.0.1
```

O script:
1. Atualiza `<Version>`/`<AssemblyVersion>`/`<FileVersion>` em `src\Directory.Build.props`.
2. Roda `dotnet publish` do `AppsCm.Shell` para a pasta `publish\` (inclui `Updater.exe`, `Preloader.exe`, `Tools\` e o runtime do WebView2 automaticamente).
3. Compacta `publish\` em `apps-cm.zip`, na raiz do repositório — esse é o único artefato que o auto-update (`UpdateService`/`Updater.exe`) baixa e aplica; continua funcionando exatamente igual para quem já tem o app instalado.
4. Compila `installer\apps-cm.iss` com o Inno Setup, gerando `apps-cm-setup.exe` na raiz — um instalador de verdade (atalho no Menu Iniciar, entrada em Adicionar/Remover Programas, desinstalador), pensado só para **instalação nova**. Requer o Inno Setup 6 instalado na máquina que gera a release: `winget install -e --id JRSoftware.InnoSetup`.
5. Calcula o SHA-256 do zip e reescreve `version.json` com a nova versão, as duas URLs de download (`url` do zip, `installerUrl` do instalador) e o hash do zip.

**Sobre o instalador e o SmartScreen:** o instalador melhora a experiência de instalação (atalho, desinstalador), mas **não** remove o aviso do Windows Defender SmartScreen que aparece durante as atualizações automáticas — isso é sobre o executável não ser assinado digitalmente (cada release tem um hash novo, sem reputação acumulada), não sobre zip vs instalador. Ficou de fora por decisão consciente; `Release.ps1` já tem um parâmetro opcional `-SigningCertThumbprint` pronto para quando (se) a assinatura de código for adotada.

### 3. Commitar o bump de versão

```powershell
git add src\Directory.Build.props version.json
git commit -m "Bump de versão para 1.0.1"
git push origin main
```

### 4. Criar a release no GitHub

A tag precisa ser **exatamente** o número da versão (`1.0.1`), e os dois assets anexados precisam se chamar **exatamente** `apps-cm.zip` e `apps-cm-setup.exe` — é isso que `url`/`installerUrl` em `version.json` esperam. Os dois arquivos ficam na raiz do repositório depois do `Release.ps1` e não são commitados (estão no `.gitignore`); só existem como asset da release.

**Pelo site:** Releases → Draft a new release → tag `1.0.1` → anexar `apps-cm.zip` **e** `apps-cm-setup.exe` da raiz → Publish release.

**Pelo GitHub CLI** (se tiver o `gh` instalado — `winget install GitHub.cli`):
```powershell
gh release create 1.0.1 apps-cm.zip apps-cm-setup.exe --title "1.0.1" --notes "Descrição das mudanças"
```

Para quem já usa o app (independente de ter instalado via zip ou via instalador), nada muda — o auto-update sempre baixa e verifica só o `apps-cm.zip`. O `apps-cm-setup.exe` é só para quem está instalando pela primeira vez.

### Como o auto-update funciona para quem já tem o app instalado

O `AppsCm.exe` já instalado consulta `version.json` (via `raw.githubusercontent.com`) toda vez que abre. Se a versão publicada for maior que a instalada, oferece para baixar e aplicar — o `Updater.exe` verifica o hash do zip antes de tocar em qualquer arquivo, então uma release mal formada (tag errada, zip errado, hash desatualizado) falha de forma segura em vez de quebrar a instalação de quem já usa o app.
