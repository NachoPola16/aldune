# Prompt para la sesión de nube — F6, parte JVM (`domain`: widget y compartir)

> Para pegar en Claude Code en la web (claude.ai/code), repo `NachoPola16/aldune`, rama nueva desde `main`. Modelo: **Sonnet, esfuerzo medio**.
> Tope de gasto **≈4 $** (créditos que caducan el 2026-11-05; saldo ≈18 $). Es una tarea pequeña: lo esperable es 1-2 $. Antes de lanzarlo, la spec
> de F6 tiene que estar subida a `main`.

---

Trabajas en el repositorio de Aldune (app de notas; cliente Android en Kotlin bajo `android/`). Tu tarea es la **parte de la fase F6 que es Kotlin/JVM puro**,
con TDD, en el módulo `android/domain`, sin nada que dependa de Android ni dibuje nada.

## Lee primero (en este orden)

1. `AGENTS.md` (reglas del proyecto; los comentarios van **en español** y explican el porqué).
2. `docs/superpowers/specs/2026-10-18-aldune-android-f6-design.md`: **es tu especificación**. Haz §3 entero y nada de §4.
3. El código: `android/domain/src/main/kotlin/app/aldune/domain/` (sobre todo `Note.kt`, `NoteListItem.kt`, `NoteService.kt`, `DotNetText.kt`) y sus tests
   (`NoteListItemTest.kt`, `NoteServiceOrderTest.kt`, `Fixtures.kt`) para copiar el estilo; de `android/text`, `NoteTitleHelper.kt` y `TaskLists.kt`
   (`convertMarkdownTasks`).

## Identidad de git (antes del primer commit, obligatorio)

```bash
git config user.name "NachoPola16"
git config user.email "nachopolac@gmail.com"
```

Ningún commit puede llevar `Co-Authored-By`, «Generated with Claude» ni nada parecido, y el autor y el committer tienen que ser esa identidad. Antes de cada push:
`git log --format='%an <%ae> | %cn <%ce>' origin/main..HEAD` solo puede mostrar `NachoPola16 <nachopolac@gmail.com>`.

## Entorno

- JDK 21. Desde `android/`: `./gradlew` (el wrapper baja Gradle 9.6.0, fijado por hash).
- **No hay SDK de Android.** Ejecuta solo los módulos JVM:
  `./gradlew :core:test :sync:test :text:test :domain:test :syncclient:test`. Si la configuración de `:app` falla por falta del SDK, mira cómo lo resuelve ya
  `android/settings.gradle.kts`; **no toques nada de `:app`.**
- Línea base **antes de cambiar nada**: apunta el número de tests de cada módulo (deben ser core 82, sync 119, text 100, domain 123, syncclient 181). Al acabar
  tienen que seguir todos, más los nuevos de `domain`.
- La suite regenera `tests/vectors/kotlin-output.json`: **descártalo** (`git checkout -- tests/vectors/kotlin-output.json`) antes de cada commit.

## Qué hacer (en este orden; un commit por paso, con mensaje en español)

1. **`DockOrder`** (spec §3.1): tests de la regla primero; luego extraer de `NoteService` y que `NoteService` lo use. Todos los tests de `NoteService*`
   siguen verdes **sin tocarlos**.
2. **`WidgetContent`** (spec §3.2): `WidgetStrip` (sellado, `Open` y `Locked`), `WidgetContent`, `WidgetOptions`, `WidgetContentBuilder`. Un test por regla
   (1-9) y el test de propiedad de la regla 3 (marcador en `text`, etiquetas y `protectedContentJson` de protegidas; con y sin `showPreview`; ningún
   `toString()` del resultado lo contiene). Construye las notas como lo hacen los tests existentes (`Fixtures.kt`).
3. **`SharedNoteText`** (spec §3.3): `SharedNote` (sellado) y `SharedNoteText.compose`. Un test por regla (1-8) y los casos de borde de la regla 9. Comprueba
   además que todo `SharedNote.Text` que sale es aceptado por `NoteService.create` (sin `\r\n`, no vacío).
4. `docs/INFORME-NUBE-F6.md`: qué se hizo, tests por módulo (antes → después), decisiones que tomaste donde la spec no decía nada y qué quedó sin hacer.

## Límites (no los cruces)

- **Solo `android/domain`** (código y tests) y el informe. No modifiques `android/core`, `android/sync`, `android/text`, `android/syncclient`, `android/app`
  ni `tests/vectors/`. Si te falta algo de `text` (p. ej. algo es `internal`), anótalo en el informe y resuélvelo dentro de `domain`.
- Nada de Android, Glance ni Compose. Ninguna dependencia nueva.
- No escribas texto de interfaz: los rellenos («Nota nueva», «Nota protegida») entran como parámetro.
- Las cifras de la spec (50 tiras, 120 y 200 caracteres, 1.000.000) son constantes con nombre en el `companion`/`object`, no números sueltos.
- Comentarios en español con el porqué, con la densidad de `domain`. Sin BOM en los `.kt` (los del repo no lo llevan).

## Criterio de parada

Para y haz push de la rama en cuanto se cumpla **una** de estas condiciones:

- **Hecho:** los pasos 1-4 completos y `./gradlew :core:test :sync:test :text:test :domain:test :syncclient:test` verde, con los 605 de antes intactos.
- **Presupuesto:** llevas unos 4 $, o has intentado tres veces arreglar el mismo fallo sin conseguirlo. Deja comiteado lo que esté verde y escribe en el
  informe hasta dónde llegaste y qué falta.
- **Bloqueo:** algo de la spec es contradictorio o exige cruzar los límites (por ejemplo, que extraer `DockOrder` obligue a cambiar un test existente). No
  improvises un rodeo: anótalo en el informe con lo que has visto y para.

No abras un PR ni fusiones nada: solo push de la rama. La revisión y la fusión las hace el usuario en local.
