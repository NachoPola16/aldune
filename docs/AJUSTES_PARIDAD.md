# Ajustes: Windows y Android en sintonía

Son la misma app en dos plataformas, así que un ajuste que existe en las dos **se llama igual en la interfaz, vale lo mismo y arranca igual**. Esta tabla
recoge qué ajustes son comunes, cuáles son de una sola plataforma y las diferencias que se han dejado a propósito. Se revisa al añadir un ajuste.

Estado a 2026-10-30. Windows: `src/Aldune.Core/AppSettings.cs`. Android: `AppSettings` en `android/app/.../data/SettingsStore.kt`.

## Comunes (mismo texto de interfaz)

| Qué | Windows | Android | Nota |
|---|---|---|---|
| Idioma | `Language` | `language` | Los mismos cinco idiomas. |
| Aspecto y colores del aspecto | `Appearance`, `AspectColors` | `appearance`, `aspectColors` | Mismos números y mismos huecos de color por aspecto. |
| Modo simplificado | `SimplifiedMode` | `simplifiedMode` | |
| Casillas grandes | `LargeCheckboxes` | `largeCheckboxes` | De fábrica: apagado en Windows, encendido solo en instalaciones nuevas de Android (pantalla táctil). |
| Mover hechas al final | `MoveCompletedTasksToEnd` | `reorderOnCheck` | |
| Esconder tareas hechas | `AutoHideCompletedTasks` + valor y unidad | `hideDoneTasks` + horas | Windows: minutos, horas, días o semanas con un número. Android: 1 hora, 1 día o 1 semana. |
| Días en la papelera | `TrashRetentionDays` | `trashDays` | Windows: número libre. Android: 7, 14, 30, 60 o 90. |
| Señal de sincronización | `ShowSyncSignal` | `showSyncSignal` | Mismo texto; de fábrica la decide el aspecto. |
| Esquinas rectas | `SquareCorners` | `squareCorners` | Idem. |
| **Color de la nota solo como franja** | `NoteColorAsStripe` | `fillNoteColor` (al revés, se guarda «fondo») | **Mismo texto y mismo sentido del interruptor** en la interfaz; bloqueado con el mismo aviso en los aspectos de terminal (Telecom, Bash, Fósforo). Desde 2026-10-30. |
| Mismo color en todas las notas | `UniformNoteColor` | `uniformNoteColor` | |
| Tema, tono y regla de color de las notas nuevas | `ActiveThemeId`, `NewNoteTone`, `ColorAssignment`, `FixedNoteColor` | `noteThemeId`, `darkNotes`, `colorRule`, `fixedNoteColor` | Android añade «Aplicar a las notas existentes», como Windows. |
| Orden de la lista | `NotesManagerOrder` | `listOrder` | |
| Recordatorio con tareas | `ReminderIncludesTasks` | `reminderIncludesTasks` | |
| Sincronizar en segundo plano | `SyncAutomatically` | `syncInBackground` | |
| Avisar de versiones nuevas | `CheckForUpdatesAutomatically` | `checkUpdates` | Una vez al día, desactivable, solo abre la página de la release. |
| Vista previa en dock o widget | `ShowNotePreview` | `widgetPreview` | |

## Solo en Windows

Atajos globales, dock (borde, alineación, monitor, seguir al ratón, ocultar a pantalla completa, mantener abierto, gestos del trackpad), tamaño del texto
(Android sigue el del sistema), recordar posición y tamaño de las notas, diseño de nota por defecto, varios perfiles de sync con su carpeta/servidor/WebDAV,
temas propios (`CustomThemes`), arranque con Windows.

## Solo en Android

Teclado al abrir una nota, forma del botón «+» (app y widgets), notificación y mosaico de captura rápida, lista rápida, alarmas exactas, aviso de falta de
copia de seguridad. Son cosas que no existen en un ordenador de sobremesa.

## Diferencias que se mantienen a propósito

- **Esconder tareas y papelera**: Windows admite cualquier valor porque se teclea; en el móvil se ofrecen opciones fijas para no pedir teclear números. Si
  un día se quiere igualar, el modelo guardado ya admite el valor libre en Android (`trashDays` entre 1 y 365; `hideDoneAfterHours` hasta un año).
- **Fósforo**: Windows no enseña ningún color de nota (monitor monocromo); Android conserva la franja del color de la nota.

## Sonidos (fase 3)

Mismo interruptor (`SoundsEnabled` / `soundsEnabled`), mismo volumen (`SoundVolume` 0-100), mismas claves de evento (`reminder`, `syncDone`, `syncFailed`,
`noteCreated`, `noteArchived`, `noteOpened`, `noteClosed`, `taskChecked`, `noteDeleted`, `typing`) y mismos números de opción (7-11 = papel y pluma).
Los WAV salen de `scripts/sounds/generar-sonidos.py`.

- **Android solo ofrece** abrir, cerrar, marcar, borrar, archivar y escribir. Recordatorio, sync hecha/fallida y nota creada no se ofrecen: los
  recordatorios ya suenan por su notificación y la sync no tiene sonido propio en el móvil.
- **Android solo tiene los cinco sonidos propios**: los del sistema de Windows (1-5) y el `.wav` propio (6) no existen allí y se quedan en silencio.
- Android no suena con el teléfono en silencio o vibración; Windows no tiene ese modo.
- El volumen de Windows no afecta a los sonidos del sistema ni a un `.wav` propio (los reproduce `SoundPlayer`); el de Android afecta a todos.
