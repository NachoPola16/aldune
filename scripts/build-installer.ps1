param(
    [switch]$SkipPublish
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$portableDir = Join-Path $root "publish\portable"
$distDir = Join-Path $root "dist"
$issPath = Join-Path $root "installer\Aldune.iss"

if (-not $SkipPublish) {
    # Los símbolos de depuración se apagan en Aldune.csproj (solo para el proyecto de la app), no aquí
    # con -p:DebugType=None: una propiedad global de MSBuild se aplica también a los proyectos
    # referenciados, y así Aldune.Core se compilaba en Release sin .pdb y el siguiente
    # "dotnet test -c Release" fallaba con MSB3030 al copiarlo. Ver docs/STATUS.md, sesión 2026-09-16.
    dotnet publish (Join-Path $root "src\Aldune\Aldune.csproj") `
        -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:PublishTrimmed=false `
        -o $portableDir

    # El publish arrastra tambien el .pdb de las dependencias (Aldune.Core.pdb), que a quien descarga
    # el portable o el instalador no le sirve de nada: los dos reparten solo aldune.exe.
    Remove-Item -Path (Join-Path $portableDir "*.pdb") -Force -ErrorAction SilentlyContinue

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

$portableZip = Join-Path $distDir "aldune-portable-win-x64.zip"
Compress-Archive -Path (Join-Path $portableDir "aldune.exe") -DestinationPath $portableZip -Force
Write-Host "Instalador y portable creados en $distDir"
