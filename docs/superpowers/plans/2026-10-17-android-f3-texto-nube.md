# Prompt de nube: F3 — módulo `android/text` (texto, color, orden, búsqueda)

> Para pegar en una sesión de Claude Code en la nube (Sonnet, esfuerzo medio) sobre `NachoPola16/aldune`. Presupuesto: ~8 $.
> Requisito: los vectores `tests/vectors/text-lines.json`, `text-color.json` y `text-order.json` en `main` (commiteados y subidos).
> Se borra cuando se fusione el resultado.

```
Lee AGENTS.md, docs/superpowers/specs/2026-10-17-aldune-android-design.md (§1, §2.1, §2.2, §3, §5 fila F3), tests/vectors/README.md (sección
«Vectores de texto, color y orden») y el patrón de android/sync (build.gradle.kts, settings.gradle.kts y cómo ScenarioHarness.kt y PlanScenarioTest.kt leen los vectores).
La referencia de comportamiento está en src/Aldune.Core: TaskLines, BulletLines, ListPrefix, ListIndent, LineMovement, LineText, TaskLists, TextEdit,
NoteTitleHelper, NoteText, OklchColor, NoteColorContrast, NoteColorDerivation, NoteThemes, NoteTheme, NoteDisplayColor, NoteColorAssigner, NoteOrdering.
Léelos, NO los modifiques.

IDENTIDAD GIT (obligatorio antes del primer commit):
  git config user.name "NachoPola16"
  git config user.email "nachopolac@gmail.com"
Sin Co-Authored-By ni mención a Claude/Anthropic. Antes de terminar: git log --format='%an <%ae> | %cn <%ce>' origin/main..HEAD
debe mostrar solo NachoPola16 <nachopolac@gmail.com>. Trabaja en la rama de sesión que te dejen empujar; no abras PR ni toques main.

ENTORNO: JDK 21 y Gradle del sistema (sin wrapper, sin binarios en el repo). Módulo nuevo android/text, Kotlin/JVM puro, sin dependencias de Android,
con el mismo build.gradle.kts que android/sync (Java 11 de destino, JUnit 5, kotlinx-serialization-json solo en test para leer los vectores).
Paquete app.aldune.text. android/core y android/sync NO se tocan (el test de android/core reescribe tests/vectors/kotlin-output.json: no lo
commitees, restáuralo con git checkout). No modifiques src/, tests/Aldune.* ni tests/vectors/*.json. Comentarios en español, explicando el porqué.
Sin Android Studio ni SDK: nada de android.* ni de librerías de Android.

CÓMO TRABAJAR (TDD, los vectores son el contrato):
- Primero el lector de vectores y un test parametrizado que, por cada caso de un fichero, ejecuta la operación `op` con sus `args` y compara con
  `expected` (un `when (op)` que espeja el `Execute` de TextVectorsTests.cs; léelo, es el mapa exacto de operaciones, argumentos y formas de
  resultado). Verlo rojo con todo sin implementar; después implementa por grupos hasta el verde. Un fallo debe imprimir op, args, esperado y obtenido.
- Los ficheros son grandes (≈1,6 MB): NO los abras enteros; mira con grep/head los casos de la operación que implementas.
- Los índices son unidades UTF-16, las de String de Kotlin: no conviertas a code points. Usa los mismos algoritmos que Core (mismas comprobaciones
  y mismo orden), no una reinterpretación: las rarezas están fijadas por los vectores (lista en el README) y no se «arreglan».
- Nombres: objetos Kotlin con el nombre de la clase de Core (TaskLines, BulletLines, ListPrefix, ListIndent, LineMovement, TaskLists, NoteTitleHelper,
  NoteText, OklchColor, NoteColorContrast, NoteColorDerivation, NoteThemes, NoteDisplayColor, NoteColorAssigner, NoteOrdering) y métodos en camelCase.
  Resultados con clases de datos (TextAndCaret, TextEdit con `mapIndex(Int)`, etc.). `null` donde Core devuelve null.
- Saltos de línea: las funciones trabajan sobre el texto tal cual (LF, CRLF o mezcla) como Core; no normalices. El `newLine` de EnterContinuation es parámetro.
- NoteTitleHelper: el texto de relleno es un parámetro (`placeholder`), no estado global; los vectores usan "New note".
- Reales: compara con tolerancia relativa 1e-9; los #RRGGBB, exactos. Si algún hex difiere en una unidad por culpa de Math.cbrt/Math.pow de la JVM frente a .NET,
  NO ensanches la tolerancia: apunta el caso en el informe y para ahí esa parte.
- OklchColor.toHex: la búsqueda binaria de croma es de 30 iteraciones exactas y la tolerancia de gama 1e-4; copia las constantes. ToByte redondea con
  Math.Round de .NET (por defecto, mitad al par): en Kotlin, `Math.rint`, no `roundToInt`.
- Los colores de los temas de serie están en NoteThemes.cs; cópialos literales y deja que text-color.json (caso NoteThemes.BuiltIn y Legacy) los compruebe.

TAREAS (commit + push + informe tras cada una; si te quedas sin margen, para tras la última completa):
1. Lector de vectores + lista y tareas: LineText (interno), TaskLines, BulletLines, ListPrefix, ListIndent, LineMovement. (text-lines.json)
2. TextEdit y TaskLists (UncheckAll, RemoveChecked, SettleToggled, ConvertMarkdownTasks —la expresión regular de Core en Regex de Kotlin con
   MULTILINE—, PendingTasks, Count), NoteTitleHelper (título con almohadillas, vista previa, vista previa de pestaña) y NoteText. (text-lines.json)
3. Color: OklchColor, NoteColorContrast, NoteColorDerivation, NoteThemes (BuiltIn, Legacy, Resolve, Sanitize), NoteTheme, NoteDisplayColor,
   NoteColorAssigner (con los enums como Int en el vector). (text-color.json)
4. Orden: NoteOrdering (Between, Spaced, SlotShift, TargetIndex). (text-order.json)
5. Búsqueda: NoteSearch.matches(texto, consulta) y NoteSearch.matchesNote(texto, etiquetas, consulta). NO hay vectores (Core no pliega acentos, Android sí:
   decisión de la spec §2.1). Tests escritos a mano: sin distinguir mayúsculas ni acentos («cafe» encuentra «Café»; «nino» encuentra «niño»), consulta vacía o de
   espacios coincide con todo, recorta la consulta, busca también en etiquetas, no confunde letras distintas (n/ñ solo se pliegan por acento, documentado), maneja
   emoji y texto en alemán/francés/portugués. Plegado con java.text.Normalizer (NFD) + quitar marcas combinantes (\p{Mn}) + minúsculas con Locale.ROOT.
6. Pruebas propias de Kotlin donde los vectores no llegan: composición ToggleCheckboxAt → SettleToggled como la usará la app (marcar y recolocar, con el cursor mapeado
   con mapIndex), idempotencia de UncheckAll, y que ninguna función lanza con cursores fuera de rango o texto vacío.

INFORME: docs/INFORME-ANDROID-F3.md con el esquema de INFORME-ANDROID-F2.md (entorno, casilla por tarea, diferencias con Core, sin hacer, cifras de tests).
Actualízalo y commitéalo tras cada tarea. Al final: `cd android/text && gradle test` completo en verde, comprobación de identidad, push y resumen de 10 líneas.
```
