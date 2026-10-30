# Aldune para Android — F7: recordatorios, notas protegidas y conflictos

> 2026-10-18. Cierra §2.3, §2.4 y §2.9 de la spec madre (`2026-10-17-aldune-android-design.md`). Las decisiones marcadas **(usuario)** las tomó el usuario
> al preparar F7; el resto son defaults razonados. Partes: §3 (JVM puro, con TDD) y §4 (Android, se hace aquí con sondas).

## 1. Qué entra en F7

1. **Recordatorios**: uno por nota, puntual, local al dispositivo; alarma, notificación con Hecho / Posponer / Abrir, campana con la hora en la lista.
2. **Notas protegidas**: abrir, editar, proteger y quitar la protección con contraseña (hasta F6 solo se veían con candado y en solo lectura).
3. **Conflictos**: ver la otra versión y quedarse con una u otra; los que se quedaron sin nota, en una lista aparte.

Fuera de F7: biometría, combinar conflictos (se queda en Windows), recordatorios que viajan al PC (formato 4 no los lleva), bloqueo de la app entera,
recordatorios repetidos.

## 2. Decisiones de producto

| Tema | Decisión |
|---|---|
| Re-bloqueo de una protegida **(usuario)** | Al salir de la nota y al pasar la app a segundo plano. Cada nota se desbloquea por separado; **no se recuerda ninguna contraseña**. Sin bloqueo por inactividad. |
| Al saltar un recordatorio **(usuario)** | Se quita al avisar, como en Windows. La notificación queda hasta descartarla. Hecho la cierra; Posponer crea uno nuevo. Si el móvil estaba apagado o la app muerta, avisa en cuanto arranca. |
| Aviso en la lista **(usuario)** | Campana y hora en la fila («hoy 20:00», «mañana 9:00» o fecha), junto al candado y al icono de conflicto. |
| Versión protegida en un conflicto **(usuario)** | Se muestra «Versión protegida» con botón Desbloquear (la contraseña de esa versión puede ser otra). Los dos botones del conflicto funcionan **sin** desbloquearla. |
| Otra versión del conflicto **(usuario)** | Texto entero de la otra versión con las líneas distintas resaltadas (comparación por posición, como `ConflictDiff` de Core). |
| Edición frente a borrado **(usuario)** | Si ganó la edición y en otro equipo se había borrado: franja «Se borró en otro equipo», con «Quedarme con esta» y «Borrarla» (el borrado viaja firmado). Si ganó el borrado y quedó sin nota: lista «N conflictos» con «Recuperar la nota» y «Descartar». |
| Hora exacta | Por defecto alarma no exacta. Ajuste «Hora exacta» que lleva a conceder `SCHEDULE_EXACT_ALARM` (spec madre §2.9). No `USE_EXACT_ALARM`. |
| Contraseña | Mínimo 4 caracteres, dos veces al proteger (`PasswordRules` de Core); al desbloquear solo se rechaza la vacía. |
| Notas vinculadas | Una nota vinculada a archivo no existe en Android; no hay nada que decidir. |
| Texto en notificación | Una protegida **nunca** lleva su texto: título «Nota protegida» y ninguna tarea. |

## 3. Parte JVM pura (JVM puro, con TDD)

Reparto por módulos ya existentes; **sin dependencias nuevas**, sin Android. Comentarios en español con el porqué. Textos de interfaz: nunca; los rellenos y las
etiquetas devuelven tipos, no cadenas.

### 3.1 `domain`: recordatorios

- **`ReminderPresets`** (port de `ReminderPresets.cs`): `inOneHour(now)`, `tonight(now)` (hoy 20:00; si ya son las 20:00 o más, mañana 20:00), `tomorrowMorning(now)`
  (siguiente día a las 9:00). Reciben `ZonedDateTime` y trabajan en su zona (el cambio de hora de verano no desplaza «9:00»). Tests en los dos lados del límite
  de las 20:00 y en el día del cambio de hora.
- **`Reminder`** (`noteId`, `dueAt: Instant`) y **`ReminderStore`** (interfaz: `get`, `set`, `clear`, `all`) con `InMemoryReminderStore` para tests.
- **`ReminderRules`** (sin reloj interno: `now` entra como parámetro):
  1. `toSchedule(reminders, notes, now)`: los que tienen nota **activa** y `dueAt > now`.
  2. `overdue(reminders, notes, now)`: nota activa y `dueAt <= now` (los de un arranque tras estar apagado), por `dueAt` ascendente.
  3. `orphans(reminders, notes)`: recordatorios cuya nota no existe o ya no está activa (archivada, papelera, borrada por sync). Se limpian al arrancar y tras una sync
     (Windows los excluye al avisar, no los borra; en Android se borran porque no hay otra forma de verlos).
  4. `snooze(kind, now)` → `dueAt`: `OneHour` = +1 h; `TomorrowMorning` = `tomorrowMorning(now)`.
- **`ReminderLabel`**: de `dueAt`, `now` y la zona devuelve un tipo sellado (`Today(time)`, `Tomorrow(time)`, `Date(date, time)`, `Overdue`) para la fila; la interfaz lo
  convierte en texto. Test en la medianoche y entre zonas.
- **`ReminderNotificationContent`**: de una nota devuelve un sellado `Open(noteId, title, pendingTasks: List<String>)` (como máximo **3**, constante con nombre,
  las `☐` sin marcar, recortadas a 120 caracteres) o `Locked(noteId)`. `Locked` **no tiene dónde poner texto** (garantía estructural, como `WidgetStrip.Locked`).
  Test de propiedad: con notas protegidas cuyo `text`, etiquetas y `protectedContentJson` llevan un marcador, ningún `toString()` del resultado lo contiene.
  Usa `NoteTitleHelper` y `TaskLists` de `text`; no reimplementes el título ni las tareas.

### 3.2 `core`: protegidas con la clave ya derivada

`ProtectedNotes.protect/reprotect/tryUnprotect` derivan la clave en cada llamada (600.000 iteraciones). Para editar sin esperar en cada autoguardado:

- **`ProtectedNotes.unlock(content, password)`** → `UnlockedProtectedNote?` (`null` si la contraseña no es), que guarda la clave derivada y la sal y expone `text`.
- **`UnlockedProtectedNote.reprotect(newText)`** → `ProtectedNoteContent` nuevo, **sin derivar otra vez**: misma sal, nonce nuevo (como `Reprotect` de Core).
  **`close()`** pone la clave a cero; usarla después lanza `IllegalStateException`.
- Los métodos actuales no cambian. Los vectores de `tests/vectors/` siguen pasando; añade un test de ida y vuelta: `protect` → `unlock` → `reprotect` → `tryUnprotect`
  con la contraseña original devuelve el texto nuevo.
- **`PasswordRules`** (port): `check(password, confirmation?)` → `None | Empty | TooShort | Mismatch`, `MIN_LENGTH = 4`.

### 3.3 `text`: diferencias de un conflicto

Port de `ConflictDiff.cs`:

- `summarize(winner, losing)` → `ConflictDiffSummary?` (`null` si es el mismo texto; `\r\n` y `\n` cuentan igual) y `clip(line, max)`.
- **`lineDiff(winner, losing)`** → lista de `DiffLine(number, text, differs)` **de la otra versión** (la perdedora), por posición, con la línea que falta contada como
  vacía. Es lo que pinta la franja. Los casos de borde: textos iguales, una versión vacía, una más larga que otra, `\r\n`.

### 3.4 `syncclient`: proteger y resolver

- **`ProtectionService(store: NoteStore, cipher…, clock)`** sobre `NoteStore` (el mismo de `NoteService`):
  - `protect(id, password, confirmation)`: valida con `PasswordRules`; cifra el texto con `ProtectedNotes.protect`, deja el texto normal **vacío**, `isProtected = true`,
    `protectedContentJson`, `updatedAt = clock.now()`, `syncAuthorDeviceId = null`. Falla si ya está protegida o no existe.
  - `unlock(id, password)` → `UnlockedProtectedNote` o error `WrongPassword`.
  - `save(id, unlocked, newText)`: `reprotect` + las mismas columnas que arriba; el texto normal sigue vacío. `StaleWrite` como en `NoteService.edit` si hay una versión más nueva.
  - `removeProtection(id, password)`: descifra, restaura el texto normal, `isProtected = false`, `protectedContentJson = null`, fecha nueva y autor `null`.
  - **Todas** conservan color, etiquetas, `dockPosition`, estado y `screenOrigin`. Un test por cada campo.
  - Una nota protegida **no** acepta `NoteService.edit` (ya devuelve `Protected`); no cambies eso.
- **`ConflictStore`** (interfaz): `all()`, `get(noteId)`, `delete(noteId)`. `InMemoryConflictStore` para tests. (Room lo implementa en `app`.)
- **`ConflictResolver(notes: NoteStore, conflicts: ConflictStore, reminders…, clock, deviceId)`**:
  - `kind(conflict)` → `EditVsEdit` (la nota viva existe y la perdedora tiene nota), `EditVsDelete` (existe la nota viva y la perdedora es un borrado: franja «Se borró en otro
    equipo») o `Orphan` (no hay nota viva: ganó un borrado).
  - `keepCurrent(noteId)`: descarta el conflicto sin tocar la nota.
  - `useOther(noteId)`: como `RestoreConflict` de Core. Con nota perdedora: la aplica como versión nueva (`updatedAt = clock.now()`, autor `null`, conserva sus
    etiquetas, color, estado y protección tal como vienen) y descarta el conflicto. Con borrado como perdedora: borra la nota viva **dejando la lápida local** (la sync la firma) y
    descarta el conflicto. Si el conflicto ya no existe, error `NotFound` y no escribe.
  - `orphans()`: los conflictos de tipo `Orphan`, más recientes primero, para la lista «N conflictos».
  - Un conflicto con la perdedora **protegida** se resuelve **sin desbloquear**: `useOther` copia el `protectedContentJson` tal cual y deja el texto normal vacío.
  - Si la nota restaurada o borrada tenía recordatorio, `useOther` lo quita cuando la nota deja de estar activa o de existir (usa `ReminderRules.orphans`).
  - Tests: los tres tipos × (`keepCurrent`, `useOther`); idempotencia (resolver dos veces); `useOther` de una perdedora con CRLF conserva `lineEnding`; protegida; escritura que
    no ocurre cuando falla.

### 3.5 Criterio de hecho de la parte JVM

Línea base **650** pruebas (core 82, sync 119, text 100, domain 168, syncclient 181): siguen todas, más las nuevas (≈80-110). Ningún cambio de comportamiento en lo que ya
existe.

## 4. Parte Android (aquí, con sondas en el Pixel y la tableta)

1. **Alarmas**: `ReminderScheduler` sobre `AlarmManager` (`setAndAllowWhileIdle` por defecto; `setExactAndAllowWhileIdle` si hay permiso y el ajuste está activo). Una alarma por
   recordatorio, `PendingIntent` con el id de la nota. Se reprograman al arrancar el dispositivo (el `BootReceiver` de F6), al cambiar la hora (`TIME_SET`) o la zona
   (`TIMEZONE_CHANGED`) y al abrir la app (`ReminderRules.toSchedule`).
2. **Aviso**: canal «Recordatorios» (importancia alta). `ReminderNotificationContent` → notificación con **Hecho**, **Posponer** (menú: +1 h / mañana 9:00) y **Abrir**
   (abre la nota; una protegida abre la lista con su fila señalada, como el widget). `POST_NOTIFICATIONS` se pide al poner el primer recordatorio, no al arrancar.
3. **Pantalla de la nota**: en el menú «⋯», «Recordatorio» abre una hoja con los tres atajos, «Elegir fecha y hora» (selectores de Material 3) y «Quitar». Texto fijo:
   «Este recordatorio solo avisa en este dispositivo».
4. **Lista**: campana + `ReminderLabel` en la fila.
5. **Protegidas**: diálogo de contraseña (Proteger: dos campos; Desbloquear: uno) con indicador mientras se deriva la clave (fuera del hilo de interfaz). El editor de una
   protegida desbloqueada guarda con `ProtectionService.save` y no deja ningún texto en `note.text`. Se cierra la sesión (`close()`) al salir de la nota y en `ON_STOP`. El menú
   «⋯» ofrece «Proteger» o «Quitar la protección» (pide la contraseña).
6. **Conflictos**: franja bajo el título de la nota con la otra versión en solo lectura (`lineDiff`) y los dos botones; la versión protegida muestra «Versión protegida» +
   Desbloquear. En la lista, aviso «N conflictos» solo para los `Orphan`, con sus dos botones por fila.
7. **Ajustes**: «Recordatorios» (hora exacta con el permiso; enlace a los ajustes del sistema cuando falta).
8. **Textos** en los cinco idiomas (`android/strings.tsv`, `python android/scripts/gen_strings.py`).

## 5. Verificación (puerta de F7)

- JVM: `./gradlew :core:test :sync:test :text:test :domain:test :syncclient:test` verde; `lintDebug` sin errores; `gen_strings.py --check`.
- **Recordatorios** (Pixel y tableta): aviso con la app cerrada y el móvil en reposo (`dumpsys deviceidle force-idle`), exacto y no exacto; reiniciar el móvil con un
  recordatorio puesto; cambiar la zona horaria; Hecho / Posponer / Abrir; recordatorio vencido con el móvil apagado; archivar la nota con recordatorio → no avisa.
- **Protegidas**: PBKDF2 **medido en la tableta** (anota los ms); una protegida creada en Android se abre en Windows y una de Windows se abre y se edita en Android, y la
  edición vuelve a Windows tras sync; contraseña equivocada; salir de la nota y segundo plano la vuelven a bloquear; widget, búsqueda y notificación nunca enseñan su texto.
- **Conflictos**: provocar los tres tipos con la sonda de sync (F5, `ProbePcSimulator`); resolver cada uno con ambos botones y comprobar el resultado en el simulador del PC;
  una perdedora protegida resuelta sin desbloquear.
- El editor se prueba con **Gboard y con un teclado de terceros**. En la tableta en modo PC no se usa Atrás.
- Pixel: pedir «No molestar» activado antes de tocar su pantalla.

## 6. Riesgos

- **Alarmas en Android 14+**: `SCHEDULE_EXACT_ALARM` viene denegado; sin él la alarma puede tardar minutos. La interfaz lo dice y el ajuste lo explica.
- **Fabricantes agresivos** (ahorro de batería): el Pixel y la tableta no, pero otros pueden matar la alarma. Se documenta en `docs/ANDROID_PROBES.md`, no se intenta resolver.
- **PBKDF2 en la tableta**: 600.000 iteraciones pueden pasar de 1 s. Por eso la sesión desbloqueada guarda la clave y `save` no deriva otra vez; si aun así el desbloqueo pasa de ~3 s,
  decidirlo con los números medidos (no se baja el número de iteraciones: es compatibilidad con Windows).
- **Fecha nueva en `useOther`**: igual que en Windows, para que la próxima sync publique la decisión; si el reloj del móvil va atrasado, la decisión puede perder contra otra versión.
- **Recordatorio huérfano tras sync**: una nota archivada o borrada en otro equipo deja su recordatorio; `ReminderRules.orphans` lo limpia en cada arranque y tras cada sync.
