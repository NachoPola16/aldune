# Identidad de marca

Este documento es la referencia de **dónde vive el nombre de la aplicación** y de qué hay que tocar
al cambiarlo. El procedimiento paso a paso para un cambio de nombre está en
[`RENAME_GUIDE.md`](RENAME_GUIDE.md).

## Nombre vigente

**Aldune.** El nombre aparece en el producto (ventanas, ajustes, avisos, bandeja del sistema), en la
estructura del código (proyectos, carpetas, namespaces, solución), en el ejecutable y el instalador,
y en la infraestructura de sincronización (contenedores, variables de entorno, prefijo de los
códigos de perfil).

## Historial de nombres

| Periodo | Nombre | Qué queda de esa época |
|---|---|---|
| hasta 2026-09-15 | Fanote | Solo compatibilidad de datos y formatos (ver «Lo que NO hay que renombrar»). |
| desde 2026-09-15 | Aldune | Nombre vigente. |

## Fuente única de verdad: `BrandIdentity`

Casi todo lo que se puede cambiar sin tocar lógica vive en
[`src/Aldune.Core/BrandIdentity.cs`](../src/Aldune.Core/BrandIdentity.cs). Es el primer sitio que hay
que mirar antes de buscar cadenas por el repositorio:

| Constante | Valor actual | Para qué sirve |
|---|---|---|
| `AppName` | `Aldune` | Nombre visible. Lo usan `Strings.AppName` (títulos y avisos que pasan por ahí) y `AppInfo.DisplayVersion` (tooltip de la bandeja). |
| `AppDataDirectoryName` | `Aldune` | Carpeta de datos en `%LOCALAPPDATA%`. |
| `ExecutableName` | `aldune.exe` | Comprobación del arranque automático en `StartupRegistration`. |
| `StartupRegistryKey` | `Aldune` | Valor en `HKCU\...\CurrentVersion\Run`. |
| `WindowMessageClassName` | `AlduneHotkey` | Clase de la ventana solo-mensajes del atajo global (`GlobalHotkey`). |
| `DatabaseFileName` | `notes.db` | Fichero SQLite dentro de la carpeta de datos. |
| `SyncProfileCodePrefix` | `aldune-profile-v2:` | Prefijo de los códigos de perfil que emite `SyncShareCodeCodec`. |
| `SyncTokenEnvVar`, `SyncTokensEnvVar`, `SyncPortEnvVar`, `SyncDataDirEnvVar`, `MonitorIndexEnvVar` | `ALDUNE_*` | Variables de entorno del cliente y del servidor. |
| `Legacy*` | `Fanote`, `FANOTE_*`, `fanote-profile-*` | Compatibilidad con lo anterior: **no se toca** al renombrar. |

El icono vive en `src/Aldune/Assets/`: `aldune.ico` (el que usan el ejecutable y el instalador) y los
`.svg` de origen (`aldune-logo.svg`, `aldune-logo-small.svg`).


## Dónde más aparece el nombre (no sale de `BrandIdentity`)

Estos sitios hay que cambiarlos a mano, o con `scripts/rename-brand.ps1`, que los cubre todos:

| Área | Ficheros |
|---|---|
| Proyectos, carpetas y solución | `Aldune.slnx`, `src/Aldune/`, `src/Aldune.Core/`, `src/Aldune.SyncServer/`, `tests/Aldune.Core.Tests/` y sus `.csproj` |
| Namespaces y clases | `namespace Aldune...`, `using Aldune...`, `x:Class="Aldune..."`, `clr-namespace:Aldune...` |
| Metadatos del ejecutable | `src/Aldune/Aldune.csproj` (`AssemblyName`, `Product`, `AssemblyTitle`, `Company`, `Description`, `ApplicationIcon`) y `src/Aldune/app.manifest` (`Aldune.app`) |
| Textos de interfaz con el nombre escrito a mano | `src/Aldune/Resources/Strings.cs` (una treintena de literales), `src/Aldune/Windowing/NoteWindow.xaml` (título de la ventana) y `src/Aldune/Windowing/NotesManagerWindow.xaml.cs` (nombre del ZIP exportado) |
| Paleta del *chrome* oscuro | Claves de recurso de `src/Aldune/App.xaml` (`AlduneGroundBrush` y compañía) y sus usos en los `.xaml` de las ventanas |
| Perfil de publicación | `src/Aldune/Properties/PublishProfiles/portable.pubxml` |
| Instalador | `installer/Aldune.iss` (nombre del fichero y defines `MyAppName`, `MyAppPublisher`, `MyAppExeName`, `DefaultDirName`, `OutputBaseFilename`) |
| Scripts de compilación | `scripts/build-installer.ps1` (salida del *publish*, nombre del ZIP, limpieza del portable antiguo) |
| Sincronización | `docker-compose.sync.yml`, `docker-compose.sync.nginx.yml`, `docker-compose.sync.https.yml`, `Caddyfile`, `.env.example`, `src/Aldune.SyncServer/Dockerfile` y `src/Aldune.SyncServer/Program.cs` |
| CI y publicación | `.github/workflows/ci.yml` y `.github/workflows/release.yml` |
| Documentación | `README.md` y `docs/*.md` |
| Icono y marca | `src/Aldune/Assets/aldune.ico`, `aldune-logo.svg`, `aldune-logo-small.svg` |

## Lo que NO hay que renombrar (compatibilidad)

Renombrar cualquiera de estos hace que las instalaciones existentes pierdan notas o dejen de
sincronizar. Están marcados como `Legacy*` a propósito:

| Elemento | Valor | Por qué se conserva |
|---|---|---|
| `LegacyAppDataDirectoryName` | `Fanote` | `App.xaml.cs` migra `%LOCALAPPDATA%\Fanote` a la carpeta nueva una sola vez, en el primer arranque. |
| `LegacyStartupRegistryKey` | `Fanote` | Migración del valor anterior del arranque automático. |
| `LegacySyncProfileCodePrefixes` | `fanote-profile-v2:`, `fanote-profile-v1:` | Los códigos de perfil ya compartidos siguen importándose. |
| `LegacySync*EnvVar`, `LegacyMonitorIndexEnvVar` | `FANOTE_*` | `.env` y servidores ya desplegados. |
| Fallbacks de `FANOTE_*` en `SyncService`, `App.xaml.cs` y `SyncServer/Program.cs` | — | Leer el token de un `.env` o de un pegado antiguo. |
| Nombre físico del volumen Docker | `fanote-sync-data` | Los datos del servidor viven ahí; el alias lógico ya se llama `aldune-sync-data`. |
| Rutas `docs/superpowers/**/…fanote…md` | — | Son documentos históricos fechados: el nombre del fichero forma parte del histórico y hay enlaces que apuntan a él. |
| Borrado de `fanote.exe` en `build-installer.ps1` | — | Limpieza del portable antiguo si quedó en `publish/portable`. |

## Comprobar que no quedan restos

Lo más rápido es el informe del script de renombrado, que no escribe nada:

```powershell
./scripts/rename-brand.ps1 -NewName "NombreDePrueba" -DryRun
```

O una búsqueda directa:

```powershell
Get-ChildItem -Recurse -File |
  Where-Object { $_.FullName -notmatch '\\bin\\|\\obj\\|\\.git\\|graphify-out|worktrees|\\publish\\|\\dist\\' } |
  Select-String -Pattern 'Fanote|fanote|FANOTE' |
  ForEach-Object { "$($_.Path):$($_.LineNumber): $($_.Line.Trim())" }
```

Los únicos resultados aceptables son los de la tabla «Lo que NO hay que renombrar».
