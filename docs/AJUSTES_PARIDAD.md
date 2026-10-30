# Ajustes: Windows y Android en sintonía

Son la misma app en dos plataformas, así que un ajuste que existe en las dos **se llama igual en la interfaz, vale lo mismo y arranca igual**. Esta tabla
recoge qué ajustes son comunes, cuáles son de una sola plataforma y las diferencias que se han dejado a propósito. Se revisa al añadir un ajuste.

Estado a 2026-10-30. Windows: `src/Aldune.Core/AppSettings.cs`. Android: `AppSettings` en `android/app/.../data/SettingsStore.kt`.

## Pestañas de Ajustes

Mismo orden y mismos nombres para lo común; lo propio de cada plataforma va en su sitio fijo (entre Colores y Sincronización).

| Pestaña | Windows | Android | Qué vive ahí (común) |
|---|---|---|---|
| General | General | General | Idioma, modo simplificado, avisar de versiones nuevas. Windows añade arranque con Windows y atajos globales; Android, la versión. |
| Aspecto | Appearance | Aspecto | Aspecto y sus colores, esquinas rectas, señal de sync, color de nota solo como franja, mismo color en todas las notas. Windows añade el tamaño del texto. |
| Notas | Notes | Notas | Casillas grandes, mover hechas al final, esconder hechas, papelera, sonidos. Windows: recordar posición y tamaño, recordatorio con tareas; Android: orden de la lista y teclado al abrir. |
| Colores | Colors | Colores | Tema de color, tono de las notas nuevas, regla de color y «Aplicar a las notas existentes». Windows añade los temas propios. |
| (propia) | Dock y pantallas | Avisos | Windows: borde, alineación, monitor, gestos. Android: captura rápida y recordatorios exactos. |
| Sincronización | Sync | Sincronización | Perfiles de sync y copia automática en carpeta. Android añade la copia local en .zip. |
| Ayuda / Acerca de | Help, About | (en General) | Solo Windows tiene páginas propias. |

El modo simplificado esconde Notas, Colores y Sincronización en las dos plataformas (Windows esconde además lo avanzado del dock).

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
`noteCreated`, `noteArchived`, `noteOpened`, `noteClosed`, `taskChecked`, `noteDeleted`, `typing`, `textDeleted`) y mismos números de opción (7-12 = papel y pluma; 12 = goma).
Los WAV salen de `scripts/sounds/generar-sonidos.py`: sin brillo áspero, sin clics y nivelados por sonoridad; escribir y borrar texto van unos 8 dB por debajo.

**Sonidos sugeridos.** Al encender el interruptor por primera vez, si no hay nada elegido para los eventos de las notas, se asignan los cuatro que se
oyen de vez en cuando (abrir = pasar hoja, cerrar = papel que se pliega, marcar = toque de pluma, borrar nota = papel que se arruga). Escribir y borrar
texto suenan en cada pulsación y se activan a mano. El botón «Usar los sonidos sugeridos» los vuelve a aplicar cuando se quiera.

- **Android no ofrece «Recordatorio»**: los recordatorios ya suenan por su notificación del sistema. «Sync hecha» suena solo si el usuario pidió la
  sincronización (a mano o tirando de la lista), como en Windows; «sync fallida» suena siempre. «Nota nueva» suena al guardarse por primera vez.
- **Android solo tiene los cinco sonidos propios**: los del sistema de Windows (1-5) y el `.wav` propio (6) no existen allí y se quedan en silencio.
- Android no suena con el teléfono en silencio o vibración, ni con «No molestar» si no deja pasar los sonidos del sistema; «Probar» lo dice con un aviso en vez de quedarse mudo. Windows no tiene ese modo.
- El volumen de Windows no afecta a los sonidos del sistema ni a un `.wav` propio (los reproduce `SoundPlayer`); el de Android afecta a todos.

## Copia automática en una carpeta (fase 4)

Mismos nombres, mismo formato de manifiesto (`.aldune-export.json`), mismas reglas en las dos apps (`AutoExportPlanner` en Core y su port en
`android/domain`, con los mismos casos de test): un `.md` por nota en `notas/` (y `archivadas/` con la casilla), nombre fijo desde la primera vez,
protegidas y papelera fuera, lo que sale se aparta en `_eliminadas/` y **nunca se borra nada**; un fichero cambiado a mano o ajeno no se pisa (la copia
va al lado como `nombre (Aldune).md`). Los ficheros están en claro: el ajuste lo dice.

- **Cuándo escribe:** Windows, unos segundos después de que se calmen los cambios y al cerrar; Android, 15 s después del último cambio (WorkManager, cada
  cambio reinicia la espera) y una vez al día como red de seguridad.
- **Carpeta:** Windows, un selector de carpeta; Android, el selector del sistema con permiso persistente. Si en Android se pierde el permiso o la carpeta, la
  copia se apaga y se avisa en la lista y en Ajustes (no hay reintentos); en Windows un fallo se apunta y se reintenta, y Ajustes lo enseña.
- La carpeta es de cada equipo: no viaja en exportar/importar configuración.
