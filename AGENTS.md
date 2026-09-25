# AGENTS.md — Aldune

Instrucciones para agentes de código (Claude Code, Codex, Cursor…). El detalle vive en `docs/`; esto es
lo que hay que saber antes de tocar nada.

## Qué es

App de notas adhesivas para Windows (WPF, .NET 10): un dock de pestañas en un borde de la pantalla,
notas como ventanas propias, sincronización opcional cifrada de extremo a extremo. Versión publicada:
**1.0.0**.

- `src/Aldune.Core`: lógica pura sin WPF (modelo, repositorio SQLite, cifrado, sync, temas, texto).
  Todo lo que se pueda probar va aquí.
- `src/Aldune`: la app WPF (`Windowing/`, `Interop/` para Win32, `Resources/Strings.cs`).
- `src/Aldune.SyncServer`: servidor de sync (ASP.NET Core minimal API, Docker).
- `tests/Aldune.Core.Tests`: xUnit. `tests/Aldune.Ui.SmokeTests`: prueba de UI con ventanas reales.

Antes de un cambio grande, leer `docs/STATUS.md` (qué hay hecho y por qué), `docs/ROADMAP.md` (qué se
descartó y por qué) y la especificación de `docs/superpowers/specs/` que toque.

## Comandos

```powershell
dotnet build Aldune.slnx -c Debug
dotnet test Aldune.slnx --no-build          # ~660 tests, deben pasar todos
dotnet run --project tests/Aldune.Ui.SmokeTests -c Debug   # mueve el ratón: no tocarlo
./scripts/build-installer.ps1               # portable + instalador en dist/ (Inno Setup 6)
```

Los 4 avisos CA1416 de `DatabaseKeyProvider` (DPAPI) son conocidos; no debe aparecer ninguno más.

## Reglas del proyecto

- **Textos de interfaz siempre con `Strings.T("inglés", "español")`** en `src/Aldune/Resources/Strings.cs`.
  Nada escrito a mano en XAML ni en código.
- **Comentarios en español**, explicando el porqué (el código ya dice el qué), con la densidad del
  código que los rodea.
- **Lógica nueva en Core con test primero** (TDD). La UI se verifica con sondas (ver abajo).
- **Codificación**: los `.cs`/`.xaml` llevan BOM UTF-8; la documentación, no. `.gitattributes` normaliza
  los saltos de línea a LF: no hace falta convertirlos a mano, pero no hay que mezclar dos BOM.
- **Compatibilidad de datos**: un `settings.json` antiguo tiene que cargar sin migración (valores por
  defecto que funcionen cuando falta un campo; los enums se escriben como número). El formato de sync
  es el **4**: no subirlo sin necesidad, porque las versiones anteriores rechazan lo que no entienden.
- **Seguridad de la sync**: todo lo que viaja va cifrado con AES-GCM, incluidos los borrados (firmados).
  Un borrado sin firma nunca borra (manda a la papelera). Ver `docs/SYNC.md` → "Modelo de seguridad".
- **Nunca ejecutar la app de desarrollo contra `%LOCALAPPDATA%\Aldune`**: son las notas reales del
  usuario. Para probar, usar una base de datos temporal (como hacen las sondas y el smoke test).
- **Colores de nota**: por temas (`NoteThemes`, `NoteColorDerivation`), nunca hex sueltos; el borde y la
  etiqueta se derivan del color de la cara.
- **Pantallas**: la elegida se identifica por `MonitorInfo.StableId`, nunca por su posición en la lista
  de Windows (cambia al apagar y encender monitores).
- Commits sin la línea `Co-Authored-By`.

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
