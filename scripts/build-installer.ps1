param(
    [switch]$SkipPublish
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$portableDir = Join-Path $root "publish\portable"
$distDir = Join-Path $root "dist"
$issPath = Join-Path $root "installer\Aldune.iss"

if (-not $SkipPublish) {
    dotnet publish (Join-Path $root "src\Fanote\Fanote.csproj") `
        -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:PublishTrimmed=false `
        -p:DebugSymbols=false `
        -p:DebugType=None `
        -o $portableDir
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish terminó con código $LASTEXITCODE." }
}

$iscc = Get-Command ISCC.exe -ErrorAction SilentlyContinue
if ($null -eq $iscc) {
    $candidates = @(
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    ) | Where-Object { $_ -and (Test-Path -LiteralPath $_) }

    if ($candidates.Count -eq 0) {
        throw "No se ha encontrado ISCC.exe. Instala Inno Setup 6 o añádelo al PATH."
    }

    $isccPath = @($candidates)[0]
}
else {
    $isccPath = $iscc.Source
}

New-Item -ItemType Directory -Path $distDir -Force | Out-Null
& $isccPath $issPath
if ($LASTEXITCODE -ne 0) { throw "Inno Setup terminó con código $LASTEXITCODE." }

$legacyPortable = Join-Path $portableDir "fanote.exe"
if (Test-Path -LiteralPath $legacyPortable) {
    Remove-Item -LiteralPath $legacyPortable -Force
}
$portableZip = Join-Path $distDir "aldune-portable-win-x64.zip"
Compress-Archive -Path (Join-Path $portableDir "aldune.exe") -DestinationPath $portableZip -Force
Write-Host "Instalador y portable creados en $distDir"
