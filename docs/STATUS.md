# Aldune — Estado del proyecto

> **Nota (2026-10-17):** los planes de implementación paso a paso de `docs/superpowers/plans/` (fases 1-3, 1.0, 1.5 y notas vinculadas; todo ya publicado) se eliminaron del árbol por residuales; las referencias de este documento a ellos siguen siendo válidas en el historial: `git log --diff-filter=D --name-only -- docs/superpowers/plans/`. Las specs de `specs/` se conservan.

Documento de continuidad: si retomas este proyecto en otra sesión, otro chat, u
otra IA, empieza por aquí. Todo lo importante vive en el repositorio (specs,
planes, commits), no solo en una conversación concreta.

## Qué es Aldune

App de notas para Windows (WPF/.NET 10), inspirada en Hold My Notes / Noty /
noty-sepia (macOS): notas ancladas al borde de la pantalla que se despliegan
en abanico al pasar el ratón por encima. Ver el diseño completo en
`docs/historia/specs/2026-08-30-aldune-v1-design.md` — ese documento es la
autoridad de diseño; todo lo demás (planes, código) se argumenta contra él.

## Qué hay hecho (fusionado en `main`)

- **Fase 1** (`docs/superpowers/plans/2026-08-30-aldune-phase1-window-mechanics.md`):
  mecánica de ventana — pill anclado al borde, hover con abanico, ventana de
  nota que activa el foco correctamente, animación respetando el ajuste de
  accesibilidad de Windows. Un solo monitor, notas falsas en memoria.
- **Fase 2** (`docs/superpowers/plans/2026-08-30-aldune-phase2-persistence.md`):
  persistencia real — SQLite, cifrado AES-GCM con clave envuelta en DPAPI
  (ligada a tu usuario de Windows), CRUD, papelera/archivado, autoguardado con
  debounce y guardado forzado al cerrar, manejo de errores de arranque (clave
  DPAPI inaccesible, base de datos corrupta, clave equivocada para una base
  existente — los tres casos que pedía la spec, cada uno con su propio
  mensaje y comportamiento).
- **Pulido visual post-Fase 2** (sin plan formal, ronda de ajustes directos):
  quitado el resaltado azul por defecto de Windows en las pestañas del dock
  (ahora oscurece el propio color de la nota al pasar el ratón), colores
  variados por nota nueva (paleta de 6 pasteles, rotando), y tratamiento de
  "título": la primera línea de cada nota se muestra en negrita en la pestaña
  del dock y como título de la ventana — sin campo de título nuevo, solo
  presentación (`NoteTitleHelper.GetTitle`, en `Aldune.Core`, puramente
  computado a partir del texto existente).
- **Arreglo de los dos bugs visuales pendientes + pulido visual moderno**
  (bounded, sin spec/plan formal — ver detalle en "Bugs visuales" e
  "Historial" más abajo): scroll en el panel desplegado, mitigación del
  glitch de despliegue, pill en reposo con margen/esquinas redondeadas/sombra
  nativa de Windows 11, ventana de nota teñida del color de la nota, pastillas
  de color por nota visibles en el pill en reposo, y selector para cambiar el
  color de una nota ya creada.
- **Botón "Archivadas" + `NotesManagerWindow`** (bounded — ver detalle en su
  sección más abajo): "Restaurar" en `NoteWindow` para notas no activas,
  purga automática de la papelera a los 30 días
  (`NotesRepository.PurgeExpiredTrash`), y una ventana aparte
  (`NotesManagerWindow`) para archivar/restaurar/borrar en bloque con
  filtro por estado. El toggle "Archivadas"/"Activas" que esta sesión
  añadió **al propio dock** fue retirado después por el rediseño de
  pestañas en abanico (ver más abajo) — `NotesManagerWindow` es ahora la
  única forma de ver notas archivadas/en papelera.
- **Rediseño de pestañas en abanico estilo Hold My Notes** (arquitectónico —
  ver detalle en su sección más abajo): panel desplegado del dock
  rehecho con pestañas escalonadas, etiqueta de texto rotada
  verticalmente por nota, y apertura de la nota creciendo desde la
  posición de su propia pestaña.
- **Fase 3a** (`docs/superpowers/plans/2026-09-02-aldune-phase3a-multimonitor-dpi.md`):
  primera sub-entrega de la Fase 3 — un `EdgeDockWindow` real por cada
  monitor conectado (antes solo el principal), con geometría y DPI reales
  de Win32 (`MonitorEnumerator`, `app.manifest` con `PerMonitorV2`), y un
  `AppCoordinator` a nivel de app para que las notas y la ventana de
  gestión no se dupliquen entre docks. Verificado contra los dos monitores
  reales del usuario (uno en vertical), incluido el checklist manual de
  interacción — encontró y arregló un bug real de refresco entre docks
  (ver historial más abajo) y, a raíz de probarlo, también se hizo que el
  tamaño del pill/panel se ajuste al número de notas en vez de ser fijo.

Tests: 506/506 pasando (`dotnet test tests/Aldune.Core.Tests --no-restore`).

- **Cambios de pantallas, disposición y sincronización**: al reconstruir los docks por un cambio de
  monitores se conserva la pantalla física de cada nota abierta y se restaura cuando vuelve a estar
  disponible; el rebuild se reintenta durante unos segundos porque Windows puede publicar el cambio
  antes de terminar de enumerar la pantalla. `Restaurar posiciones originales` abre todas las notas
  de la vista, recupera las posiciones guardadas y coloca las restantes cerca del borde del dock,
  no en el centro. Se retiró `Normal cascade`.
- **Actualizaciones desde Ajustes**: la sección Aplicación incluye `Buscar actualizaciones…`, que
  usa el mismo comprobador de releases que la bandeja y permite abrir la descarga cuando existe una
  versión nueva.
  Manage Notes usa texto adaptativo para que también sea legible sobre notas oscuras. Sync admite
  limitar un vínculo a una etiqueta y conserva ese filtro en perfiles compartidos.

- **Logo definitivo (2026-10-26)**: la «A» de tres notas sustituye a las tres pestañas con barra; un solo dibujo del que salen
  todos los formatos (`scripts/branding/generar-iconos.py`: `.ico`, ficha de tiendas, icono adaptativo y monocromo de Android,
  icono de estado, acceso directo, widget, favicon, README). Sale en la 1.7.14 de Windows y la rc10 de Android. Colores, reglas de
  uso y lo descartado (la alternativa C, notas apiladas, queda guardada): [`BRANDING.md`](BRANDING.md).
- **Android, widgets y burbuja (2026-10-26)**: el selector de widgets ofrece el mazo en 4×2, 2×2 y 4×4 y un botón 1×1 de nota nueva
  (los tres mazos comparten `DockWidget`). La burbuja la da el sistema desde Android 17 (pulsación larga en el icono): no hay nada
  que construir; decisión y lo que queda sin probar en `DECISIONES-2026-10-26.md`. El selector de color de la nota ya no pierde el
  color con el que se abrió (el negro de un aspecto retro) y avisa cuando «mismo color en todas las notas» lo oculta.
- **Sincronización**: [`SYNC.md`](SYNC.md) empieza con una comparativa de los tres tipos (carpeta compartida, servidor propio, WebDAV),
  qué admite cada plataforma, un paso a paso para cada uno y los errores más habituales. Android solo admite servidor propio y WebDAV.
- **Android, barra del editor y arrastrar tareas (2026-10-26, rama `android-varios-perfiles`)**: la barra sobre el teclado ya no se desplaza
  (deshacer, rehacer, casilla, viñeta, sangrías y «⋯»; subir y bajar la línea pasan al menú «⋯» por TalkBack). Las tareas llevan un asa de
  tres rayas a la derecha: arrastrarla mueve la tarea (`LineMovement.moveTo`, `TaskDrag` en `android/text`, gesto y dibujo en `TaskDragUi.kt`).
  Probado en el Pixel.
- **Casillas con tick, grandes y bienvenida (2026-10-26, 1.7.15 y rc11)**: lo guardado sigue siendo ☐ y ☒; cambia cómo se dibujan. Fuente propia
  AlduneCheck (`scripts/branding/generar-fuente-check.py`, caja de Segoe con ☒ = caja + tick; ☑ real se descartó por el cuadrado negro de Segoe),
  detrás de la fuente de la nota en Windows (el interlineado lo sigue marcando ella) y como estilo solo de las casillas en el editor de Android.
  Ajuste «Casillas de tarea más grandes» (+25 %, también la zona de clic en Windows; en `ConfigFields` como `largeCheckboxes`). Android: bienvenida
  de la primera vez, solo en instalaciones sin notas (`welcomeSeen`), y botón «Quitar hechas» visible sobre la barra cuando hay tareas marcadas.
  La sync al volver a primer plano ya existía (mínimo 30 s). Sin hacer, a propósito: «añadir artículo» sin abrir la nota (widget o notificación),
  QR y pegar código de perfil, transparencia de la sync en segundo plano.
- **Android, varios perfiles de sync (2026-10-26, rama `android-varios-perfiles`, sin publicar)**: el móvil puede unirse a más de un perfil
  a la vez (p. ej. «todas mis notas» + la nota compartida de la familia). Cada perfil tiene su id, su fichero de secretos, su estado, y
  `sync_base`/`sync_conflict` por (perfil, nota) (Room v3, con migración); notas y lápidas siguen siendo del dispositivo. Una pasada recorre
  los perfiles de uno en uno y el fallo de uno no frena a los demás; un perfil adicional nunca ofrece «descartar». El perfil único de las
  versiones anteriores migra solo (id `default`). Crear y compartir perfiles sigue siendo cosa de Windows. Pendiente de la ampliación de la
  sync: QR y pegar/compartir el código, transparencia de la sync en segundo plano, pasada de errores y conflictos. Spec y plan en
  `docs/superpowers/`. Probado en el emulador (migración desde un perfil, segundo perfil, duplicado, desconectar uno); sin probar en el Pixel.

- **Modo simplificado en Android y ajustes predeterminados (2026-10-27):** Ajustes tiene un botón «Usar modo simplificado» que deja a la vista solo sincronización, aspecto, idioma y la versión (la regla vive en `SettingsSections`, `android/domain`, con test; los ajustes escondidos no cambian). Las casillas de tarea grandes vienen activadas en instalaciones **nuevas** de Android (quien ya tenía la app conserva lo que tenía). En Windows se movió «Mostrar tareas pendientes en el recordatorio» de la página Dock a «Notas y tareas»; el resto de valores por defecto se revisó y se deja igual (ver `docs/DECISIONES-2026-10-27.md`). Temas propios sigue siendo solo de Windows.
- **Nota compartida con la familia (2026-10-27):** funciona con un perfil «Familia» de alcance por etiqueta (`familia`) y **su propio servicio de sync**
  (`aldune-sync-familia`, puerto 8100, `aldune-familia.polacabezon.com`): un perfil con otra clave no puede compartir almacén con el personal
  porque Aldune se niega a escribir si no puede leer ninguna nota (`SyncService`, «sync code can't read the notes in the store»). Ver `SYNC.md` apartado D
  y `servidor-config/docs/actualizar-contenedores.md`.
- **Versiones (2026-10-27):** Windows 1.7.17 y Android 0.1.0 (la primera estable, `versionCode` 12, sin `-rc`; se publica con `--latest=false`, ver `RELEASING.md`).
- **QR de emparejamiento** (2026-10-27, spec `superpowers/specs/2026-10-27-qr-emparejamiento-design.md`): Windows muestra el código de
  perfil como QR (`ShareQr` en Core con QRCoder 1.8.1; `ShareQrWindow` con aviso y cierre a los 120 s) y Android lo escanea
  (`QrScanActivity` con CameraX 1.6.2 + `QrDecoder` sobre ZXing core 3.5.4, sin Play Services). Se decidió no dividir el QR en varios
  y que el token no viaje nunca. Cero cambios en el formato de sync.

## Cómo se ha trabajado (para mantener el mismo estilo)

- **Diseño**: skill `superpowers:brainstorming` → spec escrita y comprometida
  en `docs/superpowers/specs/`.
- **Planes de implementación**: skill `superpowers:writing-plans`, un plan por
  fase en `docs/superpowers/plans/`, con tareas TDD detalladas (código
  completo, no placeholders).
- **Ejecución**: skill `superpowers:subagent-driven-development` — un
  subagente implementador por tarea + un subagente revisor independiente por
  tarea, con ronda de arreglo si el revisor encuentra algo. Cada fase termina
  con una revisión final de toda la rama (modelo más capaz) antes de fusionar.
- **Aislamiento**: cada fase/cambio se hace en un worktree separado
  (`EnterWorktree`), se fusiona a `master` con `git merge --no-ff` al
  terminar, tests verificados en el resultado fusionado antes de dar por
  cerrado.
- **Verificación manual**: los subagentes no siempre tienen forma de mover el
  ratón/hacer clic en la app real — cuando el comportamiento es
  interactivo (hover, foco de ventana, arrastrar), lo prueba el humano
  directamente lanzando `dotnet run --project src/Aldune` y siguiendo un
  checklist concreto.
- **Preferencia del usuario confirmada**: entender el porqué de las
  decisiones técnicas, no solo el qué — las explicaciones detalladas en la
  conversación (WPF, DPAPI, Win32) fueron bien recibidas y conviene mantener
  ese nivel de detalle al proponer cosas nuevas.

## Deuda técnica conocida y aplazada a propósito

Estas cosas están identificadas, no son sorpresas — decidir si se abordan
cuando toque la fase correspondiente:

- **Fase 1**: la spec pedía una comprobación de posición del ratón de baja
  frecuencia como red de seguridad para el colapso del abanico (por si
  `MouseLeave` no se dispara, ej. otra ventana tapa el dock) — nunca se
  implementó, solo el retardo simple. Funciona bien en la práctica pero es un
  hueco real frente a la spec.
- **`ContentCipher` nunca se libera** (`Dispose()`) durante la vida de la
  app — aplazado con una advertencia: un hook de cierre mal ordenado podría
  convertir el guardado-al-cerrar en una excepción y perder las últimas
  pulsaciones. Si se retoma, tiene que ir estrictamente después de que todas
  las ventanas de nota se hayan cerrado.
- Varias cosas menores de higiene (comentario explicando por qué
  `AuthenticationTagMismatchException` no se confunde con el caso DPAPI en
  `App.xaml.cs`, deduplicación de patrón `SqliteConnection.ClearPool()` en
  tests, etc.) — sin impacto funcional, solo mantenibilidad.
- **La tira de reposo del dock a veces desaparece del todo, sola, sin nada a
  pantalla completa** (reportado por el usuario, 2026-09-09; ya le había
  pasado antes de esta sesión, no es nuevo). Vuelve a verse en cuanto se pasa
  el ratón por encima. Hipótesis, sin confirmar todavía: es el riesgo que ya
  se dejó anotado sin verificar al pasar el dock a `AllowsTransparency="True"`
  (ver la sección "Rediseño de movimiento y forma del dock" más abajo) — una
  ventana WPF "layered" que deja de recomponerse tras algún evento del
  sistema (¿reanudar tras suspender? ¿reconectar un monitor? ¿aleatorio en
  uso continuado?) hasta que algo fuerza un repintado, que es justo lo que
  hace la animación de opacidad al pasar el ratón (`EdgeDockWindow.ApplyState`/
  `Fade`, tocar `Opacity` obliga a WPF a recomponer de verdad). **No se ha
  implementado ningún arreglo todavía** — hace falta primero confirmar el
  disparador real (preguntado al usuario, respuesta pendiente) antes de
  enganchar un repintado forzado al evento correcto en vez de un parche
  genérico. Si se confirma que es tras reanudar del sueño, el sitio natural
  es `SystemEvents.PowerModeChanged` en `App.xaml.cs`, mismo patrón que ya
  usa `OnDisplaySettingsChanged` para los cambios de monitor.

## Prerrequisitos para la Fase 3, resto por hacer (sub-entregas 2+)

Los puntos 1 y 2 originales (coordinador a nivel de app, geometría/DPI real
por Win32) ya están resueltos por la Fase 3a — ver el historial más abajo.
Queda:

1. **`ScreenOrigin` sigue fijo al literal `"primary"` en todas las notas.**
   Una sub-entrega futura tiene que decidir qué hacer con ese valor
   centinela frente a IDs de dispositivo reales y estables (Win32 hoy solo
   da `\\.\DISPLAY1`-style, que cambia al reconectar monitores — ver
   `MonitorInfo.DeviceName` en `Aldune.Core`, documentado ahí como "no es
   el id estable").
2. **Con varios docks, todos muestran la misma lista completa de notas**
   (se decidió así a propósito en la Fase 3a, mientras no haya IDs
   estables por monitor — ver historial). Cuando se resuelva el punto 1,
   cada dock podrá empezar a filtrar por su propio origen.
3. **Sin toggle de Ajustes** para restringir la app a un solo monitor — no
   existe ninguna UI de Ajustes todavía (`AppSettings` solo guarda la
   clave de cifrado). Necesaria si se quiere ese control además de "en
   todas las pantallas conectadas" (el único modo que hay ahora).
4. **Sin hotplug en caliente**: conectar/desconectar un monitor con la app
   ya corriendo no se maneja (`WM_DISPLAYCHANGE`) — hace falta reiniciar
   la app para que detecte el cambio. Tampoco se maneja un cambio de
   escala de DPI en caliente sin desconectar el monitor (WPF con
   `PerMonitorV2` reescala solo las ventanas existentes, pero el
   reposicionamiento fino del docking al borde no se ha probado para ese
   caso).

## Prerrequisitos para la Fase 5 (import/export)

- `GetByState` ordena por `CreatedAt` como texto ISO-8601 — solo funciona
  porque todos los timestamps se escriben en UTC (`"+00:00"`). Una
  importación que escriba timestamps con offset local desordenaría las notas
  en silencio.
- El export tiene que descifrar vía `ContentCipher`, nunca volcar los BLOBs
  cifrados tal cual.

## Decisiones de diseño de esta última sesión (algunas ya hechas, otras solo decididas)

- **Título de nota**: NO se añade un campo de título real — la primera línea
  del texto libre se trata como título solo para mostrar (negrita, en la
  pestaña del dock y en la barra de título de la ventana). Ya implementado.
- **Ver notas archivadas/en papelera — HECHO, pero no como se decidió
  aquí originalmente.** Esta sesión había descartado una ventana nueva de
  lista+detalle a favor de un botón "Archivadas" que reemplazaba
  temporalmente lo que se ve en el dock. Se implementó así primero, pero
  el rediseño de pestañas en abanico (ver más abajo) lo revirtió: ahora
  SÍ es una ventana aparte (`NotesManagerWindow`, ver su propia sección)
  la única forma de ver notas archivadas/en papelera — el dock solo
  muestra notas activas.
- **Pulido visual moderno — HECHO** (ver "Historial: pulido visual y bugs
  visuales" más abajo para el detalle técnico).
- **Modo "Papel vintage"** (textura de papel, inclinación por nota, tipografía
  manuscrita, sonidos): sigue tal cual estaba en la spec original, como
  paquete opcional aparte — sigue sin implementar, no confundir con el
  pulido visual moderno de arriba (ya cerrado).


## Historial de sesiones

El diario cronológico (qué se hizo en cada ronda, con el porqué y las trampas) se partió en dos
ficheros en `docs/historia/` el 2026-10-24; el contenido no cambió, solo de sitio:

- [`historia/STATUS-2026-09.md`](historia/STATUS-2026-09.md): de las rondas del 2026-09-02 a la 1.4.0
  (2026-09-27).
- [`historia/STATUS-2026-10.md`](historia/STATUS-2026-10.md): del cierre del 2026-09-27 a 1.7.10 y el
  arranque del cliente Android (2026-10-17).

Las especificaciones de fases ya publicadas y los informes Android cerrados están en
`historia/specs/` y `historia/informes/`. Las vigentes siguen en `superpowers/specs/`.
