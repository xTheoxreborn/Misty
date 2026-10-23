<#
.SYNOPSIS
    Prépare une release de Misty : publication, zip pour la mise à jour automatique, installateur .exe.

.DESCRIPTION
    Produit dans publish\ :
      - Misty-<version>\            l'application publiée
      - Misty-<version>.zip         à joindre à la release GitHub (utilisé par la mise à jour automatique)
      - Misty-Setup-<version>.exe   l'installateur (nécessite Inno Setup 6.3+)

    La version est lue dans src\Misty\AppInfo.cs.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File installer\build.ps1
#>
param(
    # Chemin de ISCC.exe si Inno Setup n'est pas installé à un emplacement standard
    [string]$Iscc = "",
    # Ne génère que le dossier et le zip
    [switch]$SansInstallateur
)

$ErrorActionPreference = "Stop"
$racine = Split-Path -Parent $PSScriptRoot
$projet = Join-Path $racine "src\Misty"

# ── Version ──
$appInfo = Get-Content (Join-Path $projet "AppInfo.cs") -Raw
if ($appInfo -notmatch 'const string Version = "([^"]+)"') { throw "Version introuvable dans AppInfo.cs" }
$version = $Matches[1]
Write-Host "Misty $version" -ForegroundColor Cyan

$publish = Join-Path $racine "publish"
$dossier = Join-Path $publish "Misty-$version"
$zip = Join-Path $publish "Misty-$version.zip"

# ── Publication ──
if (Test-Path $dossier) { Remove-Item $dossier -Recurse -Force }
dotnet publish $projet -c Release -r win-x64 --self-contained false -o $dossier
if ($LASTEXITCODE -ne 0) { throw "dotnet publish a échoué" }

# ── Zip (le dossier Misty-<version> doit être à la racine du zip, voir UpdateService) ──
if (Test-Path $zip) { Remove-Item $zip -Force }
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($dossier, $zip, [System.IO.Compression.CompressionLevel]::Optimal, $true)
Write-Host "Zip : $zip" -ForegroundColor Green

if ($SansInstallateur) { return }

# ── Installateur ──
if (-not $Iscc) {
    $candidats = @(
        (Get-Command ISCC.exe -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source),
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    )
    $Iscc = $candidats | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
}
if (-not $Iscc) {
    throw "Inno Setup introuvable. Installe-le (winget install JRSoftware.InnoSetup) ou passe -Iscc <chemin de ISCC.exe>."
}

& $Iscc "/DVersion=$version" "/DSource=$dossier" "/DSortie=$publish" (Join-Path $PSScriptRoot "Misty.iss")
if ($LASTEXITCODE -ne 0) { throw "ISCC a échoué" }

Write-Host "Installateur : $(Join-Path $publish "Misty-Setup-$version.exe")" -ForegroundColor Green
