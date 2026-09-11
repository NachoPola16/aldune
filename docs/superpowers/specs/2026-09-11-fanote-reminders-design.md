# Fanote — Recordatorios con notificación de Windows (design spec)

Añade un recordatorio puntual (fecha/hora concreta, no recurrente) por nota,
con aviso nativo de Windows al vencer y posponer sencillo. Ver
`docs/ROADMAP.md`, sección "Recordatorios con notificación de Windows" —
marcado ahí como el mayor hueco funcional frente a la competencia (Notezilla
lo tiene y es lo más alabado de esa app; Fanote hoy no tiene nada de esto).

## Contexto y motivación

Fanote no tiene ninguna forma de que una nota "avise" en un momento
concreto — hoy solo existe si el usuario recuerda mirarla. Notezilla, la
referencia explícita del roadmap, ofrece justo esto y es su función más
alabada. Es un subsistema nuevo (persistencia + disparo periódico + aviso
nativo del sistema operativo), no un ajuste sobre algo que ya exista, de ahí
el spec completo en vez de un cambio acotado.

Decisiones tomadas en el brainstorming de esta sesión, con su motivo, para
no volver a discutirlas desde cero:

- **Por nota entera, no por línea/tarea suelta dentro de una nota.** Encaja
  con la metáfora de post-it de Fanote (y con lo que hace la propia
  Notezilla citada) y no contradice la decisión ya tomada varias veces de
  mantener el cuerpo como `TextBox` plano sin campos estructurados (mismo
  argumento que descartó el título separado, el arrastre de líneas y el
  selector de color al crear nota). Un recordatorio por línea, además,
  sería frágil: el cuerpo no tiene identidad estable por línea (Alt+Arriba/
  Abajo reordena, se edita, se borra), así que "la línea 3" deja de ser la
  línea 3 en cuanto algo cambia por encima — un recordatorio que se
  desengancha en silencio de la tarea correcta es peor que no tener la
  función.
- **Puntual, no recurrente**, con posponer. Recurrente (diario/semanal) es
  una categoría de producto distinta (calendario/alarma) y una superficie de
  diseño bastante mayor (qué pasa con una serie archivada, cómo se edita una
  serie...) que no hace falta para el caso real de un post-it ("no olvides
  X").
- **Notificación vía `NotifyIcon.ShowBalloonTip`** (WinForms, ya en uso por
  `TrayIcon`), no un toast interactivo de verdad
  (`CommunityToolkit.WinUI.Notifications`). El toast interactivo con botones
  de acción incrustados exige registrar un acceso directo con AUMID en el
  menú Inicio apuntando a una ruta fija — si el portable se mueve de carpeta
  o corre desde otra ubicación (el caso de uso que el propio roadmap vende
  como diferenciador: "hay versión portable que no se instala, ningún
  competidor la ofrece"), ese acceso directo queda apuntando al sitio viejo
  y el toast deja de poder reabrir la nota correcta. `ShowBalloonTip` no
  toca nada fuera de la carpeta portable y en Windows 10/11 el propio
  sistema ya lo renderiza como notificación moderna en el Centro de
  actividades — solo pierde los botones de acción incrustados, y la nota
  abre en menos de un segundo al hacer clic, así que posponer desde ahí no
  es un paso pesado.
- **Un recordatorio vencido con la app cerrada o el PC dormido avisa igual
  al volver a abrir Fanote**, en vez de descartarse en silencio — coherente
  con "no te secuestro datos ni acciones".
- **Selector con atajos rápidos** ("En 1 hora", "Esta noche", "Mañana
  9:00") **+ fecha/hora exacta** para el resto de casos, no solo un
  calendario crudo — cubre el uso más común de un post-it sin navegar un
  calendario cada vez.

## Alcance

**Dentro:**

- Tabla nueva `NoteReminder` (`Fanote.Core.NotesDatabase`), con
  `NotesRepository.SetReminder`/`ClearReminder`/`GetReminder`/
  `GetDueReminders`/`GetPendingReminders`.
- `Delete`/`PurgeExpiredTrash` de `NotesRepository` amplíados para limpiar
  también `NoteReminder` de la nota que se borra — mismo cuidado que ya
  tienen con `NotePlacement`.
- `Fanote.Windowing.ReminderScheduler` (nuevo): sondeo periódico mientras
  Fanote corre + una pasada de catch-up al arrancar, dispara el aviso vía
  `NotifyIcon`.
- Entrada "Recordatorio" en el menú "⋯" de `NoteWindow`: poner, ver, quitar
  un recordatorio para la nota abierta. Mismo panel sirve para posponer
  (abrir la nota desde el aviso y volver a poner uno nuevo).
- Indicador visual en la pestaña del dock (`EdgeDockWindow`) para notas con
  recordatorio pendiente.
- Localización de los textos nuevos en `Fanote.Resources.Strings` (ES/EN).

**Fuera de alcance (no en esta ronda):**

- Recordatorios recurrentes.
- Recordatorios por línea/tarea suelta.
- Toast interactivo con botones de acción incrustados (`CommunityToolkit.
  WinUI.Notifications`) — descartado explícitamente por la tensión con
  "portable, sin huella"; ver más arriba.
- Cualquier disparo cuando Fanote no está corriendo (Task Scheduler,
  `ScheduledToastNotification`...) — aceptado a propósito: el dock tampoco
  existe si Fanote no corre, así que no es una limitación nueva frente al
  resto de la app.
- Sonido/vibración distintos del que ya trae la notificación nativa de
  Windows por defecto.

## Componentes y cambios

### `Fanote.Core.NotesDatabase` — tabla nueva

```sql
CREATE TABLE IF NOT EXISTS NoteReminder (
    NoteId TEXT PRIMARY KEY,
    DueAt TEXT NOT NULL   -- ISO-8601 UTC, misma convención que CreatedAt/UpdatedAt
);
```

`PRIMARY KEY NoteId`, no una clave compuesta ni un `Id` propio: como mucho
un recordatorio activo por nota (coherente con "puntual, no recurrente" —
poner uno nuevo reemplaza cualquiera anterior en vez de acumularlos). Mismo
patrón que `NotePlacement`, con `CREATE TABLE IF NOT EXISTS` y sin
migraciones — una base de datos ya existente del usuario simplemente gana la
tabla la primera vez que arranca con esta versión.

### `Fanote.Core.NotesRepository` — métodos nuevos

- `SetReminder(Guid noteId, DateTimeOffset dueAt)`: `INSERT OR REPLACE`,
  mismo patrón que `SavePlacement`/`MoveNote`.
- `ClearReminder(Guid noteId)`: `DELETE ... WHERE NoteId = $noteId`, mismo
  patrón que `DeletePlacement`. Se llama tanto al cancelar a mano como
  desde `ReminderScheduler` justo después de avisar (puntual: una vez
  disparado, deja de estar pendiente).
- `GetReminder(Guid noteId) -> DateTimeOffset?`: para pintar el estado
  actual en el menú "⋯" de la nota abierta.
- `GetDueReminders(DateTimeOffset now) -> IReadOnlyList<(Guid NoteId,
  DateTimeOffset DueAt)>`: `WHERE DueAt <= $now`, usado tanto por el sondeo
  periódico como por el catch-up de arranque (misma consulta, distinto
  disparador).
- `GetPendingReminders() -> IReadOnlyDictionary<Guid, DateTimeOffset>`:
  todas las filas de `NoteReminder`, para pintar el indicador en el dock sin
  una consulta por nota — se llama junto a `GetByState(Active)` cuando el
  dock recarga.
- `Delete(Guid id)` y `PurgeExpiredTrash(TimeSpan retention)`: se les añade
  `DELETE FROM NoteReminder WHERE NoteId = ...` igual que ya hacen con
  `NotePlacement`, para que una nota borrada no deje un recordatorio
  huérfano que `GetDueReminders` intentaría resolver contra una nota que ya
  no existe.

### `Fanote.Windowing.ReminderScheduler` (nuevo)

Instanciado una vez desde `App.xaml.cs`, junto al resto de servicios de
nivel de aplicación (mismo sitio que ya arma `AppCoordinator`/`TrayIcon`).
Recibe `NotesRepository`, `AppCoordinator` (para abrir notas/el gestor) y el
`NotifyIcon` que ya crea `TrayIcon` (se le pasa la instancia existente, no
se crea uno nuevo — un segundo icono de bandeja sería confuso).

- Un `DispatcherTimer` con intervalo de 30s hace `GetDueReminders(now)` en
  cada tick. Mismo orden de magnitud que otros temporizadores del proyecto
  (autoguardado 500ms, sondeo de hover 50ms, purga de tareas al
  autoguardar) — 30s es de sobra para un recordatorio pensado en minutos u
  horas, no en segundos.
- Además, una pasada inmediata al construirse (llamada desde
  `App.OnStartup` después de `PurgeExpiredTrash`), para el catch-up de
  vencidos con la app cerrada — misma consulta, sin esperar al primer tick.
- Con **un** vencido: `ClearReminder`, luego `NotifyIcon.ShowBalloonTip`
  con el título de la nota (`NoteTitleHelper.GetTitle`) como texto del
  globo. Se guarda el `NoteId` asociado a ese aviso concreto; el evento
  `BalloonTipClicked` del `NotifyIcon` lo usa para abrir esa nota via
  `AppCoordinator.OpenOrActivateNote`.
- Con **varios** vencidos a la vez (típico solo del catch-up de arranque
  con la app cerrada un tiempo): se limpian todos de golpe, y el globo dice
  "N recordatorios pendientes" en vez de listarlos; el clic abre
  `NotesManagerWindow` en lugar de N ventanas de nota de golpe — evita el
  spam de ventanas cuando son varias.
- `NotifyIcon` solo mantiene un balloon tip a la vez de forma nativa: si un
  segundo grupo de vencidos aparece mientras el globo anterior sigue
  visible, `ShowBalloonTip` simplemente lo reemplaza — aceptable, es el
  mismo comportamiento que ya tiene cualquier notificación de Windows
  encolada.

### `NoteWindow` — poner/ver/quitar recordatorio

Nueva entrada "Recordatorio" en `ActionsPopup` (el menú "⋯"), junto a
"Exportar a Markdown" — un icono de reloj antes del texto, mismo estilo que
el resto de entradas del menú.

- **Sin recordatorio puesto**: al pulsar, se abre un panel (mismo estilo
  visual que `ActionsPopup`, ver `NoteMenuItemStyle`) con: tres botones de
  atajo y, debajo, un `Calendar` + campo de hora para el resto de casos. Un
  botón "Guardar" llama a `NotesRepository.SetReminder`. Los tres atajos se
  calculan contra `DateTimeOffset.Now` (hora local, no UTC — se convierte a
  UTC solo al guardar, igual que el resto de timestamps de la app):
  - **"En 1 hora"**: `Now + 1h`, sin ambigüedad.
  - **"Esta noche"**: hoy a las 20:00 si `Now` es antes de las 20:00; si ya
    son las 20:00 o más tarde, pasa a ser mañana a las 20:00 (evita
    proponer una hora que ya pasó hoy).
  - **"Mañana 9:00"**: el día siguiente a las 9:00, siempre — no depende de
    la hora actual.
  `Fanote.Core.ReminderPresets` (nuevo, puro, TDD) calcula las tres a partir
  de un `DateTimeOffset "now"` explícito, para poder testear "esta noche"
  en los dos lados del límite de las 20:00 sin depender del reloj real.
- **Con recordatorio puesto**: la entrada del menú muestra la fecha/hora en
  vez de solo "Recordatorio" (p. ej. "Recordatorio: mañana 9:00"). Al
  pulsar se abre el mismo panel, con un botón adicional "Quitar" que llama
  a `ClearReminder`.
- **Posponer** reutiliza este mismo flujo sin UI aparte: al hacer clic en
  el globo de aviso se abre la nota (el recordatorio que acaba de sonar ya
  se limpió solo), y "⋯ → Recordatorio" está ahí mismo para poner uno
  nuevo. No se construye un panel de "posponer" independiente — sería
  duplicar exactamente el mismo panel con otro título.

### `EdgeDockWindow` — indicador en la pestaña

`SetNotes`/`Refresh` (donde ya se calcula `NoteTitleHelper.GetTabPreview`
para cada pestaña) consulta `GetPendingReminders()` una vez por refresco
(no por pestaña) y pasa si esa nota tiene una entrada. Cada pestaña pinta un
glifo de reloj pequeño (a elegir de Segoe Fluent Icons, sin verificación
visual todavía — mismo aviso que ya se dejó anotado para otros iconos de
esta sesión) junto al título cuando corresponde.

## Manejo de errores

- **`DueAt` en el pasado al guardarlo** (el usuario deja el panel abierto
  hasta que la hora elegida ya pasó, o el reloj del sistema cambia):
  `SetReminder` no valida nada especial — se guarda igual, y el próximo
  tick de `ReminderScheduler` (máximo 30s después) lo trata como vencido y
  avisa. Más simple que rechazarlo, y el resultado (avisa casi
  inmediatamente) es razonable.
- **La nota se borra/purga con un recordatorio pendiente**: cubierto por
  la limpieza añadida a `Delete`/`PurgeExpiredTrash` (ver arriba) — sin
  este cuidado, `GetDueReminders` devolvería un `NoteId` que
  `AppCoordinator.OpenOrActivateNote` no podría resolver.
- **El reloj del sistema retrocede** (cambio manual, DST mal aplicado):
  mismo caso que "`DueAt` en el pasado", sin manejo especial — el
  recordatorio simplemente espera hasta que `now >= DueAt` vuelva a ser
  cierto.
- **`ShowBalloonTip` mientras Windows tiene las notificaciones silenciadas**
  (modo concentración/pantalla completa): comportamiento nativo del
  sistema, Fanote no lo detecta ni lo gestiona — coherente con no
  interferir con las decisiones del usuario sobre su propio sistema.

## Testing

Core, TDD como el resto del proyecto:

- `NotesRepositoryReminderTests` (nuevo, mismo patrón que
  `NotesRepositoryTaskCompletionTests`): `SetReminder`/`GetReminder`
  (round-trip), `SetReminder` dos veces reemplaza en vez de acumular,
  `ClearReminder`, `GetDueReminders` distingue vencidos de futuros y no
  devuelve lo ya limpiado, `Delete`/`PurgeExpiredTrash` dejan
  `NoteReminder` sin la fila de la nota borrada.
- `ReminderPresetsTests` (nuevo): "En 1 hora" siempre `+1h`; "Esta noche"
  antes de las 20:00 da hoy 20:00, a las 20:00 en punto y después da mañana
  20:00 (el límite exacto); "Mañana 9:00" da el día siguiente a las 9:00
  sin importar la hora de `now`.

`ReminderScheduler` y los cambios de `NoteWindow`/`EdgeDockWindow` son capa
WPF (temporizador, `NotifyIcon`, UI) — sin test automatizado, mismo patrón
que el resto de esa capa en el proyecto (autoguardado, sondeo de hover,
animaciones). Se verifica a mano: poner un recordatorio a 1-2 minutos,
confirmar que el aviso llega y que el clic abre la nota correcta; forzar el
caso de "app cerrada al vencer" cerrando Fanote y reabriendo después de la
hora puesta.
