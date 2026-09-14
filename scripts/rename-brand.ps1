<#
.SYNOPSIS
    Renombra automáticamente la marca, proyectos, carpetas, namespaces y configuración de la solución.
.DESCRIPTION
    Permite cambiar de forma segura el nombre del producto (por ejemplo de 'Aldune' a un nuevo nombre)
    actualizando la estructura git, proyectos .csproj, solución .slnx, archivos XAML, Docker,
    scripts e instaladores, manteniendo intacta la lógica de abanico ("fan") y la retrocompatibilidad.
.PARAMETER NewName
    El nuevo nombre de la marca (ej. 'MiNotas').
.PARAMETER OldName
    El nombre actual de la marca a reemplazar (por defecto 'Aldune').
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$NewName,

    [string]$OldName = "Aldune"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

if ($NewName -match '[^a-zA-Z0-9_]') {
    throw "El nuevo nombre '$NewName' contiene caracteres no válidos para identificadores de C#."
}

Write-Host "Iniciando renombrado de '$OldName' a '$NewName' en $root..." -ForegroundColor Cyan

# 1. Actualizar contenidos en archivos C# (.cs)
Write-Host "1/6. Actualizando namespaces y referencias en archivos .cs..."
Get-ChildItem -Path (Join-Path $root "src"), (Join-Path $root "tests") -Recurse -Filter *.cs | ForEach-Object {
    $content = [System.IO.File]::ReadAllText($_.FullName)
    $updated = $content.Replace("namespace $OldName", "namespace $NewName")
    $updated = $updated.Replace("using $OldName", "using $NewName")
    $updated = $updated.Replace("$OldName.Core.", "$NewName.Core.")
    $updated = $updated.Replace("$OldNameHotkey", "$NewNameHotkey")
    if ($updated -ne $content) {
        [System.IO.File]::WriteAllText($_.FullName, $updated, [System.Text.Encoding]::UTF8)
    }
}

# 2. Actualizar archivos XAML (.xaml)
Write-Host "2/6. Actualizando archivos XAML..."
Get-ChildItem -Path (Join-Path $root "src") -Recurse -Filter *.xaml | ForEach-Object {
    $content = [System.IO.File]::ReadAllText($_.FullName)
    $updated = $content.Replace("x:Class=""$OldName", "x:Class=""$NewName")
    $updated = $updated.Replace("clr-namespace:$OldName", "clr-namespace:$NewName")
    $updated = $updated.Replace("${OldName}GroundBrush", "${NewName}GroundBrush")
    $updated = $updated.Replace("${OldName}SurfaceBrush", "${NewName}SurfaceBrush")
    $updated = $updated.Replace("${OldName}RaisedBrush", "${NewName}RaisedBrush")
    $updated = $updated.Replace("${OldName}HoverBrush", "${NewName}HoverBrush")
    $updated = $updated.Replace("${OldName}PressedBrush", "${NewName}PressedBrush")
    $updated = $updated.Replace("${OldName}TextBrush", "${NewName}TextBrush")
    $updated = $updated.Replace("${OldName}MutedTextBrush", "${NewName}MutedTextBrush")
    $updated = $updated.Replace("${OldName}HintBrush", "${NewName}HintBrush")
    $updated = $updated.Replace("${OldName}DividerBrush", "${NewName}DividerBrush")
    if ($updated -ne $content) {
        [System.IO.File]::WriteAllText($_.FullName, $updated, [System.Text.Encoding]::UTF8)
    }
}

# 3. Actualizar archivos .csproj
Write-Host "3/6. Actualizando referencias en archivos de proyecto .csproj..."
Get-ChildItem -Path (Join-Path $root "src"), (Join-Path $root "tests") -Recurse -Filter *.csproj | ForEach-Object {
    $content = [System.IO.File]::ReadAllText($_.FullName)
    $updated = $content.Replace($OldName, $NewName)
    $updated = $updated.Replace($OldName.ToLower(), $NewName.ToLower())
    if ($updated -ne $content) {
        [System.IO.File]::WriteAllText($_.FullName, $updated, [System.Text.Encoding]::UTF8)
    }
}

# 4. Actualizar solución .slnx
Write-Host "4/6. Actualizando solución .slnx..."
$oldSlnx = Join-Path $root "$OldName.slnx"
$newSlnx = Join-Path $root "$NewName.slnx"
if (Test-Path -LiteralPath $oldSlnx) {
    $content = [System.IO.File]::ReadAllText($oldSlnx)
    $updated = $content.Replace($OldName, $NewName)
    [System.IO.File]::WriteAllText($oldSlnx, $updated, [System.Text.Encoding]::UTF8)
    git mv "$OldName.slnx" "$NewName.slnx" 2>$null
    if (-not (Test-Path -LiteralPath $newSlnx)) {
        Rename-Item -LiteralPath $oldSlnx -NewName "$NewName.slnx"
    }
}

# 5. Renombrar carpetas y proyectos
Write-Host "5/6. Renombrando carpetas y proyectos..."
function Safe-Move($source, $dest) {
    if (Test-Path -LiteralPath $source) {
        git mv $source $dest 2>$null
        if (Test-Path -LiteralPath $source) {
            Move-Item -LiteralPath $source -Destination $dest -Force
        }
    }
}

# Proyectos
Safe-Move "src/$OldName.Core/$OldName.Core.csproj" "src/$OldName.Core/$NewName.Core.csproj"
Safe-Move "src/$OldName.SyncServer/$OldName.SyncServer.csproj" "src/$OldName.SyncServer/$NewName.SyncServer.csproj"
Safe-Move "src/$OldName/$OldName.csproj" "src/$OldName/$NewName.csproj"
Safe-Move "tests/$OldName.Core.Tests/$OldName.Core.Tests.csproj" "tests/$OldName.Core.Tests/$NewName.Core.Tests.csproj"

# Carpetas
Safe-Move "src/$OldName.Core" "src/$NewName.Core"
Safe-Move "src/$OldName.SyncServer" "src/$NewName.SyncServer"
Safe-Move "src/$OldName" "src/$NewName"
Safe-Move "tests/$OldName.Core.Tests" "tests/$NewName.Core.Tests"

# 6. Actualizar scripts, Docker e instalador
Write-Host "6/6. Actualizando scripts, Docker e instalador..."
$filesToUpdate = @(
    "scripts/build-installer.ps1",
    "installer/Aldune.iss",
    "src/$NewName.SyncServer/Dockerfile",
    "docker-compose.sync.yml",
    "docker-compose.sync.nginx.yml",
    "docker-compose.sync.https.yml",
    ".github/workflows/ci.yml",
    ".github/workflows/release.yml",
    "README.md"
)

foreach ($relPath in $filesToUpdate) {
    $full = Join-Path $root $relPath
    if (Test-Path -LiteralPath $full) {
        $c = [System.IO.File]::ReadAllText($full)
        $c = $c.Replace($OldName, $NewName)
        $c = $c.Replace($OldName.ToLower(), $NewName.ToLower())
        [System.IO.File]::WriteAllText($full, $c, [System.Text.Encoding]::UTF8)
    }
}

Write-Host "Renombrado a '$NewName' completado con éxito." -ForegroundColor Green
Write-Host "Ejecuta: dotnet test $NewName.slnx para verificar la compilación y pruebas." -ForegroundColor Yellow
