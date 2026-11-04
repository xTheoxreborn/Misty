<#
.SYNOPSIS
    Prépare une release de Misty (version Rust) : exe, zip (portable + mise à jour automatique), installateur.

.DESCRIPTION
    Produit dans publish\ :
      - Misty-<version>\            l'application (un seul fichier : Misty.exe)
      - Misty-<version>.zip         version portable, à joindre aussi à la release GitHub
                                    (c'est ce zip que télécharge la mise à jour automatique)
      - Misty-Setup-<version>.exe   l'installateur (nécessite Inno Setup 6.3+)

    La version est lue dans misty-rust\src\services\app_info.rs (const VERSION).

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
$projet = Join-Path $racine "misty-rust"

# ── Version ──
$appInfo = Get-Content (Join-Path $projet "src\services\app_info.rs") -Raw
if ($appInfo -notmatch 'pub const VERSION: &str = "([^"]+)"') { throw "Version introuvable dans app_info.rs" }
$version = $Matches[1]
Write-Host "Misty $version" -ForegroundColor Cyan

$publish = Join-Path $racine "publish"
$dossier = Join-Path $publish "Misty-$version"
$zip = Join-Path $publish "Misty-$version.zip"

# ── Compilation (release : optimisée, sans console) ──
Push-Location $projet
try {
    cargo build --release
    if ($LASTEXITCODE -ne 0) { throw "cargo build a échoué" }
} finally {
    Pop-Location
}

# ── Dossier de l'application : tout est intégré à l'exe (polices, traductions, images) ──
if (Test-Path $dossier) { Remove-Item $dossier -Recurse -Force }
New-Item -ItemType Directory -Force $dossier | Out-Null
Copy-Item (Join-Path $projet "target\release\Misty.exe") $dossier
Copy-Item (Join-Path $racine "LICENSE.txt") $dossier

# ── Zip (le dossier Misty-<version> doit être à la racine du zip, voir services\update.rs) ──
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
