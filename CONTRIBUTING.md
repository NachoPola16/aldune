# Contribuir

Los **issues** (fallos, dudas, ideas) son bienvenidos: di qué versión usas, qué hiciste y qué esperabas. Para preguntas e ideas abiertas,
usa [Discussions](https://github.com/NachoPola16/aldune/discussions).

Antes de enviar una Pull Request, abre un issue para hablarlo: el proyecto tiene decisiones de diseño documentadas en [docs/](docs/)
(`ROADMAP.md` recoge lo que se descartó y por qué; `STATUS.md`, lo que hay hecho) y las reglas para quien toque el código, más abajo.

## Reglas del proyecto

- **Textos de interfaz** siempre con `Strings.T(en, es, de, fr, pt)` (`src/Aldune/Resources/Strings.cs`): los cinco idiomas, nada escrito a mano en XAML ni en código.
- **Lógica nueva en `Aldune.Core`, con el test primero.** La interfaz se verifica con una sonda desechable ([docs/WPF_PROBES.md](docs/WPF_PROBES.md); Android: [docs/ANDROID_PROBES.md](docs/ANDROID_PROBES.md)).
- **Comentarios en español**, que expliquen el porqué (el código ya dice el qué).
- **Colores**: de las paletas y temas (`AppPalette`, `NoteThemes`), nunca hex sueltos en XAML. Logo e iconos salen de `scripts/branding/generar-iconos.py`, no se retocan a mano ([docs/BRANDING.md](docs/BRANDING.md)).
- **Nunca ejecutes la app de desarrollo contra `%LOCALAPPDATA%\Aldune`**: son las notas reales; para probar, una base de datos temporal.
- **Compatibilidad de datos**: un `settings.json` antiguo tiene que cargar sin migración, y no se cambia el formato de sincronización sin pensar en las versiones instaladas.
- Compilar y probar: `dotnet build Aldune.slnx -c Debug` y `dotnet test Aldune.slnx --no-build` (Android: `./gradlew test` desde `android/`, JDK 21).

## Licencia de lo que envíes

Al enviar código aceptas que se publique bajo [GPL-3.0](LICENSE) y que quien mantiene el proyecto pueda **redistribuirlo también bajo otras
licencias** (por ejemplo, para las tiendas de aplicaciones). Firma tus commits con `Signed-off-by` (Developer Certificate of Origin,
<https://developercertificate.org>): con ello confirmas que el código es tuyo o que tienes derecho a enviarlo.

## Lo que no se acepta sin hablarlo antes

- Cambios en el formato de sincronización (el formato es el 4; las versiones instaladas rechazan lo que no entienden).
- Código que envíe datos fuera del dispositivo o añada telemetría, anuncios o rastreadores.
