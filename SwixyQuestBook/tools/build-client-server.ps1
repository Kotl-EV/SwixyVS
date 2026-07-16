# Builds from ONE SwixyQuestBook project.
# CakeBuild scans sources, classifies Shared/Server/Client, emits two mod zips.
#
# From repo root:
#   .\build.ps1
#   # or:
#   powershell -ExecutionPolicy Bypass -File .\SwixyQuestBook\tools\build-client-server.ps1

param(
    [string] $Configuration = "Release",
    [switch] $SkipJsonValidation,
    [switch] $ContinueOnError
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
Set-Location $repoRoot

$argsList = @(
    "run", "--project", "CakeBuild/CakeBuild.csproj", "-c", "Release", "--",
    "--configuration=$Configuration"
)
if ($SkipJsonValidation) { $argsList += "--skipJsonValidation=true" }
if ($ContinueOnError) { $argsList += "--continueOnError=true" }

Write-Host ">>> dotnet $($argsList -join ' ')" -ForegroundColor Cyan
dotnet @argsList
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host ""
Write-Host "Packages:" -ForegroundColor Green
Get-ChildItem (Join-Path $repoRoot "Releases") -Filter "swixyquestbook_*" |
    Sort-Object Name |
    ForEach-Object { "  {0,-48} {1,10:N1} KB" -f $_.Name, ($_.Length/1KB) | Write-Host }

$cls = Join-Path $repoRoot "SwixyQuestBook\obj\cake-split\classification.txt"
if (Test-Path $cls) {
    Write-Host ""
    Write-Host "Classification: $cls" -ForegroundColor DarkGray
}
