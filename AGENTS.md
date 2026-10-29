# AGENTS.md — Aldune

Instrucciones para agentes de código (Claude Code, Codex, Cursor…). El detalle vive en `docs/`; esto es
lo que hay que saber antes de tocar nada.

## Qué es

App de notas adhesivas para Windows (WPF, .NET 10): un dock de pestañas en un borde de la pantalla,
notas como ventanas propias, sincronización opcional cifrada de extremo a extremo. Versión publicada:
**2.0.0** (la versión vive en `src/Aldune/Aldune.csproj`).

- `src/Aldune.Core`: lógica pura sin WPF (modelo, repositorio SQLite, cifrado, sync, temas, texto).
  Todo lo que se pueda probar va aquí.
- `src/Aldune`: la app WPF (`Windowing/`, `Interop/` para Win32, `Resources/Strings.cs`).
- `src/Aldune.SyncServer`: servidor de sync (ASP.NET Core minimal API, Docker).
- `android/`: cliente Android nativo (**Kotlin**, no MAUI), con una **raíz Gradle común** (`android/settings.gradle.kts`, wrapper con Gradle 9.6.0 fijado por hash, AGP 9.4.1,
  Kotlin 2.4.21; versiones en `android/gradle/libs.versions.toml`). Módulos: `core` (formato de sync, cifrado, notas protegidas, códigos de perfil), `sync` (motor y transportes
  HTTP/WebDAV), `text` (formato de texto, color, orden, búsqueda, paleta de la app), `domain` (reglas de las notas), todos **Kotlin/JVM puro**, y `app` (Compose; en F4).
  Son un port del formato de `Aldune.Core`, no código compartido: su contrato son los vectores de `tests/vectors/` (ver su `README.md`).
  Se prueba desde `android/` con `./gradlew test` (JDK 21; el Gradle del sistema, 8.14, ya no vale: el wrapper baja el 9.6). En este PC: JDK 21 en
  `C:\Program Files\Eclipse Adoptium\jdk-21.0.12.101-hotspot` (exportar `JAVA_HOME`) y SDK en `%LOCALAPPDATA%\Android\Sdk` (`ANDROID_HOME`; `android/local.properties` lo apunta y no se versiona).
  Compilar e instalar la app: `./gradlew :app:assembleDebug` y `adb install -r app/build/outputs/apk/debug/app-debug.apk`. `compileSdk` 37, `targetSdk` 36, `minSdk` 26.
- `tests/Aldune.Core.Tests`: xUnit. `tests/Aldune.SyncServer.Tests`: el servidor de sync con WebApplicationFactory.
  `tests/Aldune.Ui.SmokeTests`: prueba de UI con ventanas reales.

Antes de un cambio grande, leer `docs/STATUS.md` (qué hay hecho y por qué), `docs/ROADMAP.md` (qué se
descartó y por qué) y la especificación de `docs/superpowers/specs/` que toque. Si el cambio toca el
cliente móvil o cualquier sistema que no sea Windows, leer además `docs/MOBILE_PORT.md` (Android e iOS) y
`docs/DESKTOP_PORT.md` (macOS, Linux, web, y Windows en ARM64): los dos son estudios cerrados, con lo que
se decidió **no** hacer y con qué razón, para no repetir la investigación ni volver a discutirla. La revisión
crítica de esos dos estudios va en `docs/MOBILE_PORT_REVIEW.md`.

## Comandos

```powershell
dotnet build Aldune.slnx -c Debug
dotnet test Aldune.slnx --no-build          # ~1400 tests, deben pasar todos
dotnet run --project tests/Aldune.Ui.SmokeTests -c Debug   # mueve el ratón: no tocarlo
./scripts/build-installer.ps1               # portable + instalador en dist/ (Inno Setup 6)
```

Los 4 avisos CA1416 de `DatabaseKeyProvider` (DPAPI) son conocidos; no debe aparecer ninguno más.

## Reglas del proyecto

- **Logo e iconos**: nunca se retocan a mano. El símbolo (la «A» de tres notas) y todos sus formatos salen de
  `scripts/branding/generar-iconos.py` (`pip install shapely pillow`); colores, reglas de uso y lo descartado en
  [`docs/BRANDING.md`](docs/BRANDING.md). Las cadenas de Android viven en `android/strings.tsv`: se editan ahí y se
  regeneran los XML con `python android/scripts/gen_strings.py` (`--check` comprueba que coinciden).
- **Textos de interfaz siempre con `Strings.T(en, es, de, fr, pt)`** en `src/Aldune/Resources/Strings.cs`
  (inglés, español, alemán, francés, portugués de Brasil; idiomas en `UiLanguages`). Nada escrito a
  mano en XAML ni en código. Un texto nuevo lleva los cinco; si falta uno se ve en inglés. Las
  traducciones en lote se pueden delegar (ver `STATUS.md`, idiomas) y se validan con un script.
- **Comentarios en español**, explicando el porqué (el código ya dice el qué), con la densidad del
  código que los rodea.
- **Lógica nueva en Core con test primero** (TDD). La UI se verifica con sondas (ver abajo).
- **Codificación**: los `.cs`/`.xaml` llevan BOM UTF-8; la documentación, no. `.gitattributes` normaliza
  los saltos de línea a LF: no hace falta convertirlos a mano, pero no hay que mezclar dos BOM.
- **Móvil (Android)**: Kotlin nativo, no MAUI (F-Droid no construye .NET hoy; ver `docs/MOBILE_PORT.md` §13). `minSdk` 26, `targetSdk` 36.
  El formato de sync se fija con `tests/vectors/` y lo comprueban **los dos lados** (`SyncVectorsTests` en C#, `android/core` en Kotlin): no
  se cambia el formato ni se regeneran los vectores sin pensar en las versiones instaladas. `android/core` no depende de Android.
- **Compatibilidad de datos**: un `settings.json` antiguo tiene que cargar sin migración (valores por
  defecto que funcionen cuando falta un campo; los enums se escriben como número). El formato de sync
  es el **4**: no subirlo sin necesidad, porque las versiones anteriores rechazan lo que no entienden.
- **Archivos vinculados**: Aldune nunca borra un archivo vinculado y nunca escribe sobre uno cuya huella no es la
  conocida. Ver la spec de notas vinculadas (`docs/superpowers/specs/2026-10-04-notas-vinculadas-archivos-design.md`).
- **Seguridad de la sync**: todo lo que viaja va cifrado con AES-GCM, incluidos los borrados (firmados).
  Un borrado sin firma nunca borra (manda a la papelera). Ver `docs/SYNC.md` → "Modelo de seguridad".
- **Nunca ejecutar la app de desarrollo contra `%LOCALAPPDATA%\Aldune`**: son las notas reales del
  usuario. Para probar, usar una base de datos temporal (como hacen las sondas y el smoke test).
- **Colores de nota**: por temas (`NoteThemes`, `NoteColorDerivation`), nunca hex sueltos; el borde y la
  etiqueta se derivan del color de la cara.
- **Colores de la app (chrome)**: siempre `{DynamicResource Aldune<Clave>Brush}` de la paleta de
  `AppPalette` (Core), nunca hex en XAML; desde código, `SetResourceReference` o `ThemeManager.Color`.
  Así funcionan los aspectos Oscuro/Claro/Pastel/Medianoche y el cambio en vivo. Una clave nueva va
  en Dark y en Light (Pastel y Medianoche se derivan solas); los tests fijan su contraste.
- **Pantallas**: la elegida se identifica por `MonitorInfo.StableId`, nunca por su posición en la lista
  de Windows (cambia al apagar y encender monitores).
- **Claude nunca figura como autor ni contribuidor**: commits sin la línea `Co-Authored-By` y con autor y committer
  `NachoPola16 <nachopolac@gmail.com>`, nunca `Claude <noreply@anthropic.com>`. Quien trabaje en la nube o con un
  subagente debe fijar `git config user.name/user.email` antes del primer commit y comprobar
  `git log --format='%an <%ae> | %cn <%ce>' origin/main..HEAD` antes de subir. (En la 1.6.0 se coló una tanda de 6 commits de
  la nube con autor Claude; quitarla costó reescribir el historial y mover los tags.)

## Subagentes

Cada subagente arranca sin el contexto de la conversación: delegar solo lo acotado y que devuelva un resumen. Al cerrar lógica nueva en Core (o un fix de un bug)
lanzar el agente `tests`; antes de una release o al tocar sync, cifrado o el servidor, el agente `seguridad`. Para localizar código en muchos ficheros, `Explore`.
Es una pauta, no un automatismo: no hay forma documentada de forzarla salvo con hooks.

## Verificar la interfaz: sondas

Los tests de Core no cubren WPF. Para comprobar un cambio de interfaz se escribe una **sonda
desechable** (fuera del repositorio) que abre ventanas reales de la app con datos temporales, hace
clics o teclas físicos y guarda capturas. La técnica y el código base están en
[`docs/WPF_PROBES.md`](docs/WPF_PROBES.md). Varios bugs de esta app solo se reproducían así (por
ejemplo, el menú "⋯" que no se cerraba: con eventos enrutados el fallo no aparece).

## Publicar

Ver [`docs/RELEASING.md`](docs/RELEASING.md). Una pre-release lleva guion en el tag (`v1.1.0-rc1`); sin
guion sale como versión normal y se ofrece a todos los usuarios. Publicar es una acción hacia fuera:
confirmarlo siempre con el usuario.
