# Estudio: Aldune en la Microsoft Store como MSIX (2026-10-27)

Estudio de viabilidad, sin prototipo ejecutado. Lo que sale de la documentación de Microsoft va con su enlace; lo demás es mío y está marcado
**(sin verificar)**: hay que comprobarlo con un prototipo antes de comprometerse.

## Por qué MSIX

Con un instalador EXE/MSI sin empaquetar la Store **no lo firma** (hay que pagar un certificado de una CA de confianza de Microsoft). Con **MSIX**
la firma es gratuita, la distribución la aloja Microsoft y se puede usar el cobro de la Store o uno propio o de terceros. Es la única vía de firma
gratuita para vender en la Store. Fuente: tabla de
[How to distribute your Win32 app through Microsoft Store](https://learn.microsoft.com/en-us/windows/apps/distribute-through-store/how-to-distribute-your-win32-app-through-microsoft-store).

El alta de particular en la Store es gratuita desde 2025 (verificación con documento de identidad y selfie); la de empresa, 99 $.

## Qué cambia en Aldune (de lo más a lo menos serio)

| Aspecto | Hoy | Empaquetado en MSIX | Trabajo |
|---|---|---|---|
| **Datos** (`%LOCALAPPDATA%\Aldune`: `notes.db`, `settings.json`) | Carpeta real del usuario | Windows 10 1903+: los ficheros **nuevos** de `AppData` se escriben en una ubicación privada del paquete (se ven fusionados) y **se borran al desinstalar**; los ficheros que **ya existen** en la carpeta real se leen y modifican ahí. Por eso un paquete «recoge donde lo dejó» la versión sin empaquetar ([fuente](https://learn.microsoft.com/en-us/windows/msix/desktop/desktop-to-uwp-behind-the-scenes)) | Un usuario **nuevo** de la Store tendría sus notas en el almacén privado: desinstalar la app las borra. Mitigar con la copia local (`LocalBackup`) y la sync, avisarlo al desinstalar, o desvirtualizar `AppData` con una capacidad restringida **(sin verificar)** |
| **Arranque con Windows** | Valor `Run` en `HKCU` (`StartupRegistration`) | Las escrituras en `HKCU` se copian a un registro privado: **el valor `Run` no tendría efecto** | Declarar `windows.startupTask` en el manifiesto y activarlo con la API `StartupTask` (exige TFM `net10.0-windows10.0.19041` o superior). El usuario puede quitarlo desde el Administrador de tareas y la app ya no puede reactivarlo sola: cambia el texto de la casilla **(sin verificar los detalles)** |
| **«Abrir con» para `.md`, `.markdown`, `.txt`** | Claves en `HKCU\Software\Classes` que escribe el instalador | Va en el manifiesto (`uap:FileTypeAssociation`), no en el registro | Declararlo; la app ya recibe la ruta como argumento **(sin verificar que se pueda ser solo «Abrir con» sin ser el predeterminado)** |
| **Avisos de versión nueva** (`UpdateNotifier`, `ReleaseUpdateChecker`) | Consulta `releases/latest` de GitHub | Las actualizaciones las hace la Store cada 24 h | Desactivarlo cuando la app corra empaquetada (detectable con `GetCurrentPackageFullName`) **(la política de la Store seguramente lo exige; sin verificar)** |
| **Archivos vinculados** | Lee y escribe `.md` del usuario | Una app de plena confianza escribe fuera del paquete si el usuario tiene permiso ([fuente](https://learn.microsoft.com/en-us/windows/msix/desktop/desktop-to-uwp-behind-the-scenes)) | Ninguno; requiere declarar `runFullTrust` |
| Atajo global, instancia única (mutex), DPAPI, bandeja | Win32 normal | Deberían funcionar igual en plena confianza | Probar |
| **Instalación** | `%LOCALAPPDATA%\Programs\Aldune` (instalador) o carpeta portable | `C:\Program Files\WindowsApps\<paquete>` de solo lectura | Revisar que nada escriba junto al `.exe` (los registros ya van a `AppData`) |
| Aspecto, dock, notas, sync | — | — | Ninguno |

## Qué habría que construir

1. **`AppxManifest.xml`** (identidad, `runFullTrust`, `startupTask`, asociaciones de archivo, logos). La identidad (`Name` y `Publisher`) la da el
   Partner Center al reservar el nombre.
2. **Imágenes** del paquete (44, 150, 50 px…): salen de `scripts/branding/generar-iconos.py` (no se retocan a mano, ver `docs/BRANDING.md`).
3. **Empaquetado** con `makeappx.exe` (está en este PC, Windows SDK 10.0.26100) sobre la carpeta con `aldune.exe`. La Store firma el paquete que
   se sube; para probarlo en local hace falta uno firmado con un certificado de prueba y el modo desarrollador **(sin verificar el detalle)**.
4. **Un trabajo nuevo en `release.yml`** que genere el `.msix` junto al instalador. La subida a la Store sería manual en el Partner Center (o con
   la CLI `msstore`, que necesita registrar una aplicación en Azure AD; opcional).
5. **Ficha de la Store:** política de privacidad (una dirección pública), capturas, descripciones en los 5 idiomas (ya hay textos), clasificación por
   edades, y la declaración de la capacidad restringida `runFullTrust`.
6. **Código:** `StartupRegistration` y el registro de archivos pasan a depender de si hay identidad de paquete; el aviso de versiones se apaga empaquetado;
   y un ajuste para cambiar la carpeta de datos (`ALDUNE_DATA_DIR`), que además permite probar sin tocar las notas reales.

## Riesgos

- **Desinstalar borra las notas de un usuario nuevo de la Store** (ver Datos). Es el único riesgo que afecta a datos del usuario.
- **Dos copias de Aldune** (la de GitHub y la de la Store) comparten carpeta de datos, mutex y atajo: no deben ejecutarse a la vez. Hoy el mutex ya
  evita dos instancias, pero habría que decidir qué pasa con quien instala la segunda encima de la primera.
- **Certificación de la Store:** tarda días y puede rechazar el paquete por la capacidad `runFullTrust` o por el aviso de versiones.
- **Mantenimiento:** cada release hay que subirla también al Partner Center.

## Esfuerzo (mi estimación)

- Prototipo en local (manifiesto, empaquetado, arranque, asociaciones, datos): **1 sesión larga**.
- Código definitivo, trabajo de CI y pruebas de las dos formas de instalación: **1 a 2 sesiones más**.
- Ficha de la Store, política de privacidad y la certificación: **días de calendario**, poco trabajo mío.

## Recomendación

Merece la pena **solo si se va a vender o si se quiere la firma sin pagar certificado**. Si el objetivo es solo evitar el aviso de SmartScreen, es más
barato volver a pedir SignPath Foundation (gratis para código abierto) cuando el proyecto tenga más visibilidad. No empezar el prototipo sin antes
decidir el modelo de cobro (ver `_notas\ALDUNE-MERCADO-Y-NEGOCIO.md`), porque condiciona qué se declara en la ficha.

## Antes de empezar un prototipo (comprobar)

- Si se puede desvirtualizar `AppData` y con qué capacidad (y si la Store la admite sin justificación extra).
- Cómo queda el «Abrir con» sin convertirse en predeterminado, y cómo se maneja `StartupTask` desde .NET 10 con el TFM `windows10.0.19041`.
- Si la política de la Store permite un cobro propio dentro de una app gratuita y qué comisión (si alguna) se queda Microsoft.
- Qué se exige exactamente en la ficha de un particular (privacidad, dirección, estatus de comerciante).
