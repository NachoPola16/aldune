# Prompt de nube: F4 tarea 2 — módulo `android/domain` (reglas de las notas)

> Para pegar en una sesión de Claude Code en la nube (Sonnet, esfuerzo medio) sobre `NachoPola16/aldune`. Presupuesto: ~8-10 $.
> Requisito: F3 (`android/text`) en `main` (hecho). Se borra cuando se fusione el resultado.

```
Lee AGENTS.md, docs/superpowers/specs/2026-10-17-aldune-android-f4-design.md (§1 entera, es tu encargo) y docs/superpowers/specs/2026-10-17-aldune-android-design.md
(§1, §2.1, §2.5, §3), docs/INFORME-ANDROID-F3.md y el módulo android/text (build.gradle.kts, settings.gradle.kts y cómo se usan sus objetos).
La referencia de comportamiento está en src/Aldune.Core: Note.cs, NotesRepository.cs (SetTags, orden del mazo, papelera y purga, borrado definitivo y lápidas,
qué ediciones ponen SyncAuthorDeviceId = NULL), NoteEquivalence.cs, NoteListing.cs, NoteLabels.cs, NoteSearch.cs. Léelos, NO los modifiques.

IDENTIDAD GIT (obligatorio antes del primer commit):
  git config user.name "NachoPola16"
  git config user.email "nachopolac@gmail.com"
Sin Co-Authored-By ni mención a Claude/Anthropic. Antes de terminar: git log --format='%an <%ae> | %cn <%ce>' origin/main..HEAD
debe mostrar solo NachoPola16 <nachopolac@gmail.com>. Trabaja en la rama de sesión que te dejen empujar; no abras PR ni toques main.

ENTORNO: JDK 21 y Gradle del sistema (sin wrapper, sin binarios en el repo). Módulo nuevo android/domain, Kotlin/JVM puro, sin Android, mismo
build.gradle.kts que android/text (Java 11 de destino), paquete app.aldune.domain, dependiendo de android/text con includeBuild("../text") y la
coordenada app.aldune:aldune-text (mira cómo android/sync hace lo mismo con core; si android/text aún no declara group/version, añádeselos mínimamente
SIN cambiar nada más). Añade .gitignore para build/ y .gradle/ (en F3 se coló). android/core, android/sync y android/text no se tocan salvo lo dicho.
No modifiques src/, tests/Aldune.* ni tests/vectors/*.json. Comentarios en español, explicando el porqué.

TDD: test primero, rojo, luego implementación. El reloj, el generador de ids y el deviceId SE INYECTAN (interfaz Clock propia, nada de Instant.now() suelto).

TAREAS (commit + push + informe tras cada una; si te quedas sin margen, para tras la última completa):
1. Modelo Note, NoteState, LineEnding, interfaz NoteStore y InMemoryNoteStore (con lápidas locales), interfaz NoteCipher (solo la interfaz).
2. NoteService.edit: normaliza CRLF→LF para trabajar y guarda con el lineEnding original; si el texto no cambia NO toca UpdatedAt ni escribe;
   si cambia, UpdatedAt = clock.now y syncAuthorDeviceId = null. Nota nueva: LF. create() solo se llama en la primera edición con texto.
3. Color, etiquetas (máx. 20, sin duplicados ignorando mayúsculas, conserva la forma recibida —copia SetTags de Core, incluidos sus bordes—), estados
   (archive, trash, restore, deleteForever solo desde la papelera y dejando lápida local, purgeTrashed(days) con 30 por defecto). Todo cambio local pone
   UpdatedAt y syncAuthorDeviceId = null, como en Core: comprueba en NotesRepository.cs qué operaciones lo hacen y cuáles no, y cópialo; si algo no te cuadra, anótalo.
4. Orden del mazo: move(id, beforeId, afterId) con NoteOrdering.between/spaced de android/text (renumera si between devuelve null) y list(filter, query, tag)
   con el orden EXACTO de Core (cópialo de NotesRepository/NoteListing; las notas sin posición, igual que Core) y NoteSearch de android/text.
5. NoteService.create con el color de NoteColorAssigner (colores del mazo activo en su orden), tema y ajustes pasados como parámetro.
6. Tests de robustez y composición: abrir y cerrar sin editar no cambia nada; CRLF de ida y vuelta; ediciones concurrentes sobre la misma nota con un store en memoria
   (la última escritura gana pero nunca se pierde una versión más nueva por una más vieja si llegan desordenadas: define la regla y pruébala); ids y estados desconocidos
   devuelven error claro, no excepción genérica.

INFORME: docs/INFORME-ANDROID-F4-DOMAIN.md con el esquema de INFORME-ANDROID-F3.md (entorno, casilla por tarea, diferencias con Core, sin hacer, cifras de tests).
Al final: `cd android/domain && gradle test` completo en verde, comprobación de identidad, push y resumen de 10 líneas.
```
