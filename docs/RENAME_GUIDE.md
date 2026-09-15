# Guía de rebranding

Cómo cambiar el nombre de la aplicación sin romper nada. El mapa de dónde vive cada cosa está en
[`BRANDING.md`](BRANDING.md); esta guía es el procedimiento.

## 1. Solo el nombre visible

Cambia `AppName` en `src/Aldune.Core/BrandIdentity.cs`:

```csharp
public const string AppName = "MiNombre";
```

Con eso cambian de golpe todos los textos que pasan por `Strings.AppName`: el tooltip de la bandeja
(`AppInfo.DisplayVersion`), los globos de recordatorio y el título de los diálogos.

**Pero no todo.** Hay textos con el nombre escrito a mano que no se enteran del cambio:
`src/Aldune/Resources/Strings.cs`, el título de `src/Aldune/Windowing/NoteWindow.xaml` y el nombre
del ZIP exportado en `src/Aldune/Windowing/NotesManagerWindow.xaml.cs`. Si solo quieres la versión
rápida, edítalos a mano. Si además vas a cambiar la carpeta de datos, el ejecutable, las variables
de entorno o el instalador, haz el cambio completo de la sección 2.

## 2. Rebranding completo (script)

```powershell
# 1. Ver qué cambiaría, sin escribir nada
./scripts/rename-brand.ps1 -NewName "MiNombre" -DryRun

# 2. Aplicarlo
./scripts/rename-brand.ps1 -NewName "MiNombre"
```

`-OldName` se lee por defecto de `BrandIdentity.AppName`, así que normalmente no hace falta pasarlo.

Qué hace:

- Reescribe las tres formas del nombre (`MiNombre`, `minombre`, `MINOMBRE`) en código C#, XAML,
  perfiles de publicación, instalador, compose/Caddyfile, workflows, scripts y documentación,
  conservando la codificación (BOM) y los finales de línea de cada fichero.
- Renombra con `git mv` la solución, las carpetas de proyecto, los `.csproj`, el `.iss` y los assets.
- Al terminar, enumera lo que aún dice el nombre antiguo para que se pueda revisar uno a uno.

Qué **no** hace, y hay que hacerlo a mano:

- **Registrar la compatibilidad, y hacerlo al final.** Los códigos de perfil llevan la marca dentro
  del prefijo. Hazlo **después** de ejecutar el script y **a mano**, porque el script reescribiría
  cualquier valor heredado que contenga el nombre vigente: añade el prefijo que acabas de dejar atrás
  a `BrandIdentity.LegacySyncProfileCodePrefixes` (con el cambio Fanote → Aldune eso fue
  `"aldune-profile-v2:"`) para que los códigos ya compartidos se sigan importando.
- **Rehacer el histórico.** El script reescribe también la tabla «Historial de nombres» de
  `BRANDING.md` y la sección 4 de esta guía, que son un registro: vuelve a dejarlas como estaban y
  añade la fila o el bloque nuevos.
- **Rediseñar el icono.** El `.ico` de `src/Aldune/Assets/` no se regenera solo.
- **Cambiar el `AppId` del instalador.** `installer/*.iss` lleva un GUID fijo: si lo cambias, el
  instalador crea una segunda entrada en «Aplicaciones instaladas» en vez de actualizar la que ya
  existe.
- **Cambiar el nombre físico del volumen de Docker.** `fanote-sync-data` guarda los datos ya
  sincronizados del servidor; renombrarlo los deja huérfanos.

## 3. Lista de comprobación posterior

- [ ] `dotnet build Aldune.slnx -c Release`
- [ ] `dotnet test Aldune.slnx -c Release`
- [ ] `./scripts/rename-brand.ps1 -NewName "MiNombre" -DryRun` y revisar la lista: solo deben
      aparecer los restos de la tabla «Lo que NO hay que renombrar» de `BRANDING.md`.
- [ ] Arrancar la app publicada y mirar el título de una nota, el tooltip de la bandeja y Ajustes.
- [ ] `./scripts/build-installer.ps1` y comprobar los nombres generados en `dist/`.
- [ ] Si se cambió la carpeta de datos: comprobar que la migración la mueve y que las notas siguen.
- [ ] Importar un código de perfil antiguo para verificar la compatibilidad.

## 4. Cómo se hizo el cambio Fanote → Aldune

Resumen del cambio real (2026-09-15). Sirve de ejemplo de lo que suele quedar a medias:

1. `BrandIdentity` ya tenía los valores nuevos, pero quedaban restos: comentarios y textos que decían
   «Fanote», y constantes declaradas y **sin usar** (`ExecutableName`, `WindowMessageClassName`,
   `DatabaseFileName`) mientras el código llevaba esos literales a mano.
2. Se sustituyeron los literales por las constantes centrales (`GlobalHotkey`, `StartupRegistration`,
   `App.xaml.cs`) y se corrigieron referencias obsoletas en comentarios (`Fanote.Core.*` →
   `Aldune.Core.*`, `Fanote.Resources.*` → `Aldune.Resources.*`).
3. Los textos de interface que decían `FANOTE_SYNC_TOKEN=` pasaron a `ALDUNE_SYNC_TOKEN=` (el valor
   de `.env` que el usuario tiene que copiar).
4. El códec de códigos de perfil emitía `fanote-profile-v2:` aunque `BrandIdentity` declaraba
   `aldune-profile-v2:`. Ahora emite el prefijo de `BrandIdentity` y sigue aceptando los antiguos,
   con un test que lo fija (`SyncShareCodeTests.LegacyV2ProfileCodeStillImports`).
5. Se añadieron los metadatos de marca al `.csproj` (`Product`, `AssemblyTitle`, `Company`,
   `Description`): sin ellos Windows muestra `aldune` en minúsculas en las propiedades del `.exe`.
6. Se conservó **todo** lo `Legacy*`, el volumen `fanote-sync-data` y las referencias a los
   documentos históricos de `docs/superpowers/`.
7. Se dejó `scripts/rename-brand.ps1 -DryRun` y estos dos documentos para no volver a buscar a mano.

Compatibilidad del punto 4: una versión anterior a este cambio no puede importar un código de perfil
emitido después (el prefijo es otro), pero la versión nueva sí lee todos los códigos antiguos. Las
notas, los perfiles ya configurados y la sincronización en curso no se ven afectados.
