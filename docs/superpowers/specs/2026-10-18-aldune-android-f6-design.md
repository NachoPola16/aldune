# Aldune para Android — F6: captura rápida y widget

> 2026-10-18. Desarrolla la fila F6 de la spec madre ([`2026-10-17-aldune-android-design.md`](2026-10-17-aldune-android-design.md) §2.7, §2.8 y §5) y manda sobre
> ella donde la concreta. Las decisiones marcadas **(usuario)** las tomó el usuario en esta sesión; el resto son defaults razonados.
> Parte del trabajo (§3) lo hace una sesión de nube: su resultado está en [`INFORME-NUBE-F6.md`](../../historia/informes/INFORME-NUBE-F6.md).

## 1. Qué entra en F6

Seis puertas para escribir una nota sin pasar por la lista, y un widget para ver el mazo desde la pantalla de inicio:

| Puerta | Qué hace | Dónde vive |
|---|---|---|
| «+» de la lista | Ya existe (F4). | — |
| Acceso directo del icono | Pulsación larga en el icono → «Nota nueva». Shortcut **estático**. | `res/xml/shortcuts.xml` |
| Compartir hacia Aldune | `ACTION_SEND` de `text/plain`: nota nueva con el texto compartido, en el editor. | `MainActivity` |
| Mosaico de Ajustes rápidos | «Nota nueva» en el panel de Ajustes rápidos. | `NewNoteTileService` |
| Notificación persistente | Una notificación fija «Nota nueva»; **desactivada por defecto** **(usuario)**. | `CaptureNotification` |
| Widget | Tiras de las notas activas y un «+». | Glance (`DockWidget`) |

Fuera de F6: compartir imágenes o archivos, compartir varios textos (`SEND_MULTIPLE`) **(usuario: solo texto)**, accesos directos dinámicos o
fijados por nota, marcar tareas desde el widget, widget en la pantalla de bloqueo, el icono definitivo de la app (F8).

## 2. Decisiones de producto

### 2.1 Compartir hacia Aldune

- **Solo texto** **(usuario)**: el filtro es `ACTION_SEND` + `text/plain`. Un enlace llega como texto (Chrome manda la URL en `EXTRA_TEXT` y el
  título de la página en `EXTRA_SUBJECT`).
- **Con asunto, el asunto es el título** **(usuario)**: primera línea el asunto, segunda el texto. Si el texto ya empieza por el asunto, o son
  iguales, no se repite. Las reglas exactas son las de `SharedNoteText` (§3.3).
- **Abre el editor** **(usuario)**, no guarda en silencio: la nota nueva sale con el texto compartido, el cursor al final y el teclado fuera, para
  añadir algo. Como ya tiene texto, **la nota se crea al abrir** (la regla de §2.7 de la spec madre, «se crea al escribir el primer carácter», es
  para la nota vacía; aquí el primer texto lo pone quien comparte).
- Si lo compartido queda vacío (solo espacios) no se crea nada: aviso breve «No hay texto que guardar» y se cierra. Si es desmesurado (§3.3), aviso
  «El texto es demasiado largo para una nota» y no se crea.
- Las casillas de Markdown (`- [ ]`, `- [x]`) se convierten en `☐`/`☒`, como al pegar en el editor (`TaskLists.convertMarkdownTasks`).
- Aldune se abre **dentro de la tarea de quien comparte** (lo normal en Android): al terminar, Atrás vuelve a Chrome. La nota se guarda igual que
  cualquier otra (guardado continuo de F4).
- **Un mismo `Intent` crea una sola nota**: rotar la pantalla, volver desde Recientes (`FLAG_ACTIVITY_LAUNCHED_FROM_HISTORY`) o recrear la actividad
  tras matar el proceso no puede crear otra. Se marca el intent como atendido en el estado guardado de la actividad.

### 2.2 Acceso directo, mosaico y notificación

Todos lanzan `MainActivity` con la acción `app.aldune.action.NEW_NOTE`: la misma pantalla que el «+» (editor de nota nueva, teclado fuera; si se sale
sin escribir no queda nada). En tableta (panel doble) se abre a la derecha con la lista visible.

- **Acceso directo**: estático, etiqueta corta «Nota nueva», icono propio (vector monocromo sobre el fondo del sistema).
- **Mosaico**: `TileService` con estado fijo (es un botón, no un interruptor). Al tocarlo: en API 34+ `startActivityAndCollapse(PendingIntent)` (la
  variante con `Intent` lanza excepción con `targetSdk` ≥ 34); antes, la de `Intent`. Con el móvil bloqueado, `unlockAndRun` primero. En API 33+,
  Ajustes → Captura ofrece «Añadir el mosaico» (`StatusBarManager.requestAddTileService`); antes, se explica cómo añadirlo a mano.
- **Notificación persistente** **(usuario: desactivada por defecto)**:
  - Interruptor en Ajustes → Captura: «Notificación para crear notas». Al activarlo en Android 13+ se pide `POST_NOTIFICATIONS`; si se deniega, el
    interruptor vuelve a apagado con una línea que lo explica y un botón a los ajustes del sistema.
  - Canal propio «Captura rápida», importancia baja (sin sonido ni vibración, sin globo en el icono). Notificación `ongoing`, silenciosa, sin hora,
    visible en la pantalla de bloqueo (no lleva nada de ninguna nota). Tocarla = nota nueva; además, una acción «Nota nueva».
  - Se vuelve a publicar al arrancar el móvil (`BOOT_COMPLETED`), al actualizar la app (`MY_PACKAGE_REPLACED`) y al abrir la app, si el ajuste está
    activo. Android 14+ deja descartar una notificación `ongoing`: si el usuario la descarta, se respeta hasta la próxima de esas tres ocasiones (no se
    repone al instante: sería pelearse con el usuario).
  - No es un servicio en primer plano: no hace falta, la notificación no tiene trabajo detrás.

### 2.3 Widget

- **Contenido** **(usuario)**: **todas las notas activas**, en el orden del mazo (el mismo de la lista), en una lista **desplazable** dentro del
  widget. Tope de seguridad de **50** tiras (un `RemoteViews` enorme lo rechaza el lanzador); si hay más, la última fila es «N más…» y abre la app.
- **Una tira**: el color de la cara de la nota (texto con el contraste de `NoteColorContrast`) y el título (`NoteTitleHelper`, una línea, cortado).
  **Por defecto solo títulos**; el ajuste «Mostrar más texto en el widget» (apagado) añade hasta dos líneas de vista previa, con el aviso «Se verá en la
  pantalla de inicio».
- **Protegidas** **(usuario)**: la tira sale en su sitio del mazo, con su color, un candado y «Nota protegida». **Nunca** lleva nada derivado de su
  texto ni de su contenido protegido, ni con el ajuste de vista previa activo. (Hoy una protegida que llega por sync tiene `text = ""`; la regla no se
  fía de eso: §3.2 lo impone por tipo.)
- **Tocar una tira** **(usuario)** abre esa nota en el editor (móvil: a pantalla completa; tableta: lista + nota). Una **protegida** abre la lista con
  su fila señalada (desplazada a la vista y resaltada un momento); abrirla es cosa de F7. Si la nota ya no existe o ya no está activa (el widget iba
  atrasado), se abre la lista con el aviso «Esa nota ya no está en el mazo».
- **«+»** en la cabecera: nota nueva (`NEW_NOTE`). La cabecera dice «Aldune»; tocarla abre la lista.
- **Vacío**: «No hay notas» y el «+».
- **Tamaño** **(usuario: uno redimensionable que se adapta)**: un único widget, 4×2 celdas por defecto, redimensionable en los dos sentidos
  (mínimo 2×1). Por ancho: menos de 300 dp, una columna de tiras; de 300 a 479 dp, cuadrícula de 2; desde 480 dp, de 3. Es `SizeMode.Responsive`
  de Glance con esos tres cortes. En la tableta el mismo widget estirado da la cuadrícula.
- **Aspecto**: fondo y cabecera con la paleta de la app (`AppPalette`) en claro u oscuro según el sistema; los colores de las notas no cambian.
  Vista previa en el selector de widgets (`previewLayout` en API 31+, imagen antes).
- **Cuándo se repinta**: tras cualquier escritura de `NoteService` (crear, editar, color, estado, orden, borrar) y tras una pasada de sync que
  cambió algo, **agrupado** (como mucho una vez por segundo: escribir en el editor no puede repintar el widget en cada tecla). También al cambiar el
  idioma, el ajuste de vista previa o el tema del sistema. Lo pinta el **proceso de la app**, que descifra con la clave del Keystore (spec madre
  §2.8: no hay instantánea en claro en disco).
- **Si no se puede leer** (la clave de datos no abre, base no disponible): el widget dice «Abre Aldune» y tocarlo abre la app. Tras reiniciar, hasta el
  primer desbloqueo la app no corre (no es *direct boot aware*): el lanzador enseña lo último que pintó, que es lo esperado.

## 3. Parte JVM pura (la hace la nube, con TDD)

En el módulo `android/domain` (paquete `app.aldune.domain`), que ya depende de `android/text`. Sin Android, sin Glance, sin texto de interfaz: los
textos de relleno entran como parámetro, igual que en `NoteListItem`.

### 3.1 `DockOrder`: el orden del mazo, en un sitio

`NoteService.dockOrdered` es privado y el widget necesita exactamente el mismo orden. Se extrae a `object DockOrder { fun sort(notes: Collection<Note>): List<Note> }`
con la misma regla (posición, `null` detrás; luego `createdAt`; luego `id`) y `NoteService` lo usa. Los tests actuales de orden siguen pasando sin
tocarlos; los nuevos de `DockOrder` fijan la regla por sí sola.

### 3.2 `WidgetContent`: qué notas entran y cómo

```kotlin
sealed interface WidgetStrip {
    val id: String
    val color: String
    data class Open(override val id: String, override val color: String, val title: String, val preview: String?) : WidgetStrip
    data class Locked(override val id: String, override val color: String) : WidgetStrip   // sin ningún campo de texto
}

data class WidgetContent(val strips: List<WidgetStrip>, val hiddenCount: Int)

data class WidgetOptions(val placeholder: String, val showPreview: Boolean = false, val limit: Int = 50)

object WidgetContentBuilder { fun build(notes: Collection<Note>, options: WidgetOptions): WidgetContent }
```

Reglas (cada una con su test):

1. Solo `NoteState.ACTIVE`: archivadas y papelera no salen.
2. Orden de `DockOrder.sort`.
3. `isProtected` → `Locked(id, color)`, **siempre**, con `showPreview` o sin él, y aunque su `text` no esté vacío (por ejemplo, una nota desbloqueada en
   memoria en F7). El tipo no tiene dónde poner texto: la garantía es estructural. Test de propiedad: con notas protegidas cuyo `text`, etiquetas y
   `protectedContentJson` contienen un marcador, ningún `toString()` del resultado contiene el marcador.
4. `Open.title` = `NoteTitleHelper.getTitle(text, placeholder)` cortado a **120** caracteres con «…» (sin partir un par sustituto).
5. `Open.preview` = `null` si `!showPreview` o si la vista previa sale vacía; si no, `NoteTitleHelper.getPreview(text)` cortado a **200** con «…».
6. Las etiquetas no salen nunca (el widget no las enseña y así no hay que pensar en ellas).
7. `limit` cuenta las tiras de todo tipo; `hiddenCount` = las activas que no caben. `limit` ≤ 0 se trata como 0 (todo oculto).
8. Sin notas activas: lista vacía y `hiddenCount = 0`.
9. Color: el de la nota tal cual (la derivación de borde y contraste la hace la app con `text`).

### 3.3 `SharedNoteText`: una nota desde un texto compartido

```kotlin
sealed interface SharedNote {
    data class Text(val value: String) : SharedNote
    data object Empty : SharedNote
    data object TooLong : SharedNote
}
object SharedNoteText { fun compose(text: String?, subject: String?): SharedNote }
```

Reglas (cada una con su test):

1. Saltos de línea: `\r\n` y `\r` sueltos pasan a `\n` (lo que sale va a `NoteService.create`, que exige LF).
2. Texto: se quitan las líneas en blanco del principio y del final, y los espacios del final; **no** la sangría de la primera línea (una lista
   sangrada compartida conserva su sangría). Espacio en blanco con la definición .NET del proyecto (`DotNetText`).
3. Asunto: todo espacio en blanco (saltos incluidos) se colapsa a un espacio y se recorta; un asunto no puede tener dos líneas.
4. Sin asunto → el texto. Sin texto → el asunto. Ninguno → `Empty`.
5. Si la primera línea del texto, recortada, **empieza por** el asunto (sin distinguir mayúsculas, comparación ordinal) → el texto sin cambios. Cubre
   «asunto = texto» y las apps que ya mandan «Título\nURL».
6. Si no → `asunto + "\n" + texto`.
7. Después, `TaskLists.convertMarkdownTasks` sobre el resultado.
8. Más de **1.000.000** de caracteres (UTF-16) tras componer → `TooLong`. (El servidor rechaza objetos de más de 5 MB; un millón de caracteres cifrados
   y en JSON queda por debajo con holgura incluso en el peor caso de UTF-8.)
9. Casos de borde con test: asunto que es la URL misma, texto con `#` al principio (se respeta: el título lo limpia al mostrarlo), asunto con saltos,
   texto solo de espacios con asunto válido (→ el asunto), `null` en cualquiera de los dos. Nunca se corta: o cabe entero o es `TooLong`.

### 3.4 Criterio de hecho de la parte JVM

`./gradlew :core:test :sync:test :text:test :domain:test :syncclient:test` verde, con las cifras de antes intactas y las nuevas sumadas; `NoteService`
sin cambio de comportamiento; ningún fichero fuera de `android/domain` salvo el informe. Ver el prompt de la nube para el detalle.

## 4. Parte Android (aquí, con sondas)

Orden de trabajo, cada paso probado antes del siguiente:

1. **Entrada única de captura en `MainActivity`**: `NEW_NOTE`, `ACTION_SEND` y «abrir nota `id`» (extra `note_id`) se traducen a un evento del
   `NotesViewModel` («nota nueva», «nota nueva con texto», «abrir nota», «señalar nota»). `onCreate` y `onNewIntent`; atendido una sola vez (§2.1).
   `launchMode` `singleTop` para que el widget y el mosaico no apilen copias de la app.
2. **Acceso directo** (`shortcuts.xml`) y **mosaico** (`NewNoteTileService`, permiso `BIND_QUICK_SETTINGS_TILE`).
3. **Ajustes → Captura**: «Mostrar más texto en el widget», «Notificación para crear notas», «Añadir el mosaico» (API 33+). En `SettingsStore`, con
   valores por defecto que funcionan si faltan.
4. **Notificación**: canal, permiso, `BootReceiver` (`RECEIVE_BOOT_COMPLETED`, que F7 reutilizará para los recordatorios), republicar al abrir.
5. **Widget Glance**: dependencia `androidx.glance:glance-appwidget` (versión estable vigente, comprobada al añadirla; licencia Apache, apta para F-Droid),
   `DockWidget` + `DockWidgetReceiver`, `appwidget-provider` con tamaños y vista previa, `WidgetUpdater` (agrupado a 1 s) llamado desde las escrituras de
   `NoteService` vía `AppContainer` y desde `SyncController` al terminar una pasada con cambios.
6. **Textos** nuevos en `android/strings.tsv` con los cinco idiomas y `python android/scripts/gen_strings.py` (+ `--check`): «Nota nueva» (ya existe),
   «No hay texto que guardar», «El texto es demasiado largo para una nota», «Captura rápida» (canal y sección), «Notificación para crear notas» y su
   explicación, «Mostrar más texto en el widget», «Se verá en la pantalla de inicio», «Añadir el mosaico», «Nota protegida» (ya existe), «No hay notas»,
   «N más…» (sin plural: «Más notas: N»), «Esa nota ya no está en el mazo», «Abre Aldune», la descripción del widget en el selector.
7. **Iconos**: vectores monocromos propios para el acceso directo, el mosaico y la notificación (la notificación exige icono pequeño monocromo; hoy la app
   usa `sym_def_app_icon`). El icono de la app sigue para F8.

## 5. Verificación (puerta de F6)

- JVM: §3.4.
- Dispositivo: una prueba instrumentada del widget con `glance-appwidget-testing` si funciona con la versión elegida (con una protegida y el ajuste de vista
  previa activo, el árbol pintado no contiene su texto); si no, la garantía queda en §3.2 y en la sonda.
- **Checklist en el Pixel 10a y en la tableta TB350FU**, con el móvil **reiniciado** antes de la segunda pasada (notificación y widget sobreviven).
  Antes de tocar la pantalla del Pixel, pedir al usuario que active «No molestar». En la tableta en modo PC, no usar Atrás.

| # | Prueba | Esperado |
|---|---|---|
| 1 | Acceso directo del icono | Editor de nota nueva, teclado fuera; salir sin escribir no deja nota |
| 2 | Compartir desde Chrome un enlace | Nota «título de la página / URL» en el editor; Atrás vuelve a Chrome |
| 3 | Compartir un texto que ya empieza por su asunto | Sin repetir el asunto |
| 4 | Compartir y rotar / volver desde Recientes | Una sola nota |
| 5 | Compartir `- [ ] a` | `☐ a` |
| 6 | Mosaico, con el móvil desbloqueado y bloqueado | Editor de nota nueva (bloqueado: pide desbloquear antes) |
| 7 | Notificación: activar (pide permiso), denegar el permiso | Activada: aparece; denegado: el interruptor vuelve a apagado con explicación |
| 8 | Notificación tras reiniciar | Vuelve a estar |
| 9 | Widget: añadirlo; notas activas en orden; archivada y papelera no salen | ok |
| 10 | Widget con una protegida, vista previa activa | Tira con candado, sin título ni texto |
| 11 | Editar una nota en la app / llegar un cambio por sync | El widget se actualiza en ≤ 2 s; escribir no lo repinta en cada tecla |
| 12 | Tocar una tira / una protegida / una ya borrada | Editor / lista con la fila señalada / lista con aviso |
| 13 | Estirar el widget (móvil) y ponerlo grande (tableta) | Una columna → cuadrícula de 2 o 3 |
| 14 | Más de 50 notas | 50 tiras y «Más notas: N» |
| 15 | Escribir en una nota capturada con Gboard y con un teclado de terceros (AnySoftKeyboard) | Enter y Retroceso como en F4 |
| 16 | Tema claro y oscuro del sistema; los cinco idiomas en el widget y en Ajustes → Captura | Legible, sin textos en inglés sueltos |

- Informe en `docs/historia/informes/INFORME-ANDROID-F6.md` y lo aprendido en `docs/ANDROID_PROBES.md`.

## 6. Riesgos

1. **Glance y los lanzadores**: el de Lenovo (modo PC incluido) puede no respetar los tamaños o la vista previa. Se prueba pronto (paso 5 con un widget
   mínimo antes de pulirlo).
2. **Repintado con la app cerrada**: Glance necesita arrancar el proceso para pintar; si abrir la base y descifrar tarda, el widget se ve vacío un momento.
   Se mide en la tableta.
3. **Compartir en la tarea ajena** puede dejar dos copias de `MainActivity` (una en la tarea de Aldune, otra en la de Chrome). Comparten el
   `AppContainer` del proceso, así que no hay dos bases ni dos syncs; si en la sonda aparecen ediciones cruzadas raras, se pasa a una actividad
   trampolín.
