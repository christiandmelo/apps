<#
Automatiza o processo de release do shell: atualiza a versão em src\Directory.Build.props e em
version.json, publica o AppsCm.Shell e gera o apps-cm.zip pronto para subir como asset da release
no GitHub. Diferente do Release.ps1 do TestCoverageUI, há uma única fonte de versão (o bundle
inteiro é versionado junto) e o hash SHA-256 é calculado depois do zip pronto, para cobrir o
artefato final e não o conteúdo solto do publish.

Uso:
  .\Release.ps1 -Version 1.0.1
#>
param(
  [Parameter(Mandatory = $true)]
  [string]$Version
)

$ErrorActionPreference = "Stop"

$repoDir = $PSScriptRoot
$buildProps = Join-Path $repoDir "src\Directory.Build.props"
$shellCsproj = Join-Path $repoDir "src\AppsCm.Shell\AppsCm.Shell.csproj"
$versionJson = Join-Path $repoDir "version.json"
$publishDir = Join-Path $repoDir "publish"
$zipPath = Join-Path $repoDir "apps-cm.zip"

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

Write-Host "3) Gerando $zipPath..."
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath

Write-Host "4) Calculando SHA-256 e atualizando version.json..."
$hash = (Get-FileHash -Path $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -Path $versionJson -Value @"
{
  "version": "$Version",
  "url": "https://github.com/christiandmelo/apps/releases/download/$Version/apps-cm.zip",
  "sha256": "$hash"
}
"@ -NoNewline

Write-Host ""
Write-Host "Pronto. Directory.Build.props, version.json e $zipPath atualizados para $Version."
Write-Host "Faltam apenas: revisar/commitar as mudanças, dar push e criar a release '$Version' no GitHub anexando o zip."
