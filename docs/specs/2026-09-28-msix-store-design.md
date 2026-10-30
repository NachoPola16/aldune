# Aldune — Paquete MSIX para la Microsoft Store (design spec)

Decidido con el usuario el 2026-09-28, después de que SignPath Foundation rechazara la solicitud de firma
gratuita (ver `STATUS.md`). Regla que se mantiene: solo vías gratuitas.

## Problema

Los binarios de GitHub van sin firmar y SmartScreen avisa de "editor desconocido". La única vía gratuita
que queda es la Microsoft Store: el registro de desarrollador individual es gratis y la Store firma los
paquetes MSIX con el certificado de Microsoft. Subir un `.exe`/`.msi` a la Store no sirve: exige que el
instalador ya venga firmado con un certificado de confianza.

## Objetivo y límites

- Aldune se puede instalar desde la Store, firmado, sin avisos.
- La descarga de GitHub (instalador y portable) no cambia y sigue sin firmar.
- Un solo código: la app detecta en tiempo de ejecución si corre dentro del paquete y se adapta. Sin
  rama ni proyecto aparte.
- Solo x64, autocontenido, como hoy.
- Fuera de alcance: subir a la Store de forma automática (API de envíos), ARM64, la ficha de la Store
  (textos y capturas; se hace en Partner Center).

## Decisiones

### Construcción: manifiesto a mano + `makeappx`

Descartados el proyecto de empaquetado de Visual Studio (`.wapproj`: necesita el MSBuild de VS y no
compila con `dotnet build`) y el MSIX de un solo proyecto del Windows App SDK (una dependencia entera
solo para empaquetar).

- `installer/msix/AppxManifest.xml`: plantilla con marcadores para versión e identidad.
- `installer/msix/identity.json`: los tres valores que da Partner Center (`Name`, `Publisher`,
  `PublisherDisplayName`). Hasta que exista la cuenta lleva valores de prueba (`CN=Aldune Dev`); al
  tenerla se cambian solo aquí.
- `installer/msix/Assets/`: PNG de los logotipos obligatorios (Square44x44, StoreLogo 50x50,
  Square150x150, Wide310x150; escalas 100 y 200; Square44x44 también `targetsize-*` sin placa),
  generados una vez desde `src/Aldune/Assets/aldune-logo*.svg` y guardados en el repositorio para que
  la build no dependa de un conversor de SVG.
- `scripts/build-msix.ps1`: mismo `dotnet publish` autocontenido x64 que `build-installer.ps1` (se
  extrae a una función común si el código se repite), copia publish + manifiesto + logotipos a una
  carpeta de trabajo, rellena la versión y la identidad, y ejecuta `makeappx pack` →
  `dist/Aldune-<versión>.msix`. Busca `makeappx`/`signtool` en `Windows Kits\10\bin\<última>\x64`.
- Versión del paquete: la del `.csproj` con cuarto número `0` (`1.4.1.0`); la Store lo exige.
- `-Sign`: crea (o reutiliza) un certificado autofirmado en `Cert:\CurrentUser\My` con el mismo
  `Publisher` que `identity.json` y firma el paquete para instalarlo en local. Para instalarlo hay que
  confiar en ese certificado (Personas de confianza del equipo; el script lo explica, no lo hace solo
  porque pide administrador). A la Store se sube sin firmar: firma Microsoft.
- Workflow de release: genera el `.msix` sin firmar y lo guarda como **artefacto de la ejecución**, no
  como archivo de la versión de GitHub (sin firma de confianza no se puede instalar). Lo sube a mano el
  usuario a Partner Center.

### Detección del paquete

`Interop/PackageContext.cs`: `IsPackaged` = `GetCurrentPackageFullName` (kernel32, P/Invoke) devuelve
algo distinto de `APPMODEL_ERROR_NO_PACKAGE`. Se calcula una vez. Sin WinRT para esto.

En Core, `DistributionChannel` (`GitHub` | `Store`) y `DistributionFeatures.For(channel)`: qué
arranque se usa y si se ofrece la consulta de versiones. Pequeño, pero con test, y la app no reparte
`if (IsPackaged)` por todas partes.

### Datos: la misma carpeta real

Decidido por el usuario: la versión de la Store usa `%LOCALAPPDATA%\Aldune`, la misma que la de GitHub.
Quien se cambia conserva sus notas y desinstalar desde la Store no las borra (como con Inno Setup).

- Manifiesto: capacidad restringida `rescap:unvirtualizedResources` y, en `Properties`,
  `desktop6:FileSystemWriteVirtualization` y `desktop6:RegistryWriteVirtualization` a `disabled`.
  El registro también, para poder borrar la entrada `Run` de la versión de GitHub (abajo).
- `ResolveAppDataDirectory` no cambia.
- El mutex `AlduneSingleInstance` no está aislado para apps de escritorio empaquetadas de plena
  confianza: las dos versiones no pueden correr a la vez sobre la misma carpeta. Se verifica en la sonda.
- Microsoft revisa las capacidades restringidas. Texto de justificación preparado en
  `installer/msix/README.md`: los datos del usuario deben sobrevivir a desinstalar y ser los mismos
  que los de la versión descargable.
- **Plan B, no se construye ahora**: si la rechazan, se quitan capacidad y propiedades; los datos van a
  la carpeta privada del paquete y hay que avisar (ficha y Ajustes) de que desinstalar borra las notas.

### Arranque con Windows

La clave `Run` no sirve desde el paquete: apuntaría a `C:\Program Files\WindowsApps\...`, que no se
puede lanzar directamente. Dentro del paquete se usa la extensión `desktop:StartupTask`
(`TaskId="AlduneStartup"`, `Enabled="false"`) y la API `Windows.ApplicationModel.StartupTask`.

- `StartupRegistration` pasa a una interfaz con dos implementaciones: `RunKeyStartup` (el código actual)
  y `PackagedStartup`. `App` elige según `DistributionFeatures`.
- La API es WinRT: el proyecto `Aldune` pasa a `net10.0-windows10.0.19041.0` (Core no cambia).
  Comprobar que no aparecen avisos nuevos ni cambia el tamaño del publish de forma notable.
- Estados de `StartupTask`: `Enabled`, `Disabled` (activable; la primera vez Windows muestra su propio
  diálogo), `DisabledByUser` (desactivado en el Administrador de tareas: la app **no** puede
  reactivarlo), `DisabledByPolicy`. En los dos últimos la casilla de Ajustes queda desactivada con un
  texto que explica dónde se cambia. Texto nuevo con `Strings.T` en los cinco idiomas.
- La API es asíncrona: la casilla se actualiza al terminar, sin bloquear el hilo de la interfaz.
- Al activar el arranque desde el paquete se borra la entrada `Run` de la versión de GitHub si existe
  (`AlduneStartup` y la heredada): si no, Windows lanzaría las dos al iniciar sesión y ganaría la
  primera al azar.

### Aviso de versiones nuevas

Dentro del paquete no se consulta GitHub (`UpdateNotifier` no arranca) y la opción no aparece en
Ajustes → Acerca de. Actualiza la Store, y sus normas no permiten actualizarse por otra vía. El ajuste
`CheckForUpdatesAutomatically` se queda como está en `settings.json` (lo usa la versión de GitHub si
el usuario vuelve a ella).

### Carpeta de datos para pruebas

Para probar el paquete sin tocar las notas reales hace falta otra carpeta de datos, y a una app
empaquetada no se le pasan argumentos con facilidad. `ALDUNE_DATA_DIR`: si existe, `App` la usa en
lugar de `%LOCALAPPDATA%\Aldune` (sin migración de la carpeta heredada). No se documenta para usuarios.

## Pruebas

- Core (TDD): `DistributionFeatures.For` por canal; lectura y validación de `identity.json`
  (`Publisher` con forma `CN=...`, `Name` sin espacios) y la conversión de versión a cuatro números
  con el último a 0. Si la validación vive en el script, se prueba desde Core con la misma regla.
- Build: `build-msix.ps1 -Sign` genera un paquete que `Add-AppxPackage` instala.
- Sonda (fuera del repo, con `ALDUNE_DATA_DIR` temporal): la app instalada desde el MSIX escribe en la
  carpeta indicada y no en una virtualizada; activar el arranque muestra el diálogo de Windows y queda
  `Enabled`; una entrada `Run` de prueba desaparece; Acerca de no muestra la consulta de versiones;
  abrir a la vez la versión de GitHub no crea una segunda instancia; desinstalar deja los datos.
- `dotnet test` completo y smoke test sin cambios (la versión de GitHub no debe notar nada).

## Lo que hace el usuario

1. Instalar el SDK de Windows (`winget install Microsoft.WindowsSDK.10.0.26100`).
2. Crear la cuenta de desarrollador individual en Partner Center (gratis, con verificación de
   identidad) y reservar el nombre "Aldune".
3. Pasar los tres valores de identidad (Gestión de productos → Identidad del producto) para
   `identity.json`.
4. Subir el primer `.msix`, con la justificación de `unvirtualizedResources` preparada.
5. Rellenar la ficha: descripción, capturas, clasificación por edades y URL de la política de
   privacidad (el párrafo de privacidad del README, publicado en una página).
