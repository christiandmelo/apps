# Migração: de submódulos para pastas no apps-cm

Transforma `TestCoverageUI`, `git-submodule-sync` e `TestEvidence` em pastas normais do repositório apps-cm, **mantendo o histórico de cada um**. Fazer antes da fase 0 do [plano do TestEvidence](../TestEvidence/PLANO-EVOLUCAO.md).

## Por quê

- Acaba o ciclo commit no submódulo → commit do ponteiro → push nos dois.
- Código comum entre apps (a [configuração compartilhada](CONFIGURACAO-COMPARTILHADA.md)) vira um projeto referenciado, sem cópia de classe.
- Mudança que atravessa apps fica num commit/PR só.
- Não muda o tamanho da instalação (o instalador só empacota o `publish`).

## O que não muda

- **Caminhos:** as pastas continuam com o mesmo nome e no mesmo lugar. Por isso `AppsCm.sln`, os `ProjectReference` do `AppsCm.Shell.csproj`, o `Release.ps1` e o instalador **não precisam de ajuste**.
- Cada app continua com o próprio `.sln`, `Directory.Build.props` (versão própria), `.gitignore`, `LICENSE` e `CLAUDE.md`, compilando standalone.
- O app git-submodule-sync continua existindo: ele serve para outros repositórios com submódulos, só deixa de ser necessário para o próprio apps-cm.

## Estado atual (conferido)

- `.gitmodules` com os 3 submódulos, todos em `main` e sem commits locais à frente do `origin/main`.
- `git subtree` disponível no Git for Windows.
- No TestEvidence, o plano (`PLANO-EVOLUCAO.md`, `docs/`) e o protótipo do tema já estão commitados na `main` local dele (sem push: o passo 3 copia do clone local).
- No apps-cm, os docs estão commitados na branch `migra-monorepo`, que é onde a migração acontece.

## Regras para quem executar (pessoa ou IA)
- Seguir os passos na ordem, um de cada vez, e parar se algum comando der erro.
- Nunca fazer `git push`; o push é feito pelo dono do repositório.
- Mensagens de commit sem rodapé de atribuição (`Co-Authored-By`, "Generated with...").
- O feed NuGet interno da TOTVS responde 401 (erro `NU1301`) no restore. Restaurar ignorando o feed e compilar sem restore:
  ```powershell
  dotnet restore <arquivo.sln> --ignore-failed-sources
  dotnet build <arquivo.sln> --no-restore
  ```

## Passo a passo

Rodar na raiz do apps-cm, em PowerShell. Nenhum passo faz push; o push é feito por você no fim.

### 1. Conferir que está tudo commitado
Os commits do plano e do protótipo já foram feitos. Conferir que os quatro repositórios estão limpos (o ` m TestEvidence` no apps-cm, se aparecer, é só o ponteiro do submódulo e pode ser ignorado: o submódulo sai no passo 4):
```powershell
git status --short; foreach ($d in 'TestEvidence','TestCoverageUI','git-submodule-sync') { "== $d"; git -C $d status --short }
```

### 2. Branch da migração
A branch `migra-monorepo` já existe e tem estes docs. Só garantir que está nela:
```powershell
git switch migra-monorepo
```

### 3. Cópia local de cada submódulo (fonte do histórico)
A cópia é feita do clone local, não do GitHub: assim entram também commits ainda não enviados, e o passo não depende de rede.
```powershell
$tmp = Join-Path $env:TEMP "apps-cm-migracao"
Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory $tmp | Out-Null
foreach ($d in 'TestCoverageUI','git-submodule-sync','TestEvidence') { git clone --no-local $d (Join-Path $tmp $d) }
```

### 4. Tirar os submódulos
```powershell
foreach ($d in 'TestCoverageUI','git-submodule-sync','TestEvidence') {
  git submodule deinit -f $d
  git rm -f $d
  Remove-Item ".git\modules\$d" -Recurse -Force -ErrorAction SilentlyContinue
}
git rm -f .gitmodules
git commit -m "Remove os submódulos (passam a ser pastas do repositório)"
```
Depois disso as pastas somem do disco; elas voltam no próximo passo.

### 5. Trazer cada app como pasta, com histórico
```powershell
foreach ($d in 'TestCoverageUI','git-submodule-sync','TestEvidence') {
  git subtree add --prefix=$d (Join-Path $tmp $d) main -m "Incorpora $d como pasta (histórico preservado)"
}
```
Sem `--squash`, de propósito: o histórico completo de cada app entra no apps-cm. `git log -- TestEvidence` mostra os commits antigos.

### 6. Validar
```powershell
foreach ($s in 'AppsCm.sln','TestEvidence\TestEvidence.sln','TestCoverageUI\TestCoverageUI.sln','git-submodule-sync\GitSubmoduleSync.sln') {
  dotnet restore $s --ignore-failed-sources
  dotnet build $s -c Release --no-restore
}
git log --oneline -- TestEvidence | Select-Object -First 5
```
Abrir o `AppsCm.exe` do build, abrir as 3 abas e conferir os ícones (`Icone\*.ico` vem dos projetos dos apps).

Opcional: rodar o `Release.ps1` até gerar o zip, **sem** publicar, e comparar o conteúdo com o `apps-cm.zip` da última release.

### 7. Atualizar a documentação (commit "Docs: estrutura de monorepo")
- `README.md`: tirar "submódulo git" da árvore (linha 14), reescrever o parágrafo da linha 20 (cada ferramenta agora é uma pasta do repositório) e apagar a seção "Se você mudou algo dentro de um submódulo" (linhas 41–57). O fluxo passa a ser um commit só.
- `TestCoverageUI/CLAUDE.md` (linha 11) e `TestEvidence/CLAUDE.md`: "submódulo do apps-cm" vira "pasta do repositório apps-cm".
- `docs/CONFIGURACAO-COMPARTILHADA.md` já foi escrito para o monorepo (projeto `AppsCm.Compartilhado`).
- A opção "atualizar ponteiro de submódulos" da versão 1.0.1 é do app git-submodule-sync (conferido no commit `0919be3`), então ela continua.

### 8. Juntar na main
```powershell
git switch main
git merge --no-ff migra-monorepo -m "Migra os apps de submódulos para pastas"
```
O push fica com você. Depois de publicar:
- Em cada repositório antigo (`TestCoverageUI`, `git-submodule-sync`, `TestEvidence`), trocar o README por um aviso "Este projeto agora vive em apps-cm/<pasta>" e **arquivar** no GitHub (Settings → Archive). O arquivamento deixa o repositório só para leitura, e os links antigos continuam funcionando.
- Clones antigos do apps-cm em outras máquinas: `git pull`, apagar a pasta `.git\modules` e rodar `git status`. Se as pastas dos apps aparecerem como não rastreadas, apagar e rodar `git checkout -- .`.
- Apagar `$env:TEMP\apps-cm-migracao`.

## Depois da migração
- [Tema visual](TEMA-VISUAL.md): projeto `src/AppsCm.Visual` com o tema prototipado no TestEvidence, aplicado em todas as telas.
- [Configuração compartilhada](CONFIGURACAO-COMPARTILHADA.md) e as fases do [plano do TestEvidence](../TestEvidence/PLANO-EVOLUCAO.md).

## Se algo der errado
Tudo acontece na branch `migra-monorepo` até o passo 8. Para desistir:
```powershell
git switch main
git branch -D migra-monorepo
git submodule update --init
```
A main e os repositórios dos apps não são tocados até o merge.
