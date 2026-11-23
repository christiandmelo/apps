<#
Automatiza o processo de release do shell: atualiza a versão em src\Directory.Build.props e em
version.json, publica o AppsCm.Shell, gera o apps-cm.zip (consumido pelo auto-update) e o
apps-cm-setup.exe (instalador Inno Setup, só para instalação nova — o Updater nunca lê esse
artefato). Diferente do Release.ps1 do TestCoverageUI, há uma única fonte de versão (o bundle
inteiro é versionado junto) e o hash SHA-256 é calculado depois do zip pronto, para cobrir o
artefato final e não o conteúdo solto do publish.

Pré-requisito de máquina (não é pacote NuGet): Inno Setup 6.
  winget install -e --id JRSoftware.InnoSetup

Uso:
  .\Release.ps1 -Version 1.0.1
#>
param(
  [Parameter(Mandatory = $true)]
  [string]$Version,

  # Opcional: thumbprint de um certificado de code signing instalado no cert store da máquina.
  # Quando informado, assina AppsCm.exe/Updater.exe (dentro de publish\) antes de zipar/empacotar.
  # Nenhum certificado existe ainda — decidir/comprar antes de usar este parâmetro (ver README.md).
  [string]$SigningCertThumbprint
)

$ErrorActionPreference = "Stop"

# O feed NuGet privado da TOTVS retorna 401 sem autenticação; restaurar só do nuget.org (somente neste processo).
$env:RestoreSources = "https://api.nuget.org/v3/index.json"

$repoDir = $PSScriptRoot
$buildProps = Join-Path $repoDir "src\Directory.Build.props"
$shellCsproj = Join-Path $repoDir "src\AppsCm.Shell\AppsCm.Shell.csproj"
$versionJson = Join-Path $repoDir "version.json"
$publishDir = Join-Path $repoDir "publish"
$zipPath = Join-Path $repoDir "apps-cm.zip"
$installerScript = Join-Path $repoDir "installer\apps-cm.iss"
$installerPath = Join-Path $repoDir "apps-cm-setup.exe"

# winget instala o Inno Setup por usuário por padrão (LocalAppData\Programs), mas uma instalação
# manual ou "winget install --scope machine" cai em Program Files (x86) — checar os dois.
$isccCandidatos = @(
  "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
  "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
)
$iscc = $isccCandidatos | Where-Object { Test-Path $_ } | Select-Object -First 1

Write-Host "1) Atualizando versão para $Version..."
$content = Get-Content $buildProps -Raw
$content = $content -replace "<Version>[^<]*</Version>", "<Version>$Version</Version>"
$content = $content -replace "<AssemblyVersion>[^<]*</AssemblyVersion>", "<AssemblyVersion>$Version</AssemblyVersion>"
$content = $content -replace "<FileVersion>[^<]*</FileVersion>", "<FileVersion>$Version</FileVersion>"
Set-Content -Path $buildProps -Value $content -NoNewline

Write-Host "2) Publicando (dotnet publish)..."
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
dotnet publish $shellCsproj -c Release -r win-x64 --self-contained false -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish falhou" }

if ($SigningCertThumbprint) {
  Write-Host "2b) Assinando binários (signtool)..."
  $filesToSign = @("AppsCm.exe", "Updater.exe") | ForEach-Object { Join-Path $publishDir $_ }
  & signtool.exe sign /fd sha256 /tr http://timestamp.digicert.com /td sha256 `
    /sha1 $SigningCertThumbprint @filesToSign
  if ($LASTEXITCODE -ne 0) { throw "Assinatura (signtool) falhou" }
}

Write-Host "3) Gerando $zipPath..."
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath

Write-Host "4) Gerando instalador (Inno Setup)..."
if (-not $iscc) {
  throw "ISCC.exe nao encontrado (procurado em: $($isccCandidatos -join ', ')). Instale com: winget install -e --id JRSoftware.InnoSetup"
}
if (Test-Path $installerPath) { Remove-Item $installerPath -Force }
& $iscc "/DMyAppVersion=$Version" $installerScript
if ($LASTEXITCODE -ne 0) { throw "Compilacao do instalador (Inno Setup) falhou" }

Write-Host "5) Calculando SHA-256 e atualizando version.json..."
$hash = (Get-FileHash -Path $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -Path $versionJson -Value @"
{
  "version": "$Version",
  "url": "https://github.com/christiandmelo/apps/releases/download/$Version/apps-cm.zip",
  "sha256": "$hash",
  "installerUrl": "https://github.com/christiandmelo/apps/releases/download/$Version/apps-cm-setup.exe"
}
"@ -NoNewline

Write-Host ""
Write-Host "Pronto. Directory.Build.props, version.json, $zipPath e $installerPath atualizados para $Version."
Write-Host "Faltam apenas: revisar/commitar as mudanças, dar push e criar a release '$Version' no GitHub anexando os dois arquivos (apps-cm.zip e apps-cm-setup.exe)."
