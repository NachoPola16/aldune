# Aldune — Estado del proyecto

> **Nota (2026-10-17):** los planes de implementación paso a paso (fases 1-3, 1.0, 1.5 y notas vinculadas; todo ya publicado) se eliminaron del árbol por residuales; siguen en el historial de git. Las specs de `docs/specs/` se conservan.

Documento de continuidad: si retomas este proyecto después de un tiempo, empieza por
aquí. Todo lo importante vive en el repositorio (specs, commits y estos documentos).

## Qué es Aldune

App de notas para Windows (WPF/.NET 10), inspirada en Hold My Notes / Noty /
noty-sepia (macOS): notas ancladas al borde de la pantalla que se despliegan
en abanico al pasar el ratón por encima. Ver el diseño completo en
`docs/historia/specs/2026-08-30-aldune-v1-design.md` — ese documento es la
autoridad de diseño; todo lo demás (planes, código) se argumenta contra él.

## Qué hay hecho (fusionado en `main`)

- **Fase 1** (plan retirado del árbol):
  mecánica de ventana — pill anclado al borde, hover con abanico, ventana de
  nota que activa el foco correctamente, animación respetando el ajuste de
  accesibilidad de Windows. Un solo monitor, notas falsas en memoria.
- **Fase 2** (plan retirado del árbol):
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
  filtro por estado. El toggle "Archivadas"/"Activas" que se
  añadió **al propio dock** fue retirado después por el rediseño de
  pestañas en abanico (ver más abajo) — `NotesManagerWindow` es ahora la
  única forma de ver notas archivadas/en papelera.
- **Rediseño de pestañas en abanico estilo Hold My Notes** (arquitectónico —
  ver detalle en su sección más abajo): panel desplegado del dock
  rehecho con pestañas escalonadas, etiqueta de texto rotada
  verticalmente por nota, y apertura de la nota creciendo desde la
  posición de su propia pestaña.
- **Fase 3a** (plan retirado del árbol):
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
- **Accesibilidad de Windows (2026-10-29, en `main`, sin release):** auditoría del código y arreglos. (1) Foco de teclado visible: estilo `AlduneFocusVisual` (anillo de 2 px, color `AppPalette.FocusRing`, calculado de cada paleta con ≥ 3:1 sobre todos sus fondos, test en las 4 paletas y los retro con colores extremos; NO es clave de la paleta porque esa lista es el contrato compartido con Android) en todos los estilos de botón con plantilla propia y como valor por defecto de `Control`; en la nota, anillo del color de la tinta de esa cara. (2) Las pastillas de color son `ToggleButton` (Tab entra una vez, flechas, Espacio/Intro, nombre y estado para el lector). (3) El gestor de notas: **Intro abre la nota, Espacio la marca, flechas suben y bajan** (antes una fila tenía foco y no hacía nada) y atajo global opcional `Ctrl+Alt+M` (apagado de fábrica; tres campos nuevos en `AppSettings` y `ConfigFields`). **Límite conocido:** las pestañas del dock siguen siendo solo de ratón (`WS_EX_NOACTIVATE` a propósito, para no robar el foco al pasar el cursor); la ruta por teclado es el gestor. (4) Alto contraste de Windows: `AppPalette.HighContrast` con los colores de sistema (7:1, tests con cinco temas de Windows), en vivo al activarlo o cambiarlo; los aspectos retro se sustituyen por la piel lisa. (5) «Tamaño del texto» 100/115/130/150 % (`TextScale`, `AppSettings.TextScalePercent`, por defecto 100): la escala tipográfica pasó de `StaticResource` a `DynamicResource` y se añadieron `AlduneTextCaption` (12) y `AlduneTextLarge` (14); las ventanas de tamaño fijo crecen con él (`ThemeManager.ScaleWindowToText`); en la nota, `Ctrl`+rueda, `Ctrl`+`+`/`-` y `Ctrl`+`0` hacen zoom solo de esa ventana. **Sin escalar** (a propósito): iconos y glifos, el texto de las pestañas del dock y las ventanas de aviso. (6) Botones de la barra de búsqueda de la nota: 24 px y enfocables.
- **Sin publicar (2026-10-30):** Ajustes con el mismo reparto en Windows y Android (General, Aspecto, Notas, Colores, la propia de cada plataforma, Sincronización; tabla en `docs/AJUSTES_PARIDAD.md`; Windows gana la página Aspecto, Android la pestaña Colores y el idioma pasa a General). Aspecto Telecom: la onda del canal comparte fila con la vista previa de la pestaña del dock y la retícula de la nota deja margen a la izquierda. Aspectos retro: botones de icono del dock y del gestor con bisel de 1 px y sin sombra de caída, para que el icono se vea centrado. Sonidos más suaves, sonido de goma, sonidos sugeridos al encender, aviso de silencio en Android. Aspecto nuevo **cuaderno cuadriculado** (claro y oscuro, modos 15 y 16, Windows y Android; vectores de `text-color.json` regenerados solo con las entradas nuevas). Ajustes de Android con chips que se reparten en líneas en vez de pestañas desplazables. En los aspectos con bisel la cara de los botones de icono lleva contorno uniforme y la sombra va por fuera (el icono queda centrado); renglones y retícula de las notas siguen el zoom del texto y dejan hueco al cursor; «Tamaño del texto» llega a las filas creadas por código; Ajustes de Windows con ancho máximo; las ventanas minimizadas vuelven al frente. Variable de entorno `ALDUNE_DATA_DIR` para probar una build de desarrollo con datos aparte. `PRODUCT.md` y `DESIGN.md` en la raíz recogen el contexto de diseño.
- **Windows 2.1.3 (2026-10-30):** (1) en los aspectos XP el título de Ajustes y del gestor de notas se centra en la barra, a la altura de los botones de la ventana (`VerticalAlignment=Center` con la barra en degradado). (2) El marcador de una nota vacía («Escribe algo…») se pintaba **debajo** del cuadro de texto, que es opaco en los aspectos lisos, XP y Bash, y no se veía; ahora va por encima (`Panel.ZIndex`, sin captar clics). Revisados sin cambios: «+» del dock y del gestor (centrados a ≤ 1 px en los 15 aspectos), bisel del dock y bordes de nota de XP oscuro, alto contraste (paleta propia con tests de 7:1). Sin cambios en Android ni en el formato de sync.
- **Windows 2.1.2 (2026-10-30):** el dock en reposo ya no queda tapado por una nota. Notas y docks son ventanas Topmost y entre ellas manda la última activada, así que una nota arrastrada, activada o redimensionada sobre la tira plegada la tapaba y no se veía dónde estaba el dock (con el abanico abierto ya quedaba encima). Ahora `NoteWindow` pide `AppCoordinator.RaiseDocksAboveNotes()` (→ `EdgeDockWindow.RaiseAboveNotes()`, `EnsureTopmost` sin activar) al activarse y al soltar un arrastre. Comprobado con una sonda con ratón real (arrastre por el asa, clic, redimensión y abanico): sin el arreglo la nota tapaba la tira al 100 %; con él se ve. **Contrapartida:** una nota pegada al borde de la pantalla puede tener su botón de cerrar bajo la tira (se sigue pudiendo mover). **Aviso de actualizaciones:** un aviso de versión nueva sin responder se sustituía por otra más reciente solo en el siguiente sondeo (cada 24 h), y «Descargar» abría la página de la versión que enseñaba aunque ya hubiera otra. Ahora, mientras haya un aviso pendiente se vuelve a consultar cada hora (`UpdateNotifier.PendingRecheck`) y «Descargar» consulta una vez más (5 s de margen; sin red abre la del aviso) y abre la última. Comprobado con una sonda con consulta y navegador simulados (`UpdateNotifier` acepta ambos como parámetros opcionales). Servidor de sync sin cambios. **`scripts/update-sync-server.sh`** (`37b4c3e`): el `curl` único a `/health` justo tras `up -d` salía con el código 56 y daba el script por fallido aunque el servidor arrancara bien; ahora reintenta hasta 30 s (0 si responde, 1 con mensaje si no).
- **«Tamaño del texto» en el texto sin tamaño propio (2026-10-30, sin release):** el estilo de `Window` de App.xaml solo casa con el tipo exacto y el manejador de `App.OnStartup` no ponía `FontSize`, así que todo lo que no fija el suyo (botones Export/Archivar/Restaurar/Papelera del gestor, etiquetas de Sync, casillas) se quedaba en 12 px fijos al subir el tamaño. Ahora el manejador pone `AlduneTextCaption` (12: a 100 % no cambia nada) y el dock se fija a 12 a mano porque no escala. Las casillas numéricas de Ajustes (intervalo de sync, retraso de esconder tareas) pasan de `Width` a `MinWidth` para que el número no se corte. Comprobado con sonda a 150 % en XP oscuro.
- **Gestor de notas en pantallas grandes y documentación en inglés (2026-10-30, sin release):** el cuerpo del gestor (pestañas, búsqueda y lista) se queda en un ancho máximo de 1100 px y centrado cuando la ventana se maximiza (barra de título y pie siguen a todo el ancho). README, SECURITY, CONTRIBUTING y las plantillas de issues pasan a inglés por defecto (`README.md` en inglés, `README.es.md` en español); `docs/` sigue en español. GIF y capturas del README rehechos con la interfaz actual (aspecto Claro y Oscuro, libreta, cuaderno de campo y cuaderno cuadriculado) y captura 4 de Fastlane con las pestañas nuevas de Ajustes de Android; la ayuda de sync de Android y SYNC.md dicen «Ajustes → Sincronización».
- **Pulido de bandeja, barras y márgenes (2026-10-30, sin release):** (1) El menú de la bandeja fijaba `BackColor`/`ForeColor` al crearse mientras el renderer leía la paleta al pintar: en XP claro salía con cuerpo oscuro, franja beige y «Salir» ilegible; ahora se refrescan al abrir. (2) La barra de desplazamiento de Ajustes, gestor y listas pasa de bloque relleno con acento y borde sobre pista gris a una pastilla fina con la tinta apagada del aspecto, sobre pista transparente (acento solo al arrastrar), como la de las notas. (3) Márgenes del texto: en Telecom oscuro había 22 px entre el borde de la retícula y el texto a la izquierda y 5 a la derecha; el cuadro de texto de Windows lleva ahora 15 px de relleno derecho (la barra se desplaza 15 px para seguir pegada al borde) y el editor de Android 12 dp (`TEXT_END_PAD`) a la derecha; al partir una línea sobra hasta una letra, así que el hueco derecho oscila en torno al izquierdo (medido: 22/21 px en Windows, 40/36 px en Android con la línea más larga). Auditoría de accesibilidad rápida: ningún botón de icono sin nombre ni tooltip.
- **Windows 2.1.1 (2026-10-30):** la franja del color de la nota en las pestañas del dock (aspecto con «color solo como franja») sigue ahora las esquinas redondeadas en vez de asomar recta (`StripeBar` es un borde con el `CornerRadius` de la tarjeta). Sin más cambios que la 2.1.0.
- **Windows 2.1.0 y Android 1.0.1 (2026-10-30):** aspectos «libreta de viaje» y «cuaderno de campo» (claro y oscuro, con papel bajo el texto en Windows y Android); sonidos de papel y pluma (eventos abrir, cerrar, marcar, borrar y escribir, volumen, apagados de fábrica; `scripts/sounds/generar-sonidos.py`); copia automática de las notas en una carpeta (`AutoExportPlanner`, nunca borra, protegidas fuera; Android por SAF y WorkManager); en Android además barra del editor con iconos, selector de color visual, «Aplicar a las notas existentes», buscar/ajustes/«+» abajo y aviso de versión nueva. Diseño en `docs/specs/2026-10-30-libreta-viaje-sonidos-y-revision-design.md`, paridad en `docs/AJUSTES_PARIDAD.md`.
- **Windows 2.0.0 y Android 1.0.0 (2026-10-29, publicadas):** versiones «definitivas» tras una ronda de pulido. Nombre visible opcional de las notas protegidas (`ProtectedTitle`/`protectedTitle`, en claro, ≤ 60 caracteres, viaja en el sobre sin subir el formato 4; las versiones anteriores lo pierden si reenvían la nota; `docs/PROTECTED_NOTES.md`); menú al mantener pulsada una nota en Android (sin archivar ni borrar: eso es deslizar); Subir/Bajar como alternativa al arrastre; exportar todas las notas en zip y aviso de que sin sync no hay copia; Ajustes de Android en pestañas (General, Aspecto, Notas, Avisos; el modo simplificado sigue en una sola pantalla), más aire bajo las rayas y entre grupos, y ajuste «Teclado al abrir una nota» (solo nuevas por defecto / todas / nunca; `KeyboardOnOpen` en domain); semántica de TalkBack (cabeceras, estado, regiones vivas); QR en horizontal. Seguridad (auditoría del 29-10, sin bloqueantes): recorte del nombre sin partir emojis, release con permisos mínimos (`build` de solo lectura y `publish` aparte), job Android en CI, límite de intentos fallidos de token en el servidor (`ALDUNE_SYNC_MAX_AUTH_FAILURES_PER_MINUTE`, `ALDUNE_SYNC_TRUST_PROXY`), test de que las redirecciones no reenvían `Authorization` a otro host. **Límites conocidos:** el dock de Windows sigue siendo solo de ratón (la ruta por teclado es el gestor y su atajo opcional `Ctrl+Alt+M`); sin TalkBack real con voz ni Narrador probados; `usesCleartextTraffic` sigue permitido en Android porque `network_security_config` no admite rangos de IP (el aviso de la app cubre el caso fuera de la red local); el nombre visible va en claro en el equipo, en el widget y en el aviso.
- **Pulido de uso real (2026-10-27, en `main`, sin release)**: (1) Windows sincroniza **todos los perfiles activados**, no solo el elegido en Ajustes; `SyncBase` y
  `SyncConflict` pasan a clave por perfil (`ProfileKey`, migración que conserva las filas en el primer perfil), detalle en `SYNC.md`. (2) Android: el color de la
  nota como **fondo de toda la tarjeta** (como en Windows) por defecto, con interruptor en Ajustes → Aspecto; los aspectos de terminal (Bash, Telecom, Fósforo)
  van en franja por defecto y elegir aspecto reinicia el ajuste, igual que esquinas y señal. (3) Windows: ajuste `NoteColorAsStripe` («color solo como franja»,
  reutiliza la tarjeta Stripe de Bash en los aspectos de cara llena); en Bash/Telecom/Fósforo la casilla queda apagada con aviso. Probado en el emulador de
  Después, el mismo día: la sync baja las notas de 8 en 8 (antes una a una: con servidor propio o WebDAV la latencia se multiplicaba por el nº de notas),
  en Windows y Android; y **abrir una nota sincroniza antes** (regla de 30 s, `NoteOpenSync` / `SyncEvent.NoteOpened`) con un «Sincronizando…» mientras
  corre la pasada, sin bloquear la edición (decisión: sin red no se podría escribir, y los conflictos ya se guardan). Android: el editor recorre una copia en
  String al estilizar las casillas (borrar mantenido en notas largas). El aviso se probó con una sonda WPF en Windows; en Android no se ha visto (el emulador no tiene perfil de sync).
  Accesibilidad de Android (27-10, emulador con letra al 200 % y volcado del árbol de accesibilidad): la lista, el editor y Ajustes salen con todos los controles
  con nombre y de al menos 48 dp, y a 200 % nada se rompe (las pestañas se desplazan). Cambios: el símbolo de los botones de la barra del editor (↶, ⇥…) ya no se
  lee junto al nombre, y cada nota de la lista lleva acciones de accesibilidad (archivar, papelera, restaurar, borrar) como alternativa a deslizar. **No probado con
  TalkBack real**; arrastrar tareas con el asa sigue sin alternativa accesible (el texto es un único campo).
  Android (lista, interruptor, aspectos, casillas de tareas) y con una sonda WPF en Windows (ventana de nota en Oscuro, Pastel y Medianoche, con y sin
  franja: la franja solo aparece con el ajuste, el fondo es el de la app y el texto se lee). Sin probar con ratón (casillas) ni el dock.
- **Android, varios perfiles de sync (2026-10-26, rama `android-varios-perfiles`, sin publicar)**: el móvil puede unirse a más de un perfil
  a la vez (p. ej. «todas mis notas» + la nota compartida de la familia). Cada perfil tiene su id, su fichero de secretos, su estado, y
  `sync_base`/`sync_conflict` por (perfil, nota) (Room v3, con migración); notas y lápidas siguen siendo del dispositivo. Una pasada recorre
  los perfiles de uno en uno y el fallo de uno no frena a los demás; un perfil adicional nunca ofrece «descartar». El perfil único de las
  versiones anteriores migra solo (id `default`). Crear y compartir perfiles sigue siendo cosa de Windows. Pendiente de la ampliación de la
  sync: QR y pegar/compartir el código, transparencia de la sync en segundo plano, pasada de errores y conflictos. Spec y plan en
  `docs/specs/`. Probado en el emulador (migración desde un perfil, segundo perfil, duplicado, desconectar uno); sin probar en el Pixel.

- **Modo simplificado en Android y ajustes predeterminados (2026-10-27):** Ajustes tiene un botón «Usar modo simplificado» que deja a la vista solo sincronización, aspecto, idioma y la versión (la regla vive en `SettingsSections`, `android/domain`, con test; los ajustes escondidos no cambian). Las casillas de tarea grandes vienen activadas en instalaciones **nuevas** de Android (quien ya tenía la app conserva lo que tenía). En Windows se movió «Mostrar tareas pendientes en el recordatorio» de la página Dock a «Notas y tareas»; el resto de valores por defecto se revisó y se deja igual (ver `docs/DECISIONES-2026-10-27.md`). Temas propios sigue siendo solo de Windows.
- **Nota compartida con la familia (2026-10-27):** funciona con un perfil «Familia» de alcance por etiqueta (`familia`) y **su propio servicio de sync**
  (`aldune-sync-familia`, puerto 8100, `aldune-familia.polacabezon.com`): un perfil con otra clave no puede compartir almacén con el personal
  porque Aldune se niega a escribir si no puede leer ninguna nota (`SyncService`, «sync code can't read the notes in the store»). Ver `SYNC.md` apartado D
  y `servidor-config/docs/actualizar-contenedores.md`.
- **Auditoría de seguridad de la sync (2026-10-27, en `main`, sin release):** hecho: WebDAV en Android descarta un `href` que cambie de host (antes enviaba
  `Authorization` fuera); un borrado remoto no alcanza a una nota que aquí está fuera del alcance por etiqueta (Core y Android); `ApplySyncTombstone` en una
  transacción; no se aplica lo remoto a una nota que cambió durante la pasada; tope de 8 MB por respuesta en los transportes (Core y Android); el servidor
  rechaza el token de ejemplo de `.env.example`; la contraseña de una nota protegida en Android pide 8 (como Windows; desbloquear sigue aceptando desde 4).
  Un borrado **sin firma** ya no manda la nota a la papelera (M2: con el token y sin la clave se podían vaciar las notas, y la purga a los 30 días lo hacía definitivo;
  una marca local no servía porque el estado «papelera» viaja como nota normal): se ignora y se quita del almacén, y la nota se vuelve a publicar (Core y Android,
  escenarios E12a/E12c/E23b actualizados). **Abierto:** descartar sobres auténticos fechados a más de un día, `AppSettings` compartido entre el hilo de sync y la UI, `USER` no
  root en el Dockerfile del servidor (hay que mirar permisos del volumen en el CT 103), límite de intentos de token, lockfiles y `dotnet list package --vulnerable`.
- **Segunda pasada de seguridad (2026-10-27, en `main`, sin release):** revisadas la UI WPF, `SecretVault`, `syncclient`, `QrScanActivity`, DPAPI/`SettingsService`,
  el instalador y los workflows: nada alto. Hecho: un código de perfil con `usuario@` en la URL se rechaza (Core y Android; antes `https://host-bueno.com@atacante.tld/`
  parecía de confianza) y al conectar desde un código se muestra el host real en negrita con un aviso; `http` fuera de la red local pide marcar una casilla antes de
  conectar (`isCleartextOutsideLan`); `FLAG_SECURE` en el campo del código solo en producción; las credenciales no se imprimen en `toString()`; `SettingsService.Save`
  serializado (lock, temporal propio y volcado a disco); el release de Windows comprueba la firma antes de empaquetar y publica `SHA256SUMS.txt`; Inno Setup fijado
  en 6.7.1; el servidor de sync corre como usuario no root y sin capacidades (**un volumen ya existente necesita `chown -R 1654:1654`**, ver `SYNC.md`); verificación de
  dependencias de Gradle (`android/gradle/verification-metadata.xml`, ver `RELEASING.md`); CI falla con paquetes NuGet vulnerables; lint de Android limpio (el
  `@RequiresApi` estaba en `QuickListRow` en vez de en `TileRow`). **Descartado, con razón:** entropía en DPAPI (cualquier proceso del usuario la lee del binario y la migración
  podría dejar sin acceso a las notas), rechazar rutas UNC y reparse points en notas vinculadas (rompe un NAS o OneDrive), lockfiles de NuGet (en modo bloqueado no se
  cumplían al probarlos), descartar en el cliente sobres con fecha antigua (rompería a un dispositivo apagado varios días: una nota sin cambios conserva su fecha), límite
  de intentos de token (el token se compara en tiempo constante y los tras NPM comparten IP; basta con un token largo, el servidor avisa si es corto). El arrastre de
  tareas en Android sí tiene alternativa accesible: los botones «mover arriba/abajo» de la barra del editor. Sin probar: Docker (daemon apagado), el workflow de release
  (solo corre con un tag) y la interfaz nueva en pantalla.
- **Revisión de código sobre lo cambiado desde la 1.7.17 (2026-10-27, `/code-review` high; en `main`, sin release):** arreglado con test (rojo antes): una edición remota más
  nueva que una **lápida local** no se aplicaba nunca (regresión del cambio «no pisar una nota editada durante la pasada»: la foto local solo tiene notas vivas; en Android
  no pasa, `ExpectedVersion.Tombstoned`); un objeto WebDAV por encima de 8 MB tumbaba la pasada entera (ahora se salta, Core y Android); un fallo de descarga seguía
  bajando todo el almacén (ahora corta lo que está en cola); abrir una nota sincronizaba aunque el perfil fuera solo manual. **Arreglado después:** al borrar un perfil de sync se purgan sus bases y conflictos (`DeleteSyncProfile`), con lo que la señal de sus notas ya no se queda en «pendiente» (queda el caso de una nota que sale del alcance por etiqueta sin borrar el perfil, solo cosmético). **Abierto, a propósito:** el temporizador sincroniza también los perfiles manuales cuando hay otro
  automático (decisión del sync multiperfil); `SyncService` copia los campos sueltos a `AppSettings` desde su hilo mientras Ajustes puede estar editándolos (el lock solo
  ordena los guardados). Descartado por falso: un borrado sin firma no puede tocar notas fuera de alcance (solo quita su propio objeto, y solo si no hay nota viva con ese id).
- **Verificado en pantalla (2026-10-27):** emulador de Android con la APK de depuración: un código con servidor `http` público muestra el host en negrita, el aviso de HTTPS y la casilla (Conectar desactivado hasta marcarla); uno de la LAN (`192.168.x`) no pide casilla; uno con `usuario@` se rechaza como dañado. Windows con ventanas reales: `Aldune.Ui.SmokeTests` entero (dock, menú con clic físico, sin franja blanca al crecer) y una sonda con clic físico en la casilla de una tarea en Oscuro, Claro, Pastel y Medianoche (marca ☒ y se lee bien). `FLAG_SECURE` no se vio: va solo en producción.
- **Android: tocar una casilla ya no pone el cursor ahí (2026-10-27, en `main`, sin release):** el editor miraba el toque en la pasada Initial «sin consumir nada», así que el campo ponía el cursor (y abría el teclado) además de marcar la tarea, y en una lista larga el teclado tapaba lo que se iba marcando. Ahora, si el toque cae sobre una casilla, se consume el «down» (no el desplazamiento); fuera de una casilla todo sigue igual. Comprobado en el emulador antes y después (antes la barra del cursor quedaba junto a la casilla; ahora se queda donde estaba). Sin prueba de instrumentación: pendiente de confirmarlo en el móvil real.
- **`/code-review ultra v1.7.17` (2026-10-27; 10 hallazgos, todos verificados contra el código y arreglados en `main`, sin release):**
  (1) borrar un perfil de sync **borraba sus conflictos sin resolver** (guardan la única copia de la versión perdida): ahora solo borra las bases (regresión mía);
  (2) un objeto WebDAV por encima del tope se trataba como «no existe» y se **subía encima** la copia local, pisando lo de otro dispositivo en cada pasada: ahora el transporte
  anota `UnreadableIds` y la sync los deja en paz (Core y Android; en Android entran en `excludedIds`); (3) con `ResponseHeadersRead` el plazo del HttpClient ya no cubría el cuerpo
  y un servidor parado a mitad **colgaba la sync y congelaba «Sincronizar ahora»**: ahora el plazo cubre la petición entera (regresión mía); (4) la migración de bases dejaba todas
  las antiguas en el primer perfil aunque sincronizara otro: `SyncBaseMigration` las pasa una sola vez al perfil que estaba activo (marca `SyncBasesKeyed`); (5) con varios perfiles,
  uno sin red marcaba toda la pasada como fallida y **no se repintaban las ventanas** aunque otro hubiera traído cambios: ahora se repinta si `SyncResult.AppliedChanges`; (6) el
  temporizador y abrir una nota sincronizaban también los perfiles **manuales**: `Synchronize(automaticOnly: true)` los salta («Sincronizar ahora» recorre todos); (7) la señal de
  sync contaba bases de perfiles apagados o fuera de alcance: `BaseKeysCovering` solo cuenta los activados que abarcan la nota; (8) el servidor no root con un volumen de root
  arrancaba «sano» y devolvía 500 a todos: ahora **falla al arrancar con el `chown` en el mensaje** (probado en Docker); (9) en Android la casilla leía `hiddenSpans` de la primera
  composición (regresión mía): `rememberUpdatedState`; (10) la clave de bases se leía tras la red y un cambio de perfil en Ajustes a mitad de pasada la contaminaba: se fija al empezar.
  El de la lápida local y el del tiempo de la prueba de descarga ya estaban arreglados antes.
- **Segunda revisión local (`/code-review` high sobre los arreglos del ultra, 2026-10-27; 9 hallazgos, 6 arreglados):** (1) `Synchronize()` devolvía un falso éxito con la sync
  apagada y nada que hacer (sonaba «hecho» y habilitaba la poda): ahora el error solo se silencia en la pasada automática; (2) con un perfil parcialmente fallido sonaban «hecho» y
  «fallo» a la vez y se marcaba `_syncedSinceStart`: `AfterSync(fullySucceeded, …)`; (3) **la migración de bases movía las del primer perfil al elegido en instalaciones de la
  1.7.18-rc1** (que ya usaba claves por perfil): ahora solo mueve si ESA apertura migró la tabla antigua (`LegacySyncBasesMigratedOnOpen`) y un fallo no impide arrancar; (4) un perfil
  que abarca la nota y nunca la sincronizó no cuenta como al día (`GetSyncBase` exige base de cada perfil que abarca); (5) ajustes antiguos sin lista de perfiles daban «pendiente»
  siempre; (6) BOM en los ficheros nuevos; y el alcance se lee directo del perfil, sin crear unos ajustes por cada refresco de la señal. **Descartados, con razón:** conflictos que
  quedan tras borrar un perfil (es lo buscado: siguen en la lista de conflictos y se resuelven desde ahí); `TransportFactoryOverride` (interno, solo para pruebas); y un objeto con
  **formato que este cliente no entiende (`FormatException`) se sigue sustituyendo al subir** en los cuatro transportes: es el diseño documentado en `FolderSyncTransport.Write` («sustituir un
  archivo inválido es la forma de recuperarlo») y ya existía; cambiarlo (dejar en paz lo ilegible) toca todos los transportes y Android y hay que decidirlo con calma por si un objeto
  roto se queda atascado para siempre. **La `v1.7.18-rc1` publicada es anterior a todos estos arreglos.**
- **Versiones (2026-10-27 noche):** Windows **1.7.18** y Android **0.1.1** (`versionCode` 13) **publicadas** (la de Windows con `SHA256SUMS.txt`; recogen los arreglos de seguridad de la sync, la casilla sin cursor, el aviso del host y el plazo de WebDAV). Antes: Windows 1.7.17 y Android 0.1.0 (la primera estable, `versionCode` 12, sin `-rc`; se publica con `--latest=false`, ver `RELEASING.md`).
- **QR de emparejamiento** (2026-10-27, spec `specs/2026-10-27-qr-emparejamiento-design.md`): Windows muestra el código de
  perfil como QR (`ShareQr` en Core con QRCoder 1.8.1; `ShareQrWindow` con aviso y cierre a los 120 s) y Android lo escanea
  (`QrScanActivity` con CameraX 1.6.2 + `QrDecoder` sobre ZXing core 3.5.4, sin Play Services). Se decidió no dividir el QR en varios
  y que el token no viaje nunca. Cero cambios en el formato de sync.

## Cómo se trabaja

- **Diseño**: cada cambio grande empieza por una especificación en `docs/specs/`.
- **Código**: lógica nueva en `Aldune.Core` con el test primero; la interfaz se verifica con sondas desechables
  (`docs/WPF_PROBES.md`, `docs/ANDROID_PROBES.md`).
- **Integración**: cada cambio se prueba antes de fusionarse a `main` (`dotnet test` y, en Android, `./gradlew test`).
- **Verificación manual**: lo interactivo (hover, foco, arrastrar) se comprueba además a mano en la app real.
- Las explicaciones de por qué se decide algo (WPF, DPAPI, Win32) importan tanto como el qué: conviene mantener ese
  nivel de detalle en los documentos.

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
- ~~**La tira de reposo del dock a veces desaparece del todo, sola, sin nada a pantalla completa**~~ — **RESUELTO (2026-10-27, confirmado por el usuario: ya no pasa).**
  Reportado el 2026-09-09. La causa era una ventana «layered» (`AllowsTransparency`) que dejaba de recomponerse tras cambios de pantallas (Windows mueve las
  ventanas al apagar o encender monitores). Lo que lo arregló, y **se conserva**: `KeepWindowInPlace` (500 ms, devuelve la ventana a su sitio o reconstruye los
  docks), `KeepStripOnTop`, la lectura del color de la tira cada 5 s con repintado tras dos lecturas sin pintar (`RepaintStripIfUnpainted`) y la detección de
  monitores apagados por DDC/CI. Se **retiró el registro de diagnóstico del dock** (`logs\dock.log`: estados del dock, pantallas, energía y sesión); quedó solo
  `logs\errores.log` (`AppLog`) con los errores inesperados de las tareas en segundo plano (sync, recordatorios, archivos vinculados, sonidos). Historial en
  `docs/DOCK_DIAGNOSTICS.md`. Si el fallo volviera, hay que reponer el registro desde el historial de git (`git log -S RecordDiagnostics`).

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
  aquí originalmente.** Se había descartado una ventana nueva de
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

El diario cronológico de sesiones (2026-09-02 a 2026-10-17: qué se hizo en cada ronda, con el porqué y las trampas) se retiró
del árbol; sigue en el historial de git (`docs/historia/STATUS-2026-09.md` y `docs/historia/STATUS-2026-10.md`).

Las especificaciones de fases ya publicadas están en `historia/specs/`; los informes de fase y los
planes de ejecución se retiraron del árbol (siguen en el historial de git). Las vigentes siguen en `specs/`.
