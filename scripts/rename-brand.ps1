<#
.SYNOPSIS
    Renombra la marca de la aplicacion: textos, proyectos, carpetas, instalador e infraestructura.
.DESCRIPTION
    Cambia el nombre del producto de forma reproducible en lugar de ir buscando cadenas a mano.
    El mapa de donde vive el nombre esta en docs/BRANDING.md y el procedimiento en
    docs/RENAME_GUIDE.md.

    El script hace dos pasadas:
      1. Reescribe las tres formas del nombre (PascalCase, minusculas y MAYUSCULAS) en todos los
         ficheros de texto del repositorio, conservando la codificacion (BOM) y los finales de
         linea de cada fichero.
      2. Renombra con "git mv" la solucion, las carpetas de proyecto, los .csproj, el .iss y los
         assets cuyo nombre lleva la marca.

    Lo que NO hace a proposito (ver docs/RENAME_GUIDE.md): registrar los prefijos antiguos en
    BrandIdentity.LegacySyncProfileCodePrefixes, rediseñar el icono .ico, cambiar el AppId del
    instalador ni renombrar el volumen de datos de Docker.
.PARAMETER NewName
    Nombre nuevo. Solo letras, digitos y guion bajo, empezando por letra.
.PARAMETER OldName
    Nombre vigente que se reemplaza. Por defecto se lee de BrandIdentity.AppName.
.PARAMETER DryRun
    Muestra lo que cambiaria sin escribir ni renombrar nada.
.EXAMPLE
    ./scripts/rename-brand.ps1 -NewName "MiNotas" -DryRun
.EXAMPLE
    ./scripts/rename-brand.ps1 -NewName "MiNotas"
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$NewName,

    [string]$OldName,

    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

if ($NewName -notmatch '^[A-Za-z][A-Za-z0-9_]*$') {
    throw "El nombre nuevo '$NewName' no sirve como identificador: letras, digitos o guion bajo, empezando por letra."
}

# 0. Nombre vigente: se lee de BrandIdentity para no tener que pasarlo a mano.
if (-not $OldName) {
    $brandFile = Get-ChildItem -Path $root -Recurse -File -Filter "BrandIdentity.cs" |
        Where-Object { $_.FullName -notmatch '\\bin\\|\\obj\\' } |
        Select-Object -First 1
    if (-not $brandFile) { throw "No encuentro BrandIdentity.cs. Pasa -OldName a mano." }

    $brandText = [System.IO.File]::ReadAllText($brandFile.FullName)
    $appNameMatch = [regex]::Match($brandText, 'AppName\s*=\s*"([^"]+)"')
    if (-not $appNameMatch.Success) { throw "No he podido leer AppName de $($brandFile.FullName). Pasa -OldName a mano." }
    $OldName = $appNameMatch.Groups[1].Value
}

if ($OldName -eq $NewName) { throw "El nombre nuevo y el viejo son el mismo ('$OldName')." }

if (-not $DryRun) {
    $dirty = git status --porcelain
    if ($dirty) {
        throw "Hay cambios sin commitear. Haz commit o stash antes de renombrar, para poder revisar el diff del renombrado."
    }
}

Write-Host "Renombrando '$OldName' -> '$NewName'$(if ($DryRun) { ' (simulacion: no se escribe nada)' })" -ForegroundColor Cyan
Write-Host ""

$excluded = '\\bin\\|\\obj\\|\\.git\\|graphify-out|worktrees|\\publish\\|\\dist\\|\\.vs\\|\\.claude\\'
$textExtensions = @('.cs', '.xaml', '.csproj', '.slnx', '.json', '.ps1', '.yml', '.yaml', '.md',
                    '.iss', '.pubxml', '.manifest', '.props', '.targets', '.config', '.xml', '.txt')
$textFileNames = @('Caddyfile', 'Dockerfile', '.env.example', '.gitignore', '.dockerignore')

function Get-BrandTextFiles {
    Get-ChildItem -Path $root -Recurse -File -Force |
        Where-Object {
            $_.FullName -notmatch $excluded -and
            ($textExtensions -contains $_.Extension.ToLowerInvariant() -or $textFileNames -contains $_.Name)
        }
}

function Read-BrandFile([System.IO.FileInfo]$file) {
    $bytes = [System.IO.File]::ReadAllBytes($file.FullName)
    $hasBom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
    $offset = if ($hasBom) { 3 } else { 0 }
    return @{
        Text   = [System.Text.Encoding]::UTF8.GetString($bytes, $offset, $bytes.Length - $offset)
        Bom    = $hasBom
    }
}

function Write-BrandFile([System.IO.FileInfo]$file, [string]$text, [bool]$bom) {
    $encoding = New-Object System.Text.UTF8Encoding($bom)
    [System.IO.File]::WriteAllBytes($file.FullName, $encoding.GetPreamble() + $encoding.GetBytes($text))
}

# 1. Pasada de texto. El orden importa: primero MAYUSCULAS, luego PascalCase y por ultimo
#    minusculas, para que las variables de entorno del tipo XXX_SYNC_TOKEN no queden a medias.
$replacements = @(
    @($OldName.ToUpperInvariant(), $NewName.ToUpperInvariant()),
    @($OldName, $NewName),
    @($OldName.ToLowerInvariant(), $NewName.ToLowerInvariant())
)

Write-Host "1/3. Reescribiendo textos..."
$edited = New-Object System.Collections.Generic.List[string]
foreach ($file in Get-BrandTextFiles) {
    $content = Read-BrandFile $file
    $updated = $content.Text
    foreach ($pair in $replacements) { $updated = $updated.Replace($pair[0], $pair[1]) }
    if ($updated -ne $content.Text) {
        $edited.Add($file.FullName.Substring($root.Length + 1))
        if (-not $DryRun) { Write-BrandFile $file $updated $content.Bom }
    }
}
Write-Host "     $($edited.Count) ficheros con cambios"
$edited | ForEach-Object { Write-Host "       $_" }

# 2. Pasada de nombres. Primero los ficheros y despues las carpetas de dentro hacia fuera, porque
#    renombrar una carpeta invalida las rutas de lo que tiene dentro.
Write-Host "2/3. Renombrando ficheros y carpetas..."

function Move-BrandPath([string]$relative) {
    $leaf = Split-Path -Leaf $relative
    $parent = Split-Path -Parent $relative

    # Ojo con el nombre: en PowerShell los nombres de variable NO distinguen mayusculas, asi que
    # "$newName" seria exactamente la misma variable que el parametro $NewName y lo corromperia.
    $targetName = $leaf.Replace($OldName, $NewName)
    if ($targetName -eq $leaf) { $targetName = $leaf.Replace($OldName.ToLowerInvariant(), $NewName.ToLowerInvariant()) }
    if ($targetName -eq $leaf) { return }

    $destination = if ($parent) { Join-Path $parent $targetName } else { $targetName }
    if (Test-Path -LiteralPath (Join-Path $root $destination)) {
        Write-Host "       ya existe, se omite: $destination" -ForegroundColor Yellow
        return
    }

    Write-Host "       $relative -> $destination"
    if ($DryRun) { return }

    # Git quiere rutas con barra normal, tambien en Windows.
    $sourceGit = $relative.Replace('\', '/')
    $destinationGit = $destination.Replace('\', '/')
    git ls-files --error-unmatch -- $sourceGit *> $null
    if ($LASTEXITCODE -eq 0) {
        git mv -- $sourceGit $destinationGit *> $null
    } else {
        Move-Item -LiteralPath (Join-Path $root $relative) -Destination (Join-Path $root $destination)
    }
}

$brandPaths = @(Get-ChildItem -Path $root -Recurse -Force |
    Where-Object { $_.FullName -notmatch $excluded -and $_.Name -like "*$OldName*" })

$brandFiles = @($brandPaths | Where-Object { -not $_.PSIsContainer } |
    ForEach-Object { $_.FullName.Substring($root.Length + 1) })
$brandDirs = @($brandPaths | Where-Object { $_.PSIsContainer } |
    ForEach-Object { $_.FullName.Substring($root.Length + 1) } |
    Sort-Object -Property Length -Descending)

foreach ($relative in $brandFiles) { Move-BrandPath $relative }
foreach ($relative in $brandDirs) { Move-BrandPath $relative }

# 3. Informe: lo que todavia dice el nombre antiguo dentro del repositorio.
Write-Host "3/3. Restos del nombre antiguo"
if ($DryRun) {
    Write-Host "     (en simulacion no se ha escrito nada, asi que sigue estando todo)"
} else {
    $leftovers = New-Object System.Collections.Generic.List[string]
    foreach ($file in Get-BrandTextFiles) {
        $content = Read-BrandFile $file
        $number = 0
        foreach ($line in ($content.Text -split "`n")) {
            $number++
            if ($line.Contains($OldName) -or $line.Contains($OldName.ToUpperInvariant()) -or
                $line.Contains($OldName.ToLowerInvariant())) {
                $leftovers.Add("$($file.FullName.Substring($root.Length + 1)):$($number): $($line.Trim())")
            }
        }
    }
    if ($leftovers.Count -eq 0) {
        Write-Host "     Ninguno." -ForegroundColor Green
    } else {
        $leftovers | ForEach-Object { Write-Host "       $_" }
        Write-Host "     Revisalos: deberian ser solo los restos justificados de docs/BRANDING.md."
    }
}

Write-Host ""
if ($DryRun) {
    Write-Host "Simulacion terminada: no se ha tocado nada. Repite sin -DryRun para aplicarlo." -ForegroundColor Yellow
} else {
    Write-Host "Hecho. Queda esto a mano (ver docs/RENAME_GUIDE.md):" -ForegroundColor Yellow
    Write-Host "  - Anade AHORA, a mano, el prefijo de codigos de perfil antiguo a BrandIdentity.LegacySyncProfileCodePrefixes,"
    Write-Host "    para que las invitaciones ya compartidas se sigan importando."
    Write-Host "  - Revisa la tabla 'Historial de nombres' de docs/BRANDING.md y la seccion historica de docs/RENAME_GUIDE.md."
    Write-Host "  - Rediseña el icono de src/.../Assets si la marca cambia de simbolo."
    Write-Host "  - Comprueba con: dotnet test, y luego ./scripts/build-installer.ps1 para el portable y el instalador."
}
