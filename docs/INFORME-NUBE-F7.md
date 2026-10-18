# Informe de la nube — F7, parte JVM pura

Spec: `docs/superpowers/specs/2026-10-18-aldune-android-f7-design.md` §3 (nada de §4). Solo se tocaron `android/core`, `android/text`,
`android/domain` y `android/syncclient`; ni `sync`, ni `app`, ni `tests/vectors/`. Sin dependencias nuevas.

## Estado: hecho (pasos 1-6)

`./gradlew :core:test :sync:test :text:test :domain:test :syncclient:test` verde. Los 650 de antes siguen intactos.

| Módulo | Antes | Después | Nuevos |
|---|---|---|---|
| core | 82 | 96 | +14 |
| sync | 119 | 119 | 0 |
| text | 100 | 113 | +13 |
| domain | 168 | 204 | +36 |
| syncclient | 181 | 242 | +61 |
| **Total** | **650** | **774** | **+124** |

(Omitidos: 5 en `sync` y 1 en `syncclient`, los mismos que en la línea base.) `ProtectionServiceTest` tarda ~48 s porque cada
`protect`/`unlock`/`removeProtection` paga las 600.000 iteraciones de PBKDF2 reales; se dejó así a propósito (no se baja el número).

## Qué se hizo, por commit

1. **core**: `PasswordRules` (`check`, `MIN_LENGTH = 4`, `PasswordProblem`) y `ProtectedNotes.unlock` → `UnlockedProtectedNote`
   (`text`, `reprotect`, `close`). Ida y vuelta `protect → unlock → reprotect → tryUnprotect` con la contraseña original; tras `close()`
   `reprotect` y `text` lanzan `IllegalStateException`; un test mide que 100 `reprotect` no derivan la clave. Los métodos existentes de
   `ProtectedNotes` no cambian.
2. **text**: `ConflictDiff` (`summarize`, `clip`, `lineDiff`, `ConflictDiffSummary`, `DiffLine`).
3. **domain**: `ReminderPresets`, `Reminder`/`ReminderStore`/`InMemoryReminderStore`, `ReminderRules` (+ `SnoozeKind`), `ReminderLabel`,
   `ReminderNotificationContent` (con el test de propiedad de las protegidas: `Locked` no tiene campos de texto y ningún `toString()`
   lleva el marcador). Cambio mínimo en lo existente: `WidgetContentBuilder.truncate` pasa de `private` a `internal` para reutilizarlo
   (mismo comportamiento).
4. **syncclient**: `ProtectionService` (`protect`, `unlock`, `save`, `removeProtection`) con un test por campo conservado
   (color, etiquetas, posición, estado, origen de pantalla, id y fecha de creación), cada uno en las tres operaciones.
   `parseProtectedContent` de `SyncMapping.kt` pasa de `private` a `internal`.
5. **syncclient**: `ConflictStore`/`InMemoryConflictStore` y `ConflictResolver` (`kind`, `keepCurrent`, `useOther`, `orphans`): los tres tipos
   × (`keepCurrent`, `useOther`), idempotencia, protegida sin desbloquear, CRLF, y «escritura que no ocurre cuando falla».

## Decisiones donde la spec no decía nada (o decía algo ambiguo)

- **Errores de `ProtectionService`**: `NoteError` es sellada en `domain` y no admite casos nuevos desde otro módulo, así que hay un
  `ProtectionResult`/`ProtectionError` propio (`NotFound`, `AlreadyProtected`, `NotProtected`, `InvalidPassword(problem)`, `WrongPassword`,
  `UnreadableContent`, `StaleWrite`). El `ConflictResolver` sí usa `NoteResult`/`NoteError` (`NotFound`, `InvalidArgument`).
- **`ProtectionService(store, clock)`**: la spec escribe «cipher…»; no hace falta ninguno, `ProtectedNotes` es un objeto.
- **Contraseña de solo espacios** («    »): `PasswordRules` la deja pasar (port fiel de Core) pero `ProtectedNotes.protect` la rechaza;
  `protect` la devuelve como `InvalidPassword(Empty)` en vez de lanzar. Al desbloquear, la vacía o en blanco es `WrongPassword`.
- **Saltos de línea de una protegida**: `protect` cifra `note.wireText` (con sus CRLF, que es lo que ve Windows). `save` devuelve los
  saltos a los del texto con el que se abrió la sesión (`UnlockedProtectedNote.text`), de modo que editar una protegida de Windows en
  Android no reescribe sus líneas. Para eso `UnlockedProtectedNote.text` es **el texto al abrir** y `reprotect` no lo cambia.
  `unlock` devuelve ese texto sin normalizar: la interfaz pasa por `LineEnding.normalize`. `removeProtection` guarda el texto con LF y
  fija `lineEnding` con lo que detecta en el descifrado.
- **Derivar la clave no se hace con una copia vieja de la nota** (tarda): `protect` y `removeProtection` leen la nota, derivan, la
  vuelven a leer y escriben sobre lo último guardado. Si lo cifrado ya no coincide (texto o contenido protegido distinto) devuelven
  `StaleWrite` sin escribir; si solo cambió el color, etc., se respeta el cambio nuevo. Hay tests con un almacén que cambia la nota entre
  las dos lecturas.
- **`save` y `StaleWrite`**: igual que `NoteService.edit` (parámetro `editedAt`, registro en memoria, una escritura rechazada no queda
  anotada). Además, si la sal de la nota guardada no es la de la sesión (llegó por sync una versión con otra contraseña) no se escribe.
  *No* detecta la edición de otro equipo con la misma contraseña (misma sal): ahí gana la última, como en una nota normal, y la sync lo
  trata como conflicto.
- **`ReminderLabel.of(dueAt, now, zone)`**: `dueAt <= now` es `Overdue` (el mismo límite que `ReminderRules.overdue`); la hora se
  devuelve al minuto. `ReminderRules.snooze(kind, now: ZonedDateTime)` recibe la zona porque «mañana 9:00» es hora de pared.
  `overdue` desempata por `noteId` para un orden estable.
- **`ReminderNotificationContent`**: además de las tareas (3 × 120), el título se recorta a 120 (`MAX_TITLE_LENGTH`), con el mismo
  recorte y puntos suspensivos que el widget, sin partir pares sustitutos. Las cifras son constantes con nombre.
- **`lineDiff`** lista **las líneas de la otra versión** (su texto entero). Una línea que la ganadora no tiene cuenta como vacía; lo que le
  sobra a la ganadora no se lista como líneas vacías (sí lo cuenta `summarize`).
- **`ConflictStore`** va por id de nota (`String`, forma canónica), uno por nota; `InMemoryConflictStore.put` existe solo para los tests.
- **Qué es «nota viva»** para `kind`: que exista en el `NoteStore`, también si está archivada o en la papelera.
- **`useOther` y los recordatorios**: solo se limpia el de la nota resuelta (`ReminderRules.orphans` sobre ese recordatorio); los demás los
  limpia el arranque o la sync.
- **Un conflicto sin nota ni borrado como perdedora** (no debería existir): `useOther` devuelve `InvalidArgument` sin escribir y
  `keepCurrent` sí lo descarta.

## Hallazgo que afecta a `app`: la lápida de una huérfana recuperada

`NoteStore.put` «no toca las lápidas» y el planificador, si una nota y un borrado comparten id, **manda el borrado**
(`createLocalEnvelopes`: «Si una nota y un borrado comparten id, manda el borrado, como en Core»). Recuperar una huérfana solo con `put`
dejaría la nota viva en local pero la sync seguiría firmando el borrado: la decisión nunca se publicaría. Windows lo evita porque
`ApplySyncNote` quita la lápida. `NoteStore` no tiene cómo quitarla y no se podía ampliar sin romper `RoomNoteStore` (en `app`, fuera de
los límites), así que `ConflictResolver` recibe un último parámetro, `tombstones: TombstoneEraser` (`fun interface`), y lo llama al aplicar una
nota perdedora. **Pendiente en `app`**: pasarle el que borra la fila de `TombstoneEntity` (en los tests,
`TombstoneEraser(notes::removeTombstone)` sobre `InMemoryNoteStore`). `RoomLocalSyncStore.commit` ya hace lo equivalente en `ApplyNote`; lo
ideal es que `useOther` use la misma operación dentro de una transacción de Room junto con el borrado del conflicto.

## Qué queda sin hacer / a tener en cuenta en la parte Android

- Nada de la parte JVM (§3) queda sin hacer. §4 no se tocó.
- `app` tiene que implementar `ConflictStore` en Room (`all`, `get`, `delete`) y cablear `TombstoneEraser`, `ReminderStore` y
  `ProtectionService`.
- `ProtectionService` y `ConflictResolver` no son atómicos entre varias escrituras (el contrato `NoteStore` no tiene transacciones): el
  orden es nota → lápida → conflicto → recordatorio, de modo que un fallo a medias deja el conflicto en pie y se puede volver a resolver.
- No se midió PBKDF2 en tableta (no hay dispositivo); la sesión desbloqueada evita pagarlo en cada autoguardado, pero `protect`,
  `unlock` y `removeProtection` siguen derivando una vez: la interfaz debe llamarlos fuera del hilo principal.
- El entorno no tiene SDK de Android: `:app` no se compiló. Los cambios de visibilidad (`internal`) y los tipos nuevos no alteran
  nada de lo que `app` ya usa.
