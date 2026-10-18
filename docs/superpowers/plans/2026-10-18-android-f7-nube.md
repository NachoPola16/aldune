# Prompt para la sesión de nube — F7, parte JVM (recordatorios, protegidas, conflictos)

> Para pegar en Claude Code en la web (claude.ai/code), repo `NachoPola16/aldune`, rama nueva desde `main`. Modelo: **Sonnet, esfuerzo medio**.
> Tope de gasto **≈4 $** (créditos que caducan el 2026-11-05). Lo esperable son 2-3 $. Antes de lanzarlo, la spec de F7 y este fichero tienen que estar subidos a `main`.

---

Trabajas en el repositorio de Aldune (app de notas; cliente Android en Kotlin bajo `android/`). Tu tarea es la **parte de la fase F7 que es Kotlin/JVM puro**, con TDD,
en los módulos `android/core`, `android/text`, `android/domain` y `android/syncclient`, sin nada que dependa de Android ni dibuje nada.

## Lee primero (en este orden)

1. `AGENTS.md` (reglas del proyecto; los comentarios van **en español** y explican el porqué).
2. `docs/superpowers/specs/2026-10-18-aldune-android-f7-design.md`: **es tu especificación**. Haz §3 entero y nada de §4.
3. El código de referencia en C# (para copiar la regla, no el estilo): `src/Aldune.Core/ReminderPresets.cs`, `PasswordRules.cs`, `ConflictDiff.cs` y, en
   `NotesRepository.cs`, `Protect`/`TryUnlock`/`RemoveProtection`/`UpdateProtectedText`; en `SyncService.cs`, `RestoreConflict` y `DismissConflict`.
4. El código Kotlin: `android/core/.../ProtectedNotes.kt`; `android/domain/.../` (`Note.kt`, `NoteStore.kt`, `NoteService.kt`, `WidgetContent.kt` como modelo de «tipo sin dónde poner
   texto», `Clock.kt`) y sus tests/`Fixtures.kt`; `android/text/.../` (`NoteTitleHelper.kt`, `TaskLists.kt`); `android/syncclient/.../` (`LocalSyncStore.kt`, `SyncMapping.kt`) y
   `android/sync/.../SyncTypes.kt` (`SyncConflict`, `ConflictVersion`).

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
  `./gradlew :core:test :sync:test :text:test :domain:test :syncclient:test`. **No toques nada de `:app`.**
- Línea base **antes de cambiar nada**: apunta los tests de cada módulo (core 82, sync 119, text 100, domain 168, syncclient 181 = 650). Al acabar tienen que seguir todos, más los nuevos.
- La suite regenera `tests/vectors/kotlin-output.json`: **descártalo** (`git checkout -- tests/vectors/kotlin-output.json`) antes de cada commit.

## Qué hacer (en este orden; un commit por paso, con mensaje en español; TDD: el test primero y que falle antes de implementar)

1. **`PasswordRules`** y **`UnlockedProtectedNote`** en `core` (spec §3.2). El test de ida y vuelta con la contraseña original, y que tras `close()` usarla lanza.
2. **`ConflictDiff`** en `text` (spec §3.3), con `lineDiff`.
3. **Recordatorios** en `domain` (spec §3.1): `ReminderPresets`, `Reminder`/`ReminderStore`/`InMemoryReminderStore`, `ReminderRules`, `ReminderLabel`,
   `ReminderNotificationContent` (con el test de propiedad de las protegidas).
4. **`ProtectionService`** en `syncclient` (spec §3.4), con un test por campo conservado.
5. **`ConflictStore` y `ConflictResolver`** en `syncclient` (spec §3.4): los tres tipos × (`keepCurrent`, `useOther`), idempotencia, protegida sin desbloquear, CRLF.
6. `docs/INFORME-NUBE-F7.md`: qué se hizo, tests por módulo (antes → después), decisiones donde la spec no decía nada y qué quedó sin hacer.

## Límites (no los cruces)

- Solo `android/core`, `android/text`, `android/domain`, `android/syncclient` (código y tests) y el informe. **No** `android/sync`, `android/app` ni `tests/vectors/`. Si te falta algo
  de `sync` o de `app`, anótalo en el informe y resuélvelo dentro de tu módulo.
- Los métodos existentes de `ProtectedNotes` y el comportamiento de `NoteService` **no cambian**; los vectores de `tests/vectors/` siguen pasando.
- Nada de Android, Compose ni `AlarmManager`. Ninguna dependencia nueva. Ningún texto de interfaz (los tipos devuelven datos; los rellenos entran como parámetro).
- Las cifras (3 tareas, 120 caracteres, 600.000 iteraciones, 4 de contraseña) son constantes con nombre.
- Comentarios en español con el porqué, con la densidad del código que los rodea. Sin BOM en los `.kt`.

## Criterio de parada

Para y haz push de la rama en cuanto se cumpla **una** de estas condiciones:

- **Hecho:** los pasos 1-6 completos y `./gradlew :core:test :sync:test :text:test :domain:test :syncclient:test` verde, con los 650 de antes intactos.
- **Presupuesto:** llevas unos 4 $, o has intentado tres veces arreglar el mismo fallo sin conseguirlo. Deja comiteado lo que esté verde y escribe en el informe hasta dónde llegaste y qué falta.
- **Bloqueo:** algo de la spec es contradictorio o exige cruzar los límites. No improvises un rodeo: anótalo en el informe con lo que has visto y para.

No abras un PR ni fusiones nada: solo push de la rama. La revisión y la fusión las hace el usuario en local.
