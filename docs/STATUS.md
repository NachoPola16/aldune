# Aldune — Estado del proyecto

Documento de continuidad: si retomas este proyecto en otra sesión, otro chat, u
otra IA, empieza por aquí. Todo lo importante vive en el repositorio (specs,
planes, commits), no solo en una conversación concreta.

> **Nota de nombres.** El proyecto se llamó **Fanote** hasta el 2026-09-15. Las entradas anteriores de
> este documento describen el proyecto tal y como estaba entonces, así que conservan ese nombre y los
> identificadores de aquella época. Los que siguen apareciendo hoy son compatibilidad deliberada, no
> restos de renombrado: `%LOCALAPPDATA%\Fanote` (migración de datos), los prefijos de los códigos de
> perfil `fanote-profile-v1:` y `fanote-profile-v2:` y el volumen `fanote-sync-data` de un servidor
> que ya sincronizaba. El mapa completo está en `docs/BRANDING.md`.

## Qué es Aldune

App de notas para Windows (WPF/.NET 10), inspirada en Hold My Notes / Noty /
noty-sepia (macOS): notas ancladas al borde de la pantalla que se despliegan
en abanico al pasar el ratón por encima. Ver el diseño completo en
`docs/superpowers/specs/2026-08-30-aldune-v1-design.md` — ese documento es la
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

## Historial: pulido visual y bugs visuales (sesión 2026-09-02)

Bounded, sin spec/plan formal (brainstorming → implementación directa,
verificado a mano en cada paso). Por si hace falta el detalle técnico luego:

1. **Corte de notas en el panel desplegado — ARREGLADO.** El panel tenía
   tamaño fijo (`ExpandedThickness`/`ExpandedLength` en `EdgeGeometry.cs`) y
   el `ItemsControl` de pestañas vivía en un `StackPanel` que se dimensiona a
   su contenido ignorando el tamaño de la ventana — todo lo que no cabía
   quedaba cortado por el HWND sin forma de llegar a ello. Fix: en
   `EdgeDockWindow.xaml` el `StackPanel` raíz pasó a ser un `Grid` de dos
   filas (`*` + `Auto`), con la lista de pestañas dentro de un
   `ScrollViewer` (fila `*`) y el botón "+ Nueva nota" fijo en la fila
   `Auto` de abajo, siempre visible.
2. **Glitch de un frame al desplegar — mitigado, no resuelto del todo.**
   Causa raíz: el pill y el panel desplegado tenían una diferencia de tamaño
   tan grande (12px → 220px) que, durante los ~200ms de la animación, las
   pestañas se renderizaban encajadas en anchos/altos intermedios que no les
   cabían. Mitigación en `EdgeDockWindow.xaml.cs` (`ApplyGeometry`): el
   contenido se oculta (`Opacity`) al empezar a colapsar y solo reaparece
   con fundido en los últimos 80ms de la expansión. El crecimiento en sí
   se sigue sintiendo algo brusco (es un salto de tamaño grande, no ya un
   defecto de render) — el usuario decidió no perseguir esto más.
3. **Pill en reposo con margen/esquinas redondeadas/sombra.** Nueva
   constante `EdgeGeometry.PillEdgeMargin` (6px) que separa el pill del
   borde físico de pantalla (solo el pill; el panel desplegado se queda
   flush). Esquinas redondeadas y sombra vía APIs nativas de Windows 11
   (`NativeMethods.ApplyRoundedCornersAndShadow`: `DwmSetWindowAttribute`
   con `DWMWA_WINDOW_CORNER_PREFERENCE` + `DwmExtendFrameIntoClientArea`
   con márgenes negativos para la sombra estándar del sistema) —
   deliberadamente sin `AllowsTransparency` (la spec original ya descarta
   eso porque rompe ClearType, ver diseño v1). Redondea las 4 esquinas por
   igual (la API no permite solo 2); en el panel desplegado, al seguir
   pegado al borde físico, el recorte en las esquinas ancladas es
   imperceptible. En Windows 10 (sin esta API) no hace nada, sin roturas.
4. **Ventana de nota teñida del color de la nota.** `NoteWindow` pinta su
   `Background` y el del `TextBox` con el mismo pastel que ya usa la
   pestaña del dock (`NoteWindow.ApplyColor`).
5. **Pastillas de color en el pill de reposo** (inspirado en Hold My
   Notes, capturas del usuario). `EdgeGeometry.PillThickness` subió de 12
   a 20px para tener sitio. `EdgeDockWindow.xaml` tiene ahora dos paneles
   superpuestos en el mismo `Grid`: `PanelContent` (lista completa, visible
   expandido) y `PillSwatches` (mini cuadraditos de color por nota activa,
   visible en reposo) — se alternan igual que el punto 2, con fundido
   cruzado. **Cuidado con este patrón**: `Opacity` NO desactiva el
   hit-testing en WPF — hubo que fijar `IsHitTestVisible` en ambos
   explícitamente en `ApplyGeometry` (sincronizado, sin animar) porque el
   panel invisible seguía interceptando los clics del panel visible debajo.
   Si se añade un tercer panel superpuesto algún día, no olvidar este punto.
6. **Selector de color en la ventana de nota.** Fila de 6 pastillas
   (paleta compartida, extraída a `Aldune.Windowing.NoteColorPalette` para
   que la usen tanto `EdgeDockWindow` como `NoteWindow`) — clic para
   recolorear una nota ya creada. Nuevo `NotesRepository.SetColor`
   (TDD, mismo patrón que `SetState`/`UpdateText`). Se decidió
   deliberadamente NO añadir un desplegable de color al crear la nota con
   "+" — el color se sigue asignando automáticamente (rotación de la
   paleta) y se cambia después desde la propia nota si se quiere; añadir
   un flyout sobre el pill (no-activating) se consideró complejidad
   innecesaria para el beneficio.
7. **Ventana de nota sin barra de título nativa.** El usuario reportó que
   la barra de título de `NoteWindow` (`WindowStyle="ToolWindow"`) salía
   negra (tema oscuro de Windows aplicado a la barra nativa, no un fallo
   intermitente). Cambiado a `WindowStyle="None"` + `WindowChrome`
   (`System.Windows.Shell`, ya viene en WPF, no requiere paquete nuevo):
   `CaptionHeight` da una zona arrastrable invisible arriba (el propio
   color de la nota se ve a través, sin barra separada),
   `ResizeBorderThickness` mantiene el redimensionado por los bordes, y
   `GlassFrameThickness="-1"` da la sombra nativa del sistema (mismo
   truco que `NativeMethods.ApplyRoundedCornersAndShadow` pero vía
   WindowChrome en vez de P/Invoke manual, para no duplicar la llamada a
   `DwmExtendFrameIntoClientArea`). Esquinas redondeadas vía
   `NativeMethods.ApplyRoundedCorners` (la mitad de la función que ya
   usaba el dock, ahora separada de la sombra en dos métodos). X de
   cierre propia (`CloseButton`) integrada en la esquina, con
   `WindowChrome.IsHitTestVisibleInChrome="True"` (necesario: por
   defecto WindowChrome trata toda la zona de `CaptionHeight` como
   arrastre, no clic). Botones Archivar/Papelera con un estilo plano
   nuevo (`NoteActionButtonStyle`) a juego con el resto del rediseño.
8. **Ajustes finos de la ventana de nota** (ronda de feedback visual
   directa sobre capturas): margen uniforme de 20px por los cuatro lados
   (antes descompensado, más aire arriba que a los lados/abajo);
   `Papelera` con estilo propio en rojizo (`NoteDangerButtonStyle`,
   plantilla separada de `NoteActionButtonStyle` — no `BasedOn`, porque el
   color de *hover* también tenía que teñirse de rojo, no solo el de
   reposo) para distinguir la acción destructiva a simple vista;
   tipografía del cuerpo a `Segoe UI Variable Text` 14px (antes heredaba
   el tamaño pequeño por defecto de `TextBox`; la spec v1 ya pedía "una
   tipografía elegida por legibilidad" pero nunca se llegó a fijar
   ninguna); texto de marcador de posición ("Escribe algo…") cuando la
   nota está vacía, vía un `TextBlock` superpuesto con un `DataTrigger`
   sobre `Text` del `TextBox` (solo visual, no se guarda como valor real).
   **Decisión explícita que NO se hizo**: no llevar el efecto "título en
   negrita" al propio cuadro de texto de la nota (solo existe en la
   pestaña del dock y la barra de título) — hacerlo dentro exigiría texto
   enriquecido (`RichTextBox` o un control de título separado), justo la
   complejidad que la spec v1 evitó a propósito al decidir no añadir un
   campo de título real. Si se retoma, es una mejora real pero ya no un
   retoque rápido.
9. **Iconos en Archivar/Papelera + tipografía unificada en toda la app.**
   Los botones ahora llevan icono (glifo de "Segoe Fluent Icons", con
   "Segoe MDL2 Assets" como *fallback* — ambas ya vienen con Windows, sin
   assets nuevos: `U+E7B8` archivo, `U+E74D` papelera) además del texto.
   `Segoe UI Variable Text` (la fuente de sistema por defecto en Windows
   11) se aplica ahora a **toda** la UI, no solo al cuerpo de la nota —
   vía un único `Style TargetType="Window"` en `App.xaml.Resources`, que
   basta porque `FontFamily` es una propiedad heredada: se fija una vez
   en cada `Window` (implícito, se aplica también a las subclases
   `EdgeDockWindow`/`NoteWindow`) y cae en cascada a todos sus hijos que
   no la fijen explícitamente ellos mismos. Con esto, el `FontFamily`
   explícito que se había puesto en `TextBody` quedó redundante y se
   quitó.

## Rediseño de pestañas en abanico estilo Hold My Notes — HECHO (ver sección
propia más abajo)

Idea originalmente planteada aquí como brainstorming sin empezar; ya
implementada de punta a punta (spec → plan → subagent-driven-development).
Ver "Rediseño de pestañas en abanico" más abajo para el detalle técnico
completo.

## Botón "Archivadas" + gestor de notas (sesión 2026-09-02, bounded)

Implementado y verificado a mano. Resumen para no repetir decisiones si se
retoca. **Actualización posterior**: el toggle "Archivadas"/"Activas" del
propio dock descrito en el primer punto fue **retirado** por el rediseño
de pestañas en abanico (ver su propia sección más abajo) — el dock ahora
solo muestra notas activas y `NotesManagerWindow` es la única vía para
ver archivadas/papelera. El resto de esta sección (purga automática,
`NotesManagerWindow`, lecciones de WPF) sigue vigente tal cual.

- **Dock (RETIRADO, ver nota arriba)**: botón "Archivadas"/"Activas"
  (`EdgeDockWindow`, campo `_viewingArchive`) alternaba `TabsList` entre
  notas activas y archivadas+papelera combinadas (dos `GetByState` +
  `Concat`, sin método nuevo de repositorio — no hacía falta orden global
  para una vista de repaso). Cada pestaña mostraba una etiqueta pequeña
  "Archivada"/"Papelera" (`NoteStateLabelConverter`, vacía y colapsada
  para notas activas — puramente derivada de `Note.State`). `NoteWindow`
  sigue mostrando "Restaurar" en vez de Archivar/Papelera cuando
  `Note.State != Active` (esto no cambió). Sin borrado permanente manual.
- **Purga automática de la papelera**: `NotesRepository.PurgeExpiredTrash`
  (TDD) borra notas en `Trashed` con `UpdatedAt` más viejo que
  `DefaultTrashRetentionDays` (30, decisión del usuario). Se usa
  `UpdatedAt` en vez de una columna `TrashedAt` nueva **a propósito**: esta
  app no tiene sistema de migraciones (`NotesDatabase` solo hace
  `CREATE TABLE IF NOT EXISTS`, una vez) — añadir una columna rompería las
  bases de datos ya existentes. Se ejecuta al arrancar (`App.xaml.cs`) —
  desde el rediseño de pestañas en abanico, ese arranque es el único
  disparador (antes también se ejecutaba al entrar en la vista Archivadas
  del dock, que ya no existe).
- **`NotesManagerWindow`** (antes `ArchiveManagerWindow`, renombrada — ver
  más abajo): ventana aparte para acciones en bloque (checkboxes,
  "Seleccionar todo", Archivar/Restaurar/A la papelera, filtro
  Todas/Activas/Archivadas/Papelera). Se creó una ventana propia en vez de
  meter esto en el panel del dock porque **no cupo**: con checkboxes +
  toolbar de 3 botones + lista en un panel de 320px, "A la papelera"
  quedaba parcialmente fuera de la ventana y no se podía pulsar de forma
  fiable — el mismo tipo de bug de overflow ya visto y arreglado una vez
  en el propio dock. Lección aplicada aquí: la barra de botones usa
  `WrapPanel`, no `StackPanel`, precisamente para que un overflow futuro
  baje a una segunda línea en vez de salirse de la ventana sin avisar.
  Empezó como "gestor de archivadas/papelera" pero el usuario pidió que
  "Todas" incluyera también las activas — de ahí el renombrado a
  `NotesManagerWindow` y el filtro con 4 estados en vez de 2.
- **Selección en listas WPF**: `NoteRow` (`Aldune.Windowing`) envuelve cada
  `Note` con un `IsSelected` bindable (`INotifyPropertyChanged`) —
  deliberadamente fuera de `Aldune.Core`, la selección es un concepto de
  UI, no de dominio. Se usa tanto en `NotesManagerWindow` como (antes,
  luego revertido) en el propio dock.
- **Bug real encontrado y arreglado**: un `RadioButton` con
  `IsChecked="True"` puesto en XAML dispara su evento `Checked` **durante**
  `InitializeComponent()`, antes de que elementos declarados más abajo en
  el mismo árbol visual (`RowsList`) existan — causaba
  `NullReferenceException` al abrir la ventana. Arreglo: quitar
  `IsChecked="True"` del XAML y fijarlo por código *después* de
  `InitializeComponent()`. Tenerlo en cuenta si se añade otro control con
  estado inicial "marcado" que dispare un handler en el constructor.
- **Descartado en el camino**: checkboxes de selección múltiple dentro del
  propio dock (demasiado apretado en 320px, llevó al rediseño de arriba);
  un desplegable de color al crear nota con "+" (se mantiene la rotación
  automática + cambio posterior desde la nota); borrado permanente manual
  (lo cubre la purga automática).

## Fase 3a: multi-monitor real + DPI (sesión 2026-09-02, arquitectónico)

Spec: `docs/superpowers/specs/2026-09-02-aldune-phase3a-multimonitor-dpi-design.md`.
Plan: `docs/superpowers/plans/2026-09-02-aldune-phase3a-multimonitor-dpi.md`.
Implementado siguiendo el plan tarea por tarea (5 tareas, 5 commits) mientras
el usuario estaba fuera — ver ese hueco de verificación abajo.

- **`Aldune.Core.MonitorInfo` + `DpiConversion`** (TDD): tipo de dato puro
  por monitor (nombre de dispositivo, área de trabajo, escala de DPI,
  si es el principal) y la conversión píxeles→DIP que necesita Win32
  (Win32 da píxeles físicos; `Window.Left/Top/Width/Height` de WPF son
  DIPs relativas al DPI de cada monitor una vez declarado `PerMonitorV2`).
  3 tests nuevos.
- **`Aldune.Interop.MonitorEnumerator`**: `EnumDisplayMonitors` +
  `GetMonitorInfoW` + `GetDpiForMonitor` (Win32 puro, sin dependencia
  nueva, mismo patrón que `NativeMethods.cs`). Si `GetDpiForMonitor` falla
  para un monitor, asume 96 DPI en vez de propagar el error. Verificado
  contra el hardware real del usuario (un volcado temporal a fichero, ya
  que no había forma de mostrar un `MessageBox` interactivo sin el
  usuario delante): detectó correctamente el monitor principal
  (2560×1440) y el secundario en vertical (1440×2560, con offset negativo
  respecto al principal).
- **`src/Aldune/app.manifest`** declarando `PerMonitorV2`, referenciado
  desde `Aldune.csproj` (`<ApplicationManifest>`). No existía ningún
  manifest antes — la app corría con el DPI-awareness por defecto de
  Windows para un proceso sin declarar.
- **`AppCoordinator`** (`Aldune.Windowing`, una instancia para toda la
  app): se lleva `_openNoteWindows` y el `NotesManagerWindow` único que
  antes vivían dentro de `EdgeDockWindow` (que ahora es una instancia por
  monitor). `NoteWindow`/`NotesManagerWindow` ya no reciben su
  `EdgeDockWindow` "dueño", reciben el coordinador y llaman a
  `RefreshAll()` — con varios docks mostrando la misma lista de notas
  (ver prerrequisitos de arriba), archivar desde cualquiera tiene que
  refrescarlos todos, no solo uno. `EdgeDockWindow.PositionNoteWindow`
  pasó de `private` a `internal` porque el coordinador necesita llamarlo.
- **`App.xaml.cs`**: crea un `EdgeDockWindow` por cada monitor que
  devuelve `MonitorEnumerator.EnumerateMonitors()` (antes, uno solo con
  `SystemParameters.WorkArea`, que solo daba el principal). Cero monitores
  detectados se trata como un cuarto modo de fallo de arranque, igual que
  los tres que ya había.
- **Bug real de WPF encontrado y arreglado**: `EdgeDockWindow.ApplyGeometry`
  animaba `Left/Top/Width/Height` sin `From` explícito, confiando en que
  WPF mirase el valor "actual" de la propiedad como origen implícito. Al
  añadir el `app.manifest` (`PerMonitorV2`), la creación de la ventana
  puede disparar una pasada de resize interna adicional que reevalúa esa
  animación **antes** de que esas propiedades tengan nunca un valor real,
  viendo el `NaN` por defecto de WPF — lanzaba
  `System.Windows.Media.Animation.AnimationException: ... cannot use
  default origin value of 'NaN'`. Arreglo: `EdgeDockWindow` ahora lleva su
  propio campo `_currentRect` con la geometría "actual" tal como la
  entiende el propio código, y siempre pasa `From` y `To` explícitos a los
  `DoubleAnimation`, sin depender nunca de esa búsqueda implícita.
  Detectado y arreglado con la app corriendo de verdad contra los dos
  monitores del usuario (inspección de rects de ventana vía P/Invoke
  `EnumWindows`/`GetWindowRect` desde PowerShell, ya que no había nadie
  delante de la pantalla para verlo a simple vista). **Si se toca de nuevo
  `ApplyGeometry`, no volver a confiar en el origen implícito de una
  animación — siempre pasar `From` explícito.**
- **Verificado de forma automática primero**: build limpio, 74/74 tests, y
  los rects de ventana reales de los dos docks (`(2534,640)-(2554,800)` en
  el monitor principal, `(-26,659)-(-6,819)` en el secundario vertical)
  coincidían con lo que calcula `EdgeGeometry.PillRect` para cada área de
  trabajo real.
- **Checklist manual — HECHO, encontró un bug real.** El usuario probó
  con los dos monitores: crear una nota en el dock de un monitor no
  actualizaba la pestaña en el dock del otro monitor **hasta** que pasaba
  otra cosa (cerrar la ventana de la nota, archivar, etc.) que sí disparase
  un refresco global. Causa: `EdgeDockWindow.OnNewNoteClick` llamaba a su
  propio `Refresh()` en vez de `_coordinator.RefreshAll()` — un descuido
  del refactor de la Fase 3a (sí se había cambiado `OnTabClick` y
  `OnManageArchiveClick`, pero no este). Arreglado. El resto del checklist
  (hover/expandir/pestañas/"Gestionar notas" desde los dos docks) quedó
  confirmado sin más problemas.
- **Tamaño del pill/panel según nº de notas** (pedido tras probar): antes
  `PillRect`/`ExpandedRect` usaban una longitud fija
  (`PillLength`=160, `ExpandedLength`=320) sin importar cuántas notas
  hubiera — con pocas notas quedaba mucho hueco vacío. Ahora
  `EdgeGeometry.PillRect`/`ExpandedRect` reciben `noteCount` y calculan la
  longitud como `noteCount * PerNoteLength`, acotada entre un mínimo y un
  máximo (`PillMinLength`/`PillMaxLength`=60/160,
  `ExpandedMinLength`/`ExpandedMaxLength`=120/320) — TDD, 6 tests nuevos.
  `EdgeDockWindow.SetNotes` ahora guarda el recuento y vuelve a llamar a
  `ApplyGeometry()`, así que el tamaño se recalcula en caliente al
  crear/archivar/borrar notas, no solo al expandir/colapsar. **Nota del
  usuario**: el panel desplegado ahora puede verse pequeño con pocas
  notas (una franja corta con solo 1-2 pestañas) — aceptado tal cual por
  ahora, se espera que el rediseño de pestañas en abanico estilo Hold My
  Notes (ver más abajo) cambie esta forma de todos modos.

## Rediseño de pestañas en abanico (sesión 2026-09-02/03, arquitectónico)

Spec: `docs/superpowers/specs/2026-09-02-aldune-fan-tabs-redesign-design.md`.
Plan: `docs/superpowers/plans/2026-09-02-aldune-fan-tabs-redesign.md`.
Ejecutado con `superpowers:subagent-driven-development` en un worktree
propio, 4 tareas + un arreglo post-hoc + una ronda de arreglo tras la
revisión final de toda la rama.

- **Qué cambia frente al panel anterior**: el `ItemsControl` vertical de
  botones se sustituye por pestañas individuales con etiqueta de texto
  rotada -90° (`LayoutTransform`, no `RenderTransform` — importante
  porque intercambia los ejes de medida: el `Height` de la pestaña pasa a
  ser el ancho disponible para el texto rotado), entrada escalonada
  (~45ms por pestaña) al desplegarse, y apertura de nota que crece desde
  la posición real de su propia pestaña en pantalla (antes crecía desde
  un origen genérico). Se retiró el toggle "Archivadas"/"Activas" del
  dock (ver nota en su sección de arriba) y los botones "+"/engranaje
  pasaron a iconos circulares pequeños.
- **`Aldune.Core.EdgeGeometry.ExpandedPerNoteLength`** subió de 40 a 88 —
  la pestaña pasó a `Height="80"` para dar sitio real a la etiqueta
  rotada (a 36px solo había ~24px de ancho para el texto, truncándolo a
  1-2 caracteres).
- **Bug real de WPF encontrado en pruebas manuales**: un
  `TranslateTransform` declarado como XAML estático dentro de un
  `DataTemplate` acaba congelado/compartido (`Freezable`) entre todas las
  pestañas generadas — lanzaba `Cannot animate ... because the object is
  sealed or frozen`. Arreglo: crear una instancia nueva de
  `TranslateTransform` por código dentro de `OnTabLoaded`/
  `PlayTabEntrance`, nunca una compartida en XAML.
- **Hallazgos de la revisión final de toda la rama** (visibles solo mirando
  el diff combinado, no tarea por tarea) y su arreglo:
  1. La animación de entrada escalonada solo se disparaba una vez al
     arrancar (ligada al evento `Loaded`, que no vuelve a dispararse al
     pasar el ratón) — extraídos `PlayTabEntrance`/`ResetTabEntrance`
     para que `ApplyGeometry` la repita en cada expansión y la resetee en
     cada colapso.
  2. Botones "+"/engranaje inalcanzables bajo el borde de scroll — vuelto
     a un `Grid` de dos filas (lista con scroll + fila fija de botones,
     el mismo patrón que ya se había aplicado una vez antes para el mismo
     tipo de problema, ver "Historial: pulido visual" arriba).
  3. `NoteWindow.AnimateFrom` dejaba animaciones de `Left/Top/Width/Height`
     enganchadas para siempre (`FillBehavior.HoldEnd` sin limpiar) —
     añadidos handlers `Completed` que limpian y fijan el valor final como
     valor local plano, igual que ya hace `EdgeDockWindow.ApplyGeometry`.
  4. Dos comentarios que aún mencionaban el toggle "Archivadas" del dock
     ya retirado — reescritos.
- **Verificado**: build limpio, 80/80 tests, app arranca sin excepciones.

### Bugs reales encontrados en la verificación manual del usuario (sesión 2026-09-03)

El pase de verificación manual de arriba SÍ encontró problemas reales —
diagnosticados con `superpowers:systematic-debugging` (instrumentación
temporal en `ApplyGeometry`/`PollHoverState` + sondeo de `GetWindowRect`
por Win32 desde PowerShell contra el proceso real, no adivinando por
capturas). Commit `af6b569`. Los tres bugs, todos detrás del mismo
síntoma visible ("las pestañas se ven cortadas / el panel no se
comporta bien"):

1. `EdgeGeometry.ExpandedRect` nunca reservaba sitio para la fila fija de
   botones "+"/engranaje — su hueco salía del mismo presupuesto de
   longitud que las pestañas, así que la última pestaña quedaba a
   caballo del borde del scroll. Añadida `ExpandedFooterLength = 80`.
2. `ExpandedMaxLength` (320) no era múltiplo de `ExpandedPerNoteLength`
   (88) — con notas suficientes para tocar el máximo, la vista inicial
   sin hacer scroll ya cortaba la última pestaña a medias. Subido a 352
   (4×88).
3. El panel oscilaba entre colapsado/expandido cada ~200-400ms con el
   ratón quieto encima: `ApplyGeometry` mueve el borde de la ventana
   hacia fuera al expandir, y Win32 dispara un `WM_MOUSELEAVE` falso
   cuando una ventana se mueve/redimensiona bajo un cursor quieto. Peor:
   una vez que WPF dispara `MouseLeave` una vez, da por hecho
   internamente que el puntero ya se fue — un handler que simplemente
   ignora el evento falso no lo deshace, así que WPF nunca vuelve a
   avisar de un `MouseLeave` real después, y el panel se queda
   expandido para siempre. Sustituido `MouseEnter`/`MouseLeave` por un
   sondeo cada 50ms de la posición real del cursor
   (`NativeMethods.GetCursorScreenPosition`, `GetCursorPos` fresco vía
   P/Invoke — `Mouse.GetPosition` refleja el último mensaje que recibió
   esa ventana, que se queda obsoleto en cuanto deja de recibir
   ninguno). Además, la `Height` animada en concreto podía terminar su
   animación sin que la ventana real llegara a redimensionarse (`Width`
   nunca mostró esto) — causa no resuelta del todo; cada propiedad
   ahora fija su valor final como valor local plano al completar la
   animación (mismo patrón que `NoteWindow.AnimateFrom`).

**Incidente de privacidad**: durante el diagnóstico se tomó una captura
de toda la pantalla virtual (`Graphics.CopyFromScreen`) que capturó
ventanas ajenas del usuario (chat, otro editor, overlay de directo) —
borrada de inmediato, no se repitió. Toda verificación visual posterior
se hizo con capturas del propio usuario o con números de geometría
(`GetWindowRect`), nunca con una captura de pantalla propia.

### Diseño visual de las pestañas — validado en maqueta, PENDIENTE de implementar en código

Tras arreglar los bugs de arriba, el usuario comparó el resultado
contra la captura de referencia (una app tipo Hold My Notes) y señaló
que **el ancho de la pestaña se dejó fuera de alcance en la spec
original** ("el panel sigue usando `ExpandedRect`... lo que cambia es
solo cómo se rellena por dentro") — por eso cada pestaña salió tan
ancha como el panel entero (`ExpandedThickness=220`), en vez de ser
una tira estrecha como en la referencia. Esto NO es un bug de
implementación: el código hace exactamente lo que pedía la spec: la
propia spec tenía un hueco frente a la referencia.

Iterado en 4 rondas de una maqueta visual externa, no en el código real. La maqueta no forma parte
del repositorio; se conserva aquí únicamente la decisión de diseño resultante.
**Diseño validado por el usuario** (última ronda de la maqueta):

- Cada pestaña con **ancho creciente según su índice** en la pila
  (`Width ≈ 32 + índice × 14`, aprox. — el índice ya se calcula en
  `PlayTabEntrance` para el retardo de la animación), no todas del
  mismo ancho — la de más abajo/más profunda en la pila sobresale
  claramente más que la de arriba, como un fajo de fichas en abanico.
  Solape vertical entre pestañas consecutivas vía `Margin.Top`
  negativo (~19px en la maqueta).
- `HorizontalAlignment="Left"` en vez de `Stretch` (o el lado que
  corresponda según el borde del dock) — las pestañas cuelgan del
  lado interior del panel, no ocupan todo el ancho.
- El fondo oscuro del dock (`Background="#3A3A3A"`) **se mantiene** —
  Aldune nunca puede ser `AllowsTransparency` (rompe ClearType, ya
  descartado en la spec v1) — pero se **ciñe al ancho de la pestaña
  más ancha** en vez de ser una caja de tamaño fijo con hueco muerto
  alrededor (`width: max-content` en la maqueta CSS — en WPF,
  `HorizontalAlignment="Left"`/`Right` + `Width` en el `Grid`/`Border`
  contenedor en vez de un ancho fijo).
- `EdgeGeometry.ExpandedThickness` (220, fijo) **no cambia** — sigue
  fuera de alcance, la geometría exterior del panel es la misma que ya
  está arreglada arriba.
- **Pendiente de decidir, aparcado a propósito**: el usuario preferiría
  que no hubiera fondo oscuro en absoluto entre pestañas (que se vea
  el escritorio a través de los huecos, como en la referencia). Es
  técnicamente posible sin romper ClearType — no vía
  `AllowsTransparency`, sino recortando la **forma** de la ventana con
  Win32 (`SetWindowRgn`), manteniendo la ventana igual de opaca/nítida
  pero invisible fuera del contorno de las pestañas. Recortar así una
  forma no rectangular, recalculada en cada cambio de nº de notas y en
  cada frame de la animación de entrada escalonada, con cuidado del
  DPI por monitor, es un cambio real de arquitectura — el usuario
  decidió explícitamente tratarlo como su propia sesión de diseño
  (brainstorming → spec → plan), no improvisarlo. **Empieza aquí la
  próxima vez que se retome esto.**

## Rediseño de movimiento y forma del dock (sesión 2026-09-04, arquitectónico)

Rama `dock-motion-shape`, encima de `a4c6db8`. Sin spec/plan formal: partió
de una queja directa del usuario ("no me gusta la apariencia ni las
animaciones"), acotada en una ronda de preguntas a **movimiento + forma del
dock** (la dirección estética general se dejó sin decidir a propósito, y el
modo "Papel vintage" explícitamente para más adelante).

### Por qué no bastaba con retocar números

El commit anterior (`a4c6db8`, ese mismo día) ya había probado el arreglo
conservador — alargar la transición de 200 a 320ms y meterle un easing — y
no funcionó. El problema no eran las cifras sino el modelo:

- Se animaban `Left/Top/Width/Height` del **HWND**. WPF rehace el layout en
  cada frame intermedio, y de ahí sale toda la familia de fallos que
  documentan las secciones de arriba.
- Como el contenido no se puede ver bien durante ese resize, se ocultaba y
  se hacía fundido con `BeginTime=190ms` sobre 320ms: **el 60% de la
  apertura era una caja vacía creciendo**, y nada se movía a la vez.
- `QuadraticEase` es la ease-out más débil que existe; apenas se lee como
  un asentamiento.
- El escalonado (55ms × índice, más los 190ms de offset) dejaba la última
  pestaña sin asentar hasta ~760ms con 6 notas.

### El cambio

**La ventana del dock ya no se redimensiona nunca.** Siempre ocupa
`EdgeGeometry.WindowRect`; lo único que se anima es la región recortada.
El layout de WPF se mide una vez, a tamaño final, y jamás en un tamaño
intermedio — esa clase entera de fallo deja de ser posible por
construcción. En reposo la región son tiras de 20px del color de cada
nota pegadas al borde; al pasar el ratón, el borde izquierdo de cada
pestaña barre hacia fuera, escalonado.

Con eso desaparecen también: el temporizador mágico de 190ms acoplado por
comentario al `BeginTime` del fundido, el fundido cruzado entre
`PanelContent` y `PillSwatches` (y el patrón de `Opacity` +
`IsHitTestVisible` sincronizados a mano que exigía — ver punto 5 del
"pulido visual"), y el encendido/apagado de la sombra DWM.

**Se comprobó antes de apostar por esto** (búsqueda en la documentación de
Microsoft, a sugerencia del usuario): `SetWindowRgn` emite
`WM_WINDOWPOSCHANGING`/`WM_WINDOWPOSCHANGED` en **cada** llamada, y lo
recomendado para animar forma por frame son *layered windows* — que la
spec v1 descarta porque rompen ClearType. Por eso el recálculo por frame
está acotado a propósito: solo durante los ~280ms de la transición (≈17
frames), nunca en reposo ni desplegado quieto, y saltándose la llamada si
los rects redondeados a entero no han cambiado. El `WM_MOUSELEAVE` espurio
que eso podría provocar ya no importa, porque el hover se sondea contra la
posición real del cursor desde `af6b569`.

### Números que se contradecían, ahora atados por tests

- `ExpandedPerNoteLength` presupuestaba 88px por nota mientras el layout
  usaba `Margin=-28` sobre pestañas de 80px (paso real 52). Ahora
  `TabPitch = TabHeight + TabGap`, con un test que lo fija.
- Ese solape de -28 hacía que `CombineRgn`/`RGN_OR` **fundiera las pestañas
  en una sola mancha** en vez de un abanico. `TabGap` es positivo, también
  con test.
- `width = 32 + índice*14` no estaba acotado: a partir de ~14 notas la
  pestaña era más ancha que la ventana. Ahora interpola sobre
  `índice/(total-1)`, acotada entre `TabMinWidth` y `TabMaxWidth` sea cual
  sea el número de notas.
- `ExpandedThickness` eran 220px para pestañas de 32-74px. `WindowThickness`
  son 140, derivados de `TabMaxWidth`.

### Bug encontrado con la app corriendo

Verificado con `GetWindowRgn`/`GetRegionData` por P/Invoke desde PowerShell
contra el proceso real (**no** con capturas de pantalla — ver el incidente
de privacidad de la sesión 2026-09-03). La caja envolvente de la región
llegaba a y=432 cuando las 4 pestañas visibles acaban en y=344: el
`ItemsControl` no está virtualizado, así que existen `Button`s colocados
por debajo del viewport del `ScrollViewer`, `TranslatePoint` devuelve su
posición igualmente, y la región abría un agujero justo donde el
`ScrollViewer` ya no dibuja nada. Arreglado acotando cada pieza al viewport.
Tras el arreglo, la región en reposo es exactamente 4 pestañas en
y 0..80 / 88..168 / 176..256 / 264..344, tira de 20px a ras del borde,
idéntica en los dos monitores.

### Paleta

Derivada en OKLCH, no a ojo en hex. Las seis caras comparten L=0.87 exacto
con el croma acotado hue a hue al máximo que sRGB representa — la paleta
anterior mezclaba claridades dispares, así que unas notas pesaban más que
otras sin que eso significara nada. Todas dan >10:1 contra la tinta
`#1E1A14`. El `#3A3A3A` plano del chrome pasa a `#2A261F` (tintado, como
el resto).

Cada cara lleva un borde de 1px del mismo hue a L-0.16
(`NoteColorPalette.Rims`, aplicado vía `NoteRimConverter`). Es la única
forma de dar volumen aquí: sin `AllowsTransparency` todo píxel dentro de la
región es opaco y no cabe ninguna sombra.

**No se migran las notas existentes.** Las guardadas con los hex viejos
siguen con su color: reasignarlo sería cambiar datos del usuario sin
pedírselo. `RimFor` les calcula el borde oscureciendo el color
proporcionalmente, así que no se ven planas al lado de las nuevas.

### Pendiente

Tests: 107/107. Build limpio. Estado de reposo verificado a nivel de píxel.
**Falta la verificación manual del usuario** — nada de esto prueba cómo se
siente la transición, que es justo la queja original. Checklist en la
sección de abajo.

## La pestaña como lomo de la nota (sesión 2026-09-04, segunda ronda)

Misma rama `dock-motion-shape`. Partió de que el usuario compartió un **vídeo
de pantalla de Hold My Notes** (no solo la landing) y preguntó tres cosas:
escalera o ancho uniforme, cómo debía abrirse la nota, y si con sombra.

### Lo que el vídeo enseña y la landing no

Se analizó extrayendo frames con `ffmpeg` (contact sheet + recortes del borde
derecho a 60fps). Tres hallazgos que contradicen la maqueta promocional:

1. **Las pestañas son todas del mismo ancho.** La escalera de anchos
   crecientes es de la landing, no de la app.
2. Cada pestaña lleva una **línea de troquelado punteada** cerca de su borde
   derecho, y la etiqueta va en **un tono oscuro de su propio hue** (no en
   negro), en mayúsculas y con tracking.
3. **Al abrir, la pestaña no desaparece: se convierte en el lomo de la nota**,
   y el conjunto se desliza a la izquierda saliéndose del mazo. El troquelado
   es el pliegue por donde el lomo se une al cuerpo.

De ahí sale el modelo mental que ahora documenta `EdgeGeometry`: **cada
pestaña ES su nota, con casi todo el cuerpo fuera de pantalla**. Y eso fuerza
el ancho uniforme — con una escalera, cada nota se abriría con un lomo de
grosor distinto.

### Decisiones del usuario

- Ancho: **uniforme**.
- Apertura: **deslizar con el lomo por delante**.
- Reposo: **pill fino tipo HMN** (24px por nota en vez de 80).
- Sombra: delegada. **Se decidió no activar `AllowsTransparency`.** Y se
  corrigió una premisa mal planteada en la propia pregunta: `NoteWindow` ya
  tiene sombra nativa real vía `GlassFrameThickness="-1"` sobre una ventana
  opaca, sin coste de ClearType. Lo único que no puede tener sombra es el
  dock, porque DWM la dibuja sobre el RECT completo y chocaría con la región.
  El reparto correcto es: la nota, que se levanta del mazo, lleva sombra; el
  mazo va a ras del canto y no la necesita.

### Cambios

- `EdgeGeometry.TabWidth` pasa a constante (104). `SpineWidth = TabWidth -
  PerforationInset` (88), y `RestSliverWidth == PerforationInset` para que en
  reposo el borde izquierdo del guión **sea** exactamente el troquelado.
- Reposo: `RestDashLength` 24 con paso 30, frente a los 80/88 del desplegado.
  Para 4 notas son ~114px contra 344. Las pestañas llegan a su hueco de reposo
  con un `RenderTransform` por pestaña (`RestOffsetFor`), **nunca por layout**,
  así que la garantía de ventana fija sigue en pie.
- `NoteWindow` gana una columna de lomo (color de la nota, misma etiqueta, mismo
  troquelado) y se abre animando **solo `Left`**, con ease-out quíntica. Alto,
  ancho y Top son definitivos desde el primer frame, así que su contenido
  tampoco se mide nunca a un tamaño intermedio. Sustituye a `AnimateFrom`, que
  animaba las cuatro propiedades y hacía "crecer" la nota desde un rect
  diminuto.
- La ventana pasa de 260 a 348 de ancho, para que el cuerpo conserve sus 260
  con el lomo de 88 delante.
- `AppCoordinator.IsNoteOpen` permite al dock ocultar la pestaña cuya nota está
  abierta (`Hidden`, no `Collapsed`, para que el mazo conserve el hueco vacío
  de donde se sacó la ficha). Sin esto la misma etiqueta saldría dos veces.
  `EdgeDockWindow.RefreshOpenState` hace solo eso, sin reconstruir la lista:
  pasar por `SetNotes` reiniciaría la animación de entrada para nada.
- `PositionNoteWindow` alinea la nota con la altura de **su propia pestaña**,
  que es de donde el usuario acaba de tirar. La cascada queda solo en
  horizontal.

### Verificación, y dos bugs que encontró

Tests: 127/127. Build limpio.

1. **Sondeando la región de la app real con 5 notas**: `RestStripLength`
   heredaba el tope de `MaxContentLength` (4 pestañas), pero ese tope existe
   porque el abanico desplegado hace scroll y **la tira de reposo no**. El
   quinto guión se dibujaba pero caía fuera de la zona sensible al ratón — se
   veía y no se podía pulsar. Arreglado; `RestStripStart` además se acota a 0
   para que una tira que desborde pierda las últimas notas y no las primeras.
2. **Rasterizando la plantilla de pestaña en aislamiento** (script WPF en
   PowerShell con `RenderTargetBitmap`, sin lanzar la app ni capturar
   pantalla): la etiqueta iba en el color del filete, que da **1.74:1** contra
   su cara. Diseñado para una línea de 1px, ilegible para texto de 11px en
   mayúsculas. Se añadió una escala aparte, `NoteColorPalette.Labels` (mismo
   hue a L-0.44, 5.3-5.6:1), con su `NoteLabelColorConverter`. El mismo render
   confirmó que el troquelado sale discontinuo de verdad: la primera versión
   usaba una `OpacityMask` en mosaico con `Stretch="None"`, que era una
   apuesta; se cambió a `Path` + `StrokeDashArray`, la receta estándar.

**Técnica que conviene reutilizar**: rasterizar una plantilla WPF en
aislamiento con `RenderTargetBitmap` desde PowerShell (`-STA`) verifica
render sin lanzar la app y sin capturar nada de la pantalla del usuario — no
tiene el problema de privacidad de la sesión 2026-09-03, y encontró dos cosas
que sondear la región no podía encontrar.

### Tercera ronda: texto cortado y reposo demasiado escondido

Feedback del usuario sobre capturas de la app real. Dos quejas, dos causas
distintas:

1. **"Se corta el texto"** — culpa de un tope duro de 9 caracteres que había
   puesto a ojo en `NoteTabLabelConverter`, y "NUEVA NOTA" (el título por
   defecto) tiene 10. Encima, con `TabHeight` en 80 tampoco cabría entera
   aunque no hubiera tope: el alto de la pestaña **es** el ancho disponible
   para la etiqueta girada. Fuera el tope (ahora recorta el propio `TextBlock`
   con elipsis y solo cuando de verdad no cabe) y `TabHeight` sube a 100.
   `TabPitch` pasa a 108 y `MaxContentLength` a 432.
2. **"No me gusta que quede así escondido"** — volviendo al vídeo se vio lo que
   me había dejado: HMN **no** tiene guiones sueltos, tiene un **contenedor
   oscuro** detrás que los agrupa y les da borde contra cualquier fondo. Sin
   él, cuatro pasteles claros sobre un escritorio claro desaparecen. Añadido
   como una pieza más de región (`RestContainerWidth`/`RestContainerInset`),
   con la tira despegada del canto en reposo (desplegada sigue a ras).

**Consecuencia técnica que no era obvia**: para que el fondo del contenedor
asome entre guiones, las pestañas no pueden solaparse en reposo — y con 100px
de alto y paso 32 se solapan 68. Hizo falta añadir una **escala vertical** por
pestaña (`RestScaleFor`) además del desplazamiento. Sigue siendo
`RenderTransform`, así que la garantía de "el layout se mide una vez a tamaño
final" no se toca. Como en reposo solo se ve el extremo derecho de la pestaña,
el aplastamiento de la etiqueta (que vive en el extremo izquierdo) no se ve.

También: los botones "+"/engranaje pasan a ser **dos círculos** en la región,
en vez de una caja rectangular que los envolvía — era lo único del dock con
esquinas en pico. `RegionPiece` gana `SquareRightSide` para distinguir las
pestañas (redondeadas solo por la izquierda, su lado derecho va a ras del
canto) de las pastillas y círculos completos.

### Segunda técnica de verificación: capturar solo el HWND propio

Además del render aislado, se usó `PrintWindow` con `PW_RENDERFULLCONTENT`
sobre **el HWND del dock y nada más**: pinta esa ventana en un DC propio, sin
leer el escritorio ni ninguna otra ventana, así que no es una captura de
pantalla y no reincide en el incidente de privacidad del 2026-09-03. Es lo
único que podía confirmar que los guiones se ven **separados** dentro del
contenedor, porque eso depende de que la escala vertical funcione y la región
por sí sola no lo dice (en reposo la región es una única pastilla; el color y
los huecos los pone el render de las pestañas que hay detrás).

Tests: 127/127.

### Cuarta ronda: la animación invadía el otro monitor, y el dock se aparta ante pantalla completa

**1. La apertura de una nota se dibujaba en el monitor de al lado.** `SlideInFrom`
arrancaba la ventana en la X de su pestaña con el cuerpo colgando por la
derecha, lo que da por hecho que a la derecha del dock no hay nada. Con varios
monitores es falso: el canto derecho de uno linda con el siguiente. En el
vertical del usuario (x -1440..0), una nota de 348px arrancando en x=-104 se
dibujaba de x=0 a 244 **encima del monitor principal**, donde había un juego.
`EdgeGeometry.SlideOriginFor` acota ahora el origen al área de trabajo del
dock. Efecto secundario bueno: tampoco quedan frames con media nota fuera de
pantalla ni con un solo monitor.

**2. Los guiones llenaban el contenedor de lado a lado**, sin el marco de fondo
que lo hace legible como objeto. El recorte horizontal se estaba dejando a la
región, pero **en reposo la región ES el contenedor**, así que una pestaña sin
recortar pinta sus 104px enteros por detrás. Cada pestaña lleva ahora su propio
`UIElement.Clip` (solo render, no toca layout) cuyo borde izquierdo barre con
la transición, y la región usa los mismos números para que no se
desincronicen. **Un `ScaleX` habría sido la solución obvia y es la mala**:
aplastaría también la etiqueta, y la etiqueta solo está oculta en reposo porque
vive en el extremo izquierdo de la pestaña. Añadido además
`RestContainerPad`, porque sin margen la curva de las tapas se comía el primer
y el último guión.

### El dock se aparta ante una aplicación a pantalla completa

Pedido por el usuario a raíz de lo anterior. El dock es `Topmost`, así que sin
esto se queda dibujado encima de un juego o un vídeo.

- `Aldune.Core.FullscreenDetection.CoversMonitor` (puro, con tests) compara la
  ventana contra el rectángulo **completo** del monitor, no contra su área de
  trabajo: así una ventana **maximizada** —que deja la barra de tareas a la
  vista— no cuenta. Es la distinción que importa; esconder el dock cada vez que
  alguien maximiza algo sería insufrible.
- `NativeMethods.IsFullscreenAppCovering` hace las tres exclusiones necesarias:
  ventanas del propio proceso (una nota no debe esconder su dock), el
  escritorio y la barra de tareas (`Progman`/`WorkerW`/`Shell_TrayWnd`, que
  tapan el monitor entero pero no son aplicaciones — y son justo lo que
  `GetForegroundWindow` devuelve cuando no hay nada delante, así que sin
  excluirlas el dock estaría escondido casi siempre), y las ventanas de otro
  monitor (comparando `HMONITOR`, no coordenadas).
- Sondeo propio a 500ms, aparte del de hover a 50ms: pasar a pantalla completa
  no hay que detectarlo en 50ms, y quien tiene un juego delante agradece que no
  le sondeen el primer plano 20 veces por segundo.
- Se colapsa **antes** de esconderse, y de golpe: si se escondiera desplegado
  volvería con el abanico abierto sin el ratón encima.
- `Visibility` en vez de `Hide()`/`Show()`: esas arrastran semántica de
  activación, y este dock es `WS_EX_NOACTIVATE` a propósito — no debe robar el
  foco al volver, y menos a un juego que acaba de salir de pantalla completa.
  Añadido también `ShowActivated="False"` en el XAML.

**Verificado end-to-end contra la app corriendo** (script en el scratchpad, no
versionado): creando una ventana sin bordes que cubre el monitor vertical, las
ventanas visibles de Aldune pasan de 1 a 0 y vuelven a 1 al cerrarla. Y con una
ventana 100px más corta que el monitor se queda en 1, que es el caso negativo
que de verdad hay que proteger.

Tests: 127/127.

### Quinta ronda: etiqueta cortada, solape que no ocurria, y hotplug de monitores

- **La etiqueta se salia del alto de la pestana.** No eran dos lineas (mal
  leido por mi en la primera captura): era una sola linea desbordando por
  abajo. Causa: el tracking con espacio fino (U+2009) alargaba la etiqueta un
  ~25%. Se pasa al espacio capilar (U+200A), que conserva el espaciado y entra
  con holgura, y el espacio de la frase pasa a duro (U+00A0) — era el unico
  sitio por donde podia partirse en dos columnas. `TextWrapping="NoWrap"`
  explicito en pestana y lomo. Comprobado renderizando cinco variantes en
  aislamiento.
- **El solape no ocurria.** El presupuesto estaba atado solo a una fraccion de
  pantalla, y el 80% de un monitor de 2560px son ~1900px: ocho notas cabian sin
  solaparse. Un monitor alto no significa que quieras un dock alto, asi que hay
  un tope absoluto (`MaxFanLength`) ademas de la fraccion. Ahora solapa desde la
  quinta nota y el abanico se queda en 480px entre 5 y 12; pasada la 13 manda
  `MinPitch` y el abanico se pasa de largo (y scrollea) antes que apretar las
  pestanas hasta que no se lea cual es cual.
- **Al apagar un monitor, su dock se duplicaba en el que quedaba.** Era el hueco
  documentado en "Prerrequisitos para la Fase 3" punto 4 (sin hotplug). Los
  docks se posicionan con coordenadas absolutas calculadas una vez, asi que
  cuando un monitor desaparece Windows reubica el dock huerfano sobre el otro y
  quedan dos apilados. Ahora `App` escucha
  `SystemEvents.DisplaySettingsChanged`, con un retardo reiniciable de 600ms
  (Windows dispara varios seguidos al reconfigurar), y reconstruye los docks
  contra la lista de monitores nueva. `AppCoordinator.CloseAllDocks` +
  `EdgeDockWindow.PrepareForClose`, que para los dos timers y el handler de
  `CompositionTarget.Rendering` — es un evento **estatico**, asi que un dock
  cerrado a media transicion seguiria llamando a `ApplyRegion` sobre un HWND
  destruido en cada frame, para siempre.
  Las ventanas de nota abiertas sobreviven: no guardan referencia a ningun dock.
- **Reordenado el arranque**: `BuildDocks` termina llamando a `RefreshAll`, que
  es donde de verdad se intenta descifrar por primera vez, asi que tiene que ir
  **dentro** del try/catch de `AuthenticationTagMismatchException` — si no, el
  caso (c) escaparia sin traducirse a su mensaje.

**Error de proceso que costo una ronda entera**: un `dotnet build` iba en el
mismo bloque de PowerShell que un heredoc `<<'EOF'`, que PowerShell no soporta.
El fallo es de *parseo*, asi que aborta el bloque entero antes de ejecutar nada
— la compilacion nunca corrio y luego se relanzo con `--no-build`. El usuario
estuvo evaluando el binario anterior a los arreglos. Para commits multilinea,
usar Bash; y comprobar la fecha del binario antes de dar por bueno un lanzamiento.

Tests: 130/130.

### Sexta ronda: transparencia, etiquetas horizontales, y adios a las regiones

Dos decisiones del usuario que se reforzaban entre si.

**1. Transparencia en el dock.** Las curvas salian escalonadas y no habia nada
que pulir: `SetWindowRgn` recorta con una mascara de **1 bit** — un pixel esta
dentro o fuera, sin medios tonos — asi que toda curva de la region salia
dentada. Peor en los botones circulares, que son curva pura. Es la misma raiz
por la que el dock no podia llevar sombra.

`EdgeDockWindow` pasa a `AllowsTransparency="True"`. El precio es perder
ClearType en **esa** ventana; se asume solo ahi, porque su texto son etiquetas
cortas de una linea, mientras que `NoteWindow` —donde de verdad se lee y se
escribe— sigue opaca y lo conserva.

A cambio **desaparece toda la maquinaria de region**: `SetWindowRgn` por frame,
el bucle de `CompositionTarget.Rendering`, el recorte al viewport, el clip por
pestana, `RegionPiece`, `TabRegionShape.BuildRegion` y ~150 lineas de interop
GDI. La forma la dibuja WPF con antialiasing, la animacion pasa a ser WPF
normal (dos capas con fundido cruzado + entrada escalonada por `BeginTime`), y
el dock gana sombras reales. `TabRegionShape` se queda solo con los tiempos y
se renombra a `FanTiming`.

**2. Etiquetas horizontales.** Con solape, la franja visible de cada pestana es
un paso: una etiqueta horizontal necesita ~18px de alto y una vertical ~90px,
asi que la vertical solo funcionaba sin solapar. Al pasar a horizontal la
pestana baja de 100 a 40px de alto y **caben ~10 notas sin solapar en vez de
4**, ademas de leerse el titulo entero siempre.

Consecuencia en la ventana de nota: el lomo vertical ya no encaja con una
pestana horizontal, asi que pasa a ser **cabecera** — la misma pestana que
estaba en el mazo, ahora como barra de titulo de la nota, con el troquelado
horizontal debajo separandola del cuerpo.

**Animacion al anadir nota**: `SetNotes` compara los ids con los de antes y
anima solo las pestanas nuevas, en vez de rehacer la entrada del abanico
entero — que hacia que crear una nota pareciera un refresco y no una insercion.

Tests: 104/104 (bajan de 130 porque desaparecieron los de region).

### Septima ronda: recorte, apilado, gestor y ubicacion

- **El titulo se cortaba** por aritmetica, no por tipografia. `WindowThickness`
  reserva UN `ShadowMargin` (el lado derecho va a ras del canto y no aloja
  sombra), pero el Grid raiz llevaba `Margin="18"` a los cuatro lados: 36px de
  226 dejaban 190 para una pestana de 208, y al ir alineada a la derecha perdia
  18px por la izquierda, justo donde vive el margen de la etiqueta. Margen
  asimetrico; verificado en la app: 226-18 = 208 clavados.
- **El mismo bug desplazaba el footer**: al alinearse a la derecha se paraba
  18px antes que las pestanas, y por eso los botones se veian corridos a la
  izquierda respecto al abanico.
- **Las pestanas solapadas se leian como un bloque macizo** porque la sombra
  apuntaba hacia abajo: se apilan hacia abajo y cada una tapa a la anterior, asi
  que la sombra caia justo donde la siguiente la ocultaba. Proyectada hacia
  arriba (`Direction=95`), cada una deja un canto oscuro sobre la de encima, que
  es como se ve un mazo escalonado. Elegido renderizando tres variantes juntas;
  la del canto claro parecia bisel. Los botones conservan sombra descendente,
  que no se apilan.
- **"Gestionar notas" salia siempre en el monitor principal**: no fijaba
  posicion y Windows la ponia en (0,0). Ahora `OpenOrActivateNotesManager` toma
  el dock que la pide y `CenterOnThisMonitor` la centra en su area de trabajo.
- **Barra de scroll del gestor**: fina y oscura. Matiz sobre una decision
  anterior — una ronda descarto barras personalizadas citando el anti-patron de
  "reinventar afordancias estandar". Esto no las reinventa, las **tine**:
  conserva arrastre, clic en la pista y rueda; solo cambia color y grosor.
- **Animaciones en el gestor**: las filas que dejan la vista actual salen
  deslizandose a la derecha antes de recargar, y cambiar de filtro entra
  apareciendo y subiendo. `RowsLeavingView` mantiene eso honesto: en el filtro
  "Todas" no se va ninguna, solo cambia su chip, asi que no se anima nada.
- **Apertura y cierre de nota revisados**: se anima `Left` y, ademas, el
  contenido entra con retraso y apareciendo — sin eso la ventana llegaba a plena
  opacidad de golpe y solo el rectangulo se movia. **No se anima
  `Window.Opacity`**: WPF la implementa con una ventana por capas, justo lo que
  esta ventana evita para conservar ClearType. Al cerrar, la nota vuelve al
  mazo; hay que cancelar el Close y repetirlo al terminar (WPF no permite
  aplazarlo), con un flag para cortar el bucle — `Flush` es idempotente, asi que
  correr en las dos pasadas no guarda dos veces.
- **La vista previa de la pestana** (segunda linea con el cuerpo de la nota)
  hace que la pestana diga que hay dentro y no solo como se llama. Se esconde
  entera por debajo de `PreviewVisiblePitch` en vez de quedar cortada a media
  linea, que parece un fallo de render.

Tests: 113/113.

### `ALDUNE_MONITOR_INDEX`

Variable de entorno que restringe la app a un monitor (índice 0-based sobre el
orden de `MonitorEnumerator`; valor inválido se ignora). Nació de una
necesidad real — poder probar sin invadir la pantalla donde el usuario estaba
jugando — y es la pieza mínima del punto 3 de "Prerrequisitos para la Fase 3".
Cuando ese punto se aborde de verdad, debería pasar a `AppSettings`.

### Petición del usuario de esa ronda — ya implementada

Que el dock se esconda ante una ventana a pantalla completa: hecho en la cuarta
ronda, ver su sección más arriba.

## Icono de bandeja, arranque con Windows e icono de la app (sesion 2026-09-05)

Cierra el agujero mas grande que quedaba de producto: **la app no tenia forma de
cerrarse ni de configurarse**. Se lanzaba a mano y se cerraba matando el
proceso.

- **`TrayIcon`** (`System.Windows.Forms.NotifyIcon`): nueva nota, gestionar
  notas, "Abrir al iniciar sesion" y salir. Doble clic abre el gestor. Menu con
  `ProfessionalColorTable` propia para que no desentone con el resto — el
  renderer por defecto de WinForms es gris claro y de otra epoca.
- **`UseWindowsForms` obliga a un ajuste**: mete `System.Windows.Forms` y
  `System.Drawing` en los global usings de TODO el proyecto, y ahi chocan con
  WPF (`Application`, `Button`, `Point`, `Color` existen en los dos mundos), asi
  que cada fichero empezaba a dar CS0104. Se sacan con `<Using Remove=...>` y
  solo `TrayIcon.cs` los pide explicitamente.
- **`ShutdownMode` pasa a `OnExplicitShutdown`**: con bandeja, cerrar la ultima
  nota no debe cerrar la app. Se sale por el menu, o por los `Shutdown(1)` de
  los fallos de arranque.
- **`StartupRegistration`**: clave `Run` de HKCU, no de maquina — no pide
  permisos de administrador, y es la que Windows enseña y deja desactivar en
  Administrador de tareas > Inicio, asi que siempre hay una segunda via para
  quitarlo. La ruta va entrecomillada: sin comillas, una ruta con espacios
  haria que Windows ejecutara el primer trozo y pasara el resto como argumentos.
  El menu refleja lo que de verdad quedo guardado, no lo que se pidio, por si el
  registro esta restringido por directiva.
- **Icono** (`src/Aldune/Assets/aldune.ico`, generado con un script de un solo
  uso): fichas de colores asomando por el canto derecho sobre el fondo tintado,
  que es literalmente lo que hace la app. Dos detalles del formato:
  - Los tamanos <=48px van en **BMP**, no PNG. Windows admite PNG dentro de .ico
    desde Vista, pero GDI+ tropieza con esas entradas (`Icon.ToBitmap` revienta
    con "Requested range extends past the end of the array") y el icono de
    bandeja pasa por ahi. 256px si va en PNG: en BMP ocuparia 256KB.
  - A 16px tres barras con sus huecos son papilla, asi que ese tamano tiene un
    dibujo propio de dos barras mas gruesas.

**Riesgo asumido**: con `OnExplicitShutdown`, si el icono de bandeja fallara al
crearse no habria forma de salir salvo el Administrador de tareas.

Tests: 113/113.

## Atajo configurable, estado vacío y portable (sesión 2026-09-05, segunda ronda)

Tres cosas que salieron de usar la app de verdad, no de mirarla.

### El atajo global tenía que ser configurable, no elegido por mí

`Ctrl+Alt+N` no hacía nada en la máquina del usuario: la tenía asignada a
"siguiente canción". Windows **no comparte una combinación entre aplicaciones**
— se la queda la primera que la pide —, así que no existe una combinación por
defecto que sea segura. Cualquiera que elija chocará con alguien.

- **`Aldune.Core.HotkeyBinding`** (record con modificadores y tecla virtual).
  Vive en Core, y no en la ventana de ajustes, por una razón concreta: el
  `DisplayName` que se enseña en pantalla tiene que salir de los mismos bits que
  se registran en Win32. Si la interfaz compusiera el texto por su cuenta podría
  anunciar una combinación distinta de la que de verdad está activa, y eso es un
  bug que nadie reporta porque parece cosa suya.
- **Se captura pulsando, no eligiendo de una lista.** El botón escucha la
  siguiente combinación (Esc cancela). Dos desplegables de "modificador" y
  "tecla" obligan a traducir mentalmente algo que uno ya sabe pulsar.
- **Se exige al menos un modificador**: sin él, el atajo se tragaría esa tecla en
  todo el sistema.
- Un modificador suelto no cierra la captura — mientras se mantiene Ctrl sin
  haber elegido tecla se sigue escuchando, en vez de registrar "Ctrl + Ctrl".
- El defecto pasa a `Ctrl+Shift+N`, y **la pista de la ventana dice la verdad**:
  si `RegisterHotKey` falló porque otra app ya la tenía, lo dice y pide otra.
- `AppSettings.GlobalHotkeyEnabled` nace en `true` a propósito, para que un
  `settings.json` ya existente se actualice con el atajo activo en vez de
  aparecer apagado sin que nadie lo apagara.

### Sin notas, el dock era invisible y no se podía hacer nada

Con la base de datos vacía no había tira de guiones (cero guiones), luego no
había nada donde pasar el ratón, luego no había forma de crear la primera nota.
La app quedaba muerta justo en el único momento en que todo el mundo la ve: al
estrenarla. Ahora, con cero notas, el dock **se muestra ya desplegado** y sin
tira en reposo — no hay nada que colapsar, y los botones "+" y engranaje quedan
a la vista.

También se corrigió el guión suelto descentrado dentro del óvalo: el `Padding`
vertical del contenedor y el `Margin` inferior de cada guión sumaban dos veces
por abajo. El contenedor solo pone el hueco de arriba.

### Portable: un `.exe` y nada más

`src/Aldune/Properties/PublishProfiles/portable.pubxml`, y se usa así:

    dotnet publish src/Aldune -p:PublishProfile=portable   →   publish/portable/aldune.exe

Decisiones que no son obvias leyendo el fichero:

- **Perfil, no propiedades en el `.csproj`.** `RuntimeIdentifier` también afecta
  a `dotnet build` y `dotnet run`, así que meterlo en el proyecto ralentizaría
  cada compilación de desarrollo por algo que solo importa al publicar.
- **Autocontenido** (~82 MB): un portable que antes exige instalar el runtime de
  .NET no es portable.
- **Sin recorte (`PublishTrimmed=false`)**: WPF usa reflexión por todas partes y
  el recortador se lleva tipos que hacen falta. Fallaría al abrir una ventana, no
  al compilar, que es la peor forma de fallar.
- **Dos propiedades para un solo `.pdb`**: `DebugType=none` silencia a Aldune,
  pero el `.pdb` de Aldune.Core llegaba por otra vía — se copia como *fichero
  acompañante* de la referencia a ese proyecto, y solo
  `AllowedReferenceRelatedFileExtensions` lo para.
- `publish/` va al `.gitignore`: se versiona el perfil, no sus 82 MB de
  resultado.

**Verificado contra el ejecutable publicado ya en marcha** (no contra el de
desarrollo):

- `ProcessPath` apunta al `.exe` real, no al directorio temporal de extracción
  del single-file. De eso depende el arranque con Windows: si apuntara al temporal,
  la clave del registro quedaría escrita hacia una ruta que desaparece.
- El atajo global queda registrado (pedirlo desde otro proceso es rechazado).
- `Icon.ExtractAssociatedIcon` sobre el single-file devuelve 32×32, así que el
  icono de bandeja carga.
- Un dock por monitor, los dos visibles. Un `visible=False` observado antes en el
  monitor primario era el ocultado por pantalla completa funcionando (había un
  juego delante), no una regresión.

Tests: 132/132.

## Buscar notas (sesión 2026-09-05, tercera ronda)

Primer punto de la lista de "lo siguiente" de la ronda anterior — la app ya se
puede usar a diario gracias al portable, y esto era lo primero que se echaría
en falta al crecer más allá de un puñado de notas.

- **`Aldune.Core.NoteSearch.Matches(texto, consulta)`**: en Core, no en la
  ventana, porque es la única pieza con lógica real que vale la pena cubrir con
  tests en vez de con clics — recorta espacios, una consulta vacía coincide con
  todo (así borrar la caja no necesita un camino aparte de "sin búsqueda"), y
  compara sin distinguir mayúsculas pero sí acentos (vía la cultura actual, no
  `OrdinalIgnoreCase`).
- **La búsqueda se queda dentro del filtro activo**, no lo sustituye por una
  vista mezclada de los tres estados. La razón es el botón "Eliminar" (borrado
  permanente): solo aparece bajo "Papelera", y una búsqueda que mezclara
  estados lo dejaría actuando sobre notas que no están ahí de verdad — ese
  botón existe justo para que no se pueda saltar la papelera por accidente. El
  texto escrito persiste al cambiar de pestaña, así que mirar el mismo término
  en otra categoría no obliga a reescribirlo.
- **El estado vacío distingue la causa**: "ninguna nota contiene «X»" cuando
  hay búsqueda, en vez de reciclar el mensaje de "no hay notas en este filtro"
  — ese mensaje sería mentira si de hecho sí hay notas y solo ninguna coincide.

**Verificado con una ventana renderizada de forma aislada** (un proyecto
descartable en el scratchpad, `RenderTargetBitmap` sobre el HWND de la propia
ventana, nunca una captura de pantalla) contra datos de prueba sembrados a
mano: coincide dentro de "Activas", se mantiene acotada al cambiar a
"Archivadas", y el mensaje de "sin resultados" nombra el término buscado.

Tests: 140/140.

## Prioridad del dock, arreglo de pantalla completa y selector de monitor en Ajustes (sesión 2026-09-06)

Resuelve la incidencia donde Aldune se iba al fondo o desaparecía al interactuar con aplicaciones maximizadas (p. ej. seleccionar pestañas en Chrome), mantiene la ocultación ante videojuegos en pantalla completa real, y traslada el control de monitor de la variable de entorno a la UI de Ajustes.

### Causa raíz del falso positivo de pantalla completa

- En configuraciones con barra de tareas auto-oculta o multimonitor donde la barra no resta espacio en pantalla, `Bounds == WorkingArea`.
- Una ventana maximizada estándar de Windows (como Chrome con pestañas) mide físicamente unos pocos píxeles más que el monitor (`X = -8, Y = -8, Width = W + 16, Height = H + 16` por los bordes de redimensionado invisibles de Win32).
- `FullscreenDetection.CoversMonitor` asumía que una ventana maximizada nunca cubría el monitor completo porque la barra de tareas lo impedía. Al ser `Bounds == WorkingArea`, la condición matemática se cumplía siempre que el usuario hacía clic en una pestaña de una ventana maximizada.
- `IsFullscreenAppCovering` concluía erróneamente que una ventana maximizada era un videojuego a pantalla completa, y `PollFullscreenApp` ponía el dock en `Visibility.Hidden`.

### Cambios realizados

1. **Distinción estricta de maximizado vs fullscreen**:
   - `NativeMethods` incorpora `IsZoomed(foreground)` y comprobación de `WS_MAXIMIZE` y `WS_CAPTION`. Si una ventana está maximizada por el SO o tiene barra de título/menú de aplicación tradicional, se excluye de inmediato de la detección de pantalla completa.
   - `FullscreenDetection.CoversMonitor` gana un parámetro opcional `isZoomed = false` para verificar que solo ventanas no maximizadas que cubren el monitor (videojuegos borderless o exclusivos, vídeos F11) cuentan como pantalla completa.
   - Tests añadidos en `FullscreenDetectionTests` (142/142 tests pasando).

2. **Prioridad Topmost reforzada**:
   - `NativeMethods.EnsureTopmost(hWnd)` reafirma `HWND_TOPMOST` con `SWP_NOACTIVATE | SWP_FRAMECHANGED` sin robar el foco.
   - `EdgeDockWindow` lo llama al inicializarse, al sobrevolar (`PollHoverState`) y al volver de un estado oculto, garantizando que el dock nunca quede tapado por ventanas estándar.

3. **Selector de pantalla y opciones en Ajustes**:
   - `AppSettings.TargetMonitorIndex`: índice opcional para fijar Aldune en un monitor concreto (`null` = todas las pantallas conectadas).
   - `AppSettings.HideOnFullscreen`: opción booleana para habilitar/deshabilitar la ocultación ante videojuegos (por defecto `true`).
   - `SettingsWindow.xaml`: nueva sección visual "Pantallas donde mostrar Aldune" con selector de tarjetas estilizadas (`MonitorRadioStyle`) generado dinámicamente con las pantallas conectadas (nombre, resolución, indicación de monitor principal), y casilla para el auto-ocultado ante videojuegos.
   - `AppCoordinator.RebuildDocks()`: reconstruye los docks en caliente al cambiar la selección en Ajustes sin necesidad de reiniciar la app.

Tests: 142/142 pasando.

### Segunda ronda: recordar posición de la nota, borde izquierdo, animación tras inactividad, ayuda rápida

Continuación de la misma sesión, tras verificación manual del usuario de lo anterior. Pidió además:
que las notas recuerden dónde se dejaron (sensación de post-it real), un selector de borde del
dock, investigar por qué la animación de abrir una nota se ve más brusca "cuando llevas un rato sin
abrir ninguna", y una revisión de ergonomía general.

1. **Las notas recuerdan su posición y tamaño** (`AppSettings.RememberNotePositions`, por defecto
   activado — el campo ya existía sin usar, junto con la tabla `NotePlacement` y
   `NotesRepository.SavePlacement/GetPlacement/DeletePlacement`, de una ronda anterior a un corte de
   contexto):
   - `NoteWindow.SavePlacementOnce` guarda `Left/Top/Width/Height` en el **primer** evento
     `Closing`, antes de que `OnClosingWithAnimation` cancele el cierre y anime la nota deslizándose
     fuera de la pantalla — si se guardara después, se persistiría la posición a medio deslizar, no
     la real. Un flag (`_placementSaved`) evita que la segunda pasada de `Closing` (cuando la
     animación termina y cierra de verdad) sobrescriba el valor bueno.
   - `AppCoordinator.TryRestorePlacement` decide al abrir: si hay una posición guardada y sigue
     siendo visible en algún monitor conectado ahora mismo (`PlacementValidation.IsVisibleOnMonitors`,
     contra la lista completa de monitores, no solo el del dock que pidió abrir la nota), la nota
     aparece ahí directamente, **sin** la animación de deslizarse desde su pestaña — así lo decidió
     el usuario explícitamente, para que se sienta como un post-it que sigue donde lo dejaste, no
     como una ventana que se abre desde el dock. Sin posición guardada, o si el monitor donde estaba
     ya no existe, se comporta como siempre.
   - Se guarda siempre al cerrar, la hayas movido tú o no: la primera vez que una nota se cierra, su
     sitio (aunque sea el de la cascada automática) queda fijado. Si el usuario prefiere que solo
     cuente cuando la arrastra de verdad, hay que distinguir ambos casos — no se hizo, decisión
     explícita por simplicidad.
   - Tests nuevos en `PlacementValidationTests` (no tenía ninguno; la lógica ya existía sin probar).

2. **Selector de borde del dock: Izquierda/Derecha** (`AppSettings.DockEdge`, por defecto
   `Right` — también existía sin usar). Arriba/Abajo queda fuera a propósito: la apertura de nota
   (`EdgeDockWindow.PositionNoteWindow`, `NoteWindow.SlideInFrom`) solo anima `Left` en horizontal, y
   generalizarla a vertical es un cambio real, no solo exponer un selector — se decidió acotar el
   alcance en vez de improvisarlo.
   - `EdgeGeometry.WindowRect`/`RestingVisibleRect` ya soportaban los 4 bordes (geometría del dock en
     sí); lo que faltaba era la apertura de nota. Con el dock a la izquierda, el canto interior de la
     pestaña (por donde sale la nota) es `Left(dock) + TabWidth` en vez de `Left(dock) + ShadowMargin`
     — el margen de sombra vive siempre en el lado interior, opuesto al canto físico de pantalla que
     esa pestaña toca (deducido por simetría con `RestingVisibleRect`, que ya trataba los dos lados
     así).
   - `EdgeGeometry.SlideOriginForLeftEdge` (nuevo, con tests): misma idea que `SlideOriginFor` pero
     en la dirección contraria — la nota sale hacia la derecha, así que el origen se acota entre el
     canto físico izquierdo y el destino final, nunca más allá.
   - Nueva sección "Lado de la pantalla" en Ajustes, mismo estilo de tarjetas que el selector de
     monitor, con reconstrucción en caliente de los docks al cambiar.
   - **Pendiente de verificación manual del usuario** — geometría nueva, hay que verla en pantalla
     con el dock a la izquierda de verdad.

3. **Hipótesis sobre la animación brusca tras inactividad**: Aldune vive casi siempre sin foco (nadie
   lo activa como una ventana normal salvo al abrir una nota), así que Windows puede clasificarlo
   como candidato a "power throttling" (EcoQoS) — reducir su prioridad/CPU tras un rato en segundo
   plano. El primer frame de una animación justo después de que el proceso se reactive puede salir
   con tirones porque el hilo de UI arranca con el reloj/prioridad aún reducidos — encaja con el
   patrón descrito ("se ve peor tras un rato sin abrir ninguna nota"). Mitigación aplicada:
   `NativeMethods.DisablePowerThrottling()` (`SetProcessInformation` con
   `PROCESS_POWER_THROTTLING_EXECUTION_SPEED` desactivado), llamada una vez en `App.OnStartup`. Sin
   coste ni efecto secundario conocido si el diagnóstico resultara no ser este. **Pendiente de
   verificación manual** — es un problema intermitente, solo se puede confirmar usando la app en el
   día a día.

4. **Pulido de ergonomía menor**:
   - `NoteWindow`: Esc cierra la nota (antes solo la X); el texto se autoguarda igual, así que no se
     pierde nada.
   - Nueva sección "Ayuda rápida" al final de Ajustes (mismo sitio que ya explicaba cada opción):
     gestos del dock, Esc, el atajo configurado (se actualiza solo si se cambia), y el icono de la
     bandeja — no había ninguna ayuda visible en la app hasta ahora, todo se descubría por accidente.
   - Valoración de ergonomía general (sin cambios de código): distribución de botones, papelera sin
     confirmación (mitigado por la papelera de 30 días) y arrastre libre se consideraron ya
     razonables; no se identificaron más ajustes claramente necesarios.

Tests: 150/150 pasando.

### Tercera ronda: animación de apertura consistente, y Ajustes/notas por atajo en la pantalla del cursor

Feedback del usuario tras usar lo de arriba un rato, antes incluso de la verificación manual
pendiente. Dos cambios más:

1. **Animación de abrir nota, unificada — se quita el deslizamiento desde la pestaña.** El usuario
   dudaba entre perfeccionarla, quitarla, o hacerla igual venga de donde venga; se le presentaron
   tres opciones (fundido+crecimiento consistente, deslizamiento siempre pero con origen inventado,
   o ninguna animación) y eligió la primera. Motivo real para preferirla, más allá del gusto: con
   posición recordada (ver ronda anterior) una nota reaparecida no tenía ninguna relación geométrica
   con el dock, así que "deslizar siempre" habría exigido inventar un origen arbitrario — y el
   deslizamiento largo por la pantalla era además un candidato más al tirón que motivó investigar
   `DisablePowerThrottling` en primer lugar.
   - `NoteWindow.SlideInFrom` (deslizaba `Left` + fundido/desplazamiento del contenido) sustituido
     por `NoteWindow.PlayOpenAnimation`: fundido + `ScaleTransform` del 95% al 100% con
     `RenderTransformOrigin` centrado, 180ms, siempre ya en la posición definitiva. Se llama siempre
     (antes `SlideInFrom` solo corría si `originRect` no era nulo, así que una nota creada por atajo
     global no tenía ninguna animación de apertura — ahora sí, la misma que todas).
   - `OnClosingWithAnimation` (deslizaba `Left+40` + fundido) sustituido por el mismo fundido +
     encogimiento al 95%, simétrico a la apertura, 140ms.
   - `EdgeDockWindow.PositionNoteWindow` deja de devolver un "origen de deslizamiento" (pasa de
     `double` a `void`) — solo calcula la posición final de cascada junto a la pestaña, que sigue
     haciendo falta cuando no hay posición recordada.
   - Código muerto retirado: `EdgeGeometry.SlideOriginFor`/`SlideOriginForLeftEdge` y sus 6 tests
     (3 preexistentes + 3 de la ronda anterior) — nada los llama ya.

2. **La posición recordada de una nota pasa a ser por pantalla, no global.** Reportado por el
   usuario antes de que el `Left`/`Right` de la ronda anterior llegara a probarse: si una nota se
   dejó en el monitor vertical y se abre desde el dock del horizontal, no debe "traerse" desde la
   vertical — cada pantalla tiene que recordar su propio sitio, igual que si hubiera dos monitores
   horizontales.
   - `NotePlacement` gana `MonitorKey` (el `MonitorInfo.DeviceName` del monitor, p. ej.
     `\\.\DISPLAY1`) como parte de su clave — la tabla `NotePlacement` pasa a tener clave primaria
     compuesta `(NoteId, MonitorKey)`. **Corrección sobre lo dicho al principio de esta sesión**: se
     asumió que la tabla nunca había llegado a usarse de verdad y que por tanto no hacía falta
     migración — falso, la sesión anterior ya la había creado en la base de datos real del usuario
     (con el esquema viejo, sin `MonitorKey`) al probar el guardado de posición, y
     `CREATE TABLE IF NOT EXISTS` no toca una tabla que ya existe: el primer intento de usar
     `SavePlacement`/`GetPlacement` con el esquema nuevo rompía con `SQLite Error 1: no such column:
     MonitorKey`. Arreglado en `NotesDatabase.DropOutdatedNotePlacementTable` (nuevo, TDD): si la
     tabla existe sin esa columna, se recrea entera antes del `CREATE TABLE IF NOT EXISTS` de
     siempre — seguro porque `NotePlacement` es caché de UI (dónde estaba una ventana), no contenido
     del usuario como `Note`; perder posiciones recordadas de antes de esta sesión no pierde
     ninguna nota.
   - `NotesRepository.SavePlacement`/`GetPlacement` ganan el parámetro `monitorKey`. `DeletePlacement`
     (y el `DELETE` en línea de `Delete`/`PurgeExpiredTrash`) siguen borrando por `NoteId` sin más:
     borrar una nota borra su recuerdo en **todas** las pantallas, no solo una.
   - `Aldune.Core.MonitorLookup.DeviceNameAt` (nuevo, con tests): dado un rectángulo, en qué monitor
     cae su centro — puro, sin Win32, para poder probarlo. Se usa en dos sitios distintos:
     - `NoteWindow.SavePlacementOnce` lo usa contra la posición **actual** de la nota al cerrarla
       (puede haberse arrastrado a otra pantalla desde que se abrió).
     - `AppCoordinator.TryRestorePlacement` en cambio busca por el monitor **del dock que pidió
       abrir la nota** (`EdgeDockWindow.MonitorKey`, nuevo — cada dock ya conocía su
       `WorkingArea` pero no guardaba el `DeviceName`), no por dónde vaya a caer la nota: hay que
       saber si hay un recuerdo para esa pantalla antes de decidir su posición, no después.
   - Tests nuevos: `MonitorLookupTests`, `NotesRepositoryPlacementTests` (la lógica de
     `SavePlacement`/`GetPlacement` no tenía ninguno hasta ahora, ronda incluida).

3. **Ajustes y "Gestionar notas" (desde la bandeja) se abren en la pantalla del cursor, no siempre
   en el primer monitor registrado.** Mismo bug de fondo que ya se corrigió para el selector de
   monitor: `AppCoordinator.OpenSettings`/`OpenNotesManager`/`CreateAndOpenNote` usaban siempre
   `_docks[0]` para centrar la ventana o decidir dónde cae la nota nueva por atajo — en
   multimonitor, si la bandeja o el atajo se usan estando en el segundo monitor, la ventana saltaba
   al primero igualmente.
   - `NativeMethods.MonitorFromCursor()`/`MonitorFromHwnd()` (nuevos): HMONITOR del cursor y de un
     HWND dado, vía `MonitorFromPoint`/`MonitorFromWindow`.
   - `EdgeDockWindow.IsOnMonitor(IntPtr)` (nuevo): si el dock vive en ese HMONITOR.
   - `AppCoordinator.DockNearCursor()` (nuevo): el dock del monitor donde está el cursor ahora
     mismo, con `_docks.FirstOrDefault()` como último recurso. Sustituye a `_docks[0]` en los tres
     sitios de arriba.

Tests: 156/156 pasando. **Pendiente de verificación manual del usuario**, igual que el resto de
esta sesión — el propio motivo de estos cambios fue feedback llegado antes de completar esa
verificación.

### Un arreglo más de la verificación manual: margen de la nota nueva

La nota nueva se abría pegada al canto del dock (captura del usuario: el borde de la nota tocaba
casi el panel desplegado). Herencia de cuando la nota tenía que arrancar ahí para "deslizarse hacia
fuera" — sin esa animación (ver más arriba), quedarse pegada ya no vendía nada, solo se veía
encimada. `EdgeDockWindow.NoteWindowGapFromDock` (nuevo, 24px) separa la posición de cascada por
defecto del canto del dock en los dos bordes (izquierda/derecha). Solo afecta a notas sin posición
recordada — una vez que el usuario mueve una nota y la cierra, su sitio guardado manda y este
margen deja de aplicar.

Tests: 156/156 pasando.

### Panel oscuro detrás de los botones del footer (gear/"+")

El usuario compartió una captura del estado sin notas: solo se ven dos círculos (ajustes y "+")
flotando sobre el escritorio, sin nada que los agrupe. Causa: es el único sitio del dock donde un
elemento no lleva el tratamiento de "panel oscuro redondeado" que sí llevan la tira de reposo y el
contenedor de guiones (`RestStrip`) — sin él, dos círculos sueltos desaparecen visualmente contra
la mitad de los fondos de escritorio posibles.

Se discutieron dos preguntas más antes del cambio, ambas resueltas sin tocar código:
- **¿Solo con hover, incluso sin notas?** No — con cero notas la tira de reposo no tiene guiones que
  mostrar, así que exigir además pasar el ratón dejaría la primera vez sin ninguna pista de que ahí
  hay un "+". Se mantiene siempre visible cuando no hay notas (decisión ya tomada en una ronda
  anterior, reconfirmada aquí).
- **¿Botón de cerrar la app en el dock?** No — ya existe "Salir" en el menú de la bandeja (el sitio
  estándar de Windows para esto), y añadirlo junto a "+"/ajustes en un panel tan compacto y de uso
  frecuente sería un riesgo real de clic accidental que mata toda la app.

Cambio: `Border` con el mismo `Background="#2A261F"` de `RestStrip` envolviendo `FooterPanel`,
`CornerRadius="27"` (pastilla, radio = mitad del alto real). `EdgeGeometry.FooterLength` sube de 64
a 78 para que el nuevo relleno (7px arriba/abajo) no le quite al botón mayor el aire que ya tenía
reservado para su sombra — los tests lo referencian simbólicamente (`EdgeGeometry.FooterLength`),
no como `64` literal, así que no hizo falta tocar ninguno.

Tests: 156/156 pasando.

## Tareas, panel de acciones, logo y limpieza visual (sesión 2026-09-06, cuarta ronda)

Partió de una investigación de mercado (resumida en **`docs/ROADMAP.md`**, que desde ahora guarda
todo lo aplazado y lo descartado con su razón — leerlo antes de proponer funcionalidades nuevas). El
hallazgo que la motiva: **el concepto de Aldune no existe en Windows**; las dos apps equivalentes
(Hold My Notes y noty) son solo macOS.

### Casillas de tarea (`Aldune.Core.TaskLines`, TDD)

Una línea que empieza por `"☐ "` o `"☒ "` es una tarea. **Son texto plano, no un control**: el cuerpo
de la nota es un `TextBox` plano a propósito (la spec v1 descartó el texto enriquecido), y como
prefijo de texto la casilla se cifra, se busca, se exporta y aparece en la pestaña del dock sin
código nuevo en ninguno de esos sitios. `TaskLines` es puro y probado (30 tests); `NoteWindow` solo
traduce gestos: clic en el glifo lo marca, `Ctrl+L` convierte la línea en tarea, Enter continúa la
lista y una tarea vacía + Enter la termina.

**El glifo marcado es `☒` (U+2612), no `☑` (U+2611).** No es capricho: rasterizando los dos en la
fuente real de la nota (`RenderTargetBitmap` en aislamiento, la técnica que ya documenta la sesión
del 2026-09-04) se ve que `☑` **no está en Segoe UI Variable Text** y cae en una fuente sustituta que
lo dibuja como un cuadrado negro macizo — más pesado que el `☐` fino, **más ancho** (el texto de la
tarea se desplazaba al marcarla) y el único negro puro de una ventana que evita el negro absoluto a
propósito. `☒` sale de la misma fuente que `☐`: mismo peso, misma anchura. `☑` se sigue **aceptando
al leer** (llega pegado desde otras apps), pero nunca se escribe.

La pestaña del dock enseña ahora el progreso: `NoteTitleHelper.GetTabPreview` antepone `"1/3 · "`
cuando la nota tiene tareas, y `GetPreview` se come los glifos (en una línea de ~26 caracteres,
repetir `☐` gasta el hueco en decir lo que el contador ya dice).

### El cuerpo de la nota, liberado

Los seis colores y los botones Archivar/Papelera estaban **siempre visibles** al pie de cada nota:
~90 px de una ventana de 320, **casi un tercio del alto**, ocupados de forma permanente por acciones
que se usan una vez cada mucho, restándoselo al texto. Ahora el cuerpo es solo texto y todo eso vive
en un panel que abre el botón `⋯` de la cabecera (color, «siempre encima», archivar, papelera).

- **«Siempre encima»** es nuevo: `NoteWindow` estaba clavado a `Topmost="True"`, así que una nota
  que dejabas abierta se quedaba sobre todo lo demás sin escapatoria. El interruptor es **por
  sesión**, no se guarda (haría falta una columna nueva en la tabla `Note`, la que sí tiene datos
  reales del usuario — ver `ROADMAP.md`).
- Mismo panel en el **clic derecho sobre una pestaña del dock**: color, abrir, archivar, papelera sin
  abrir la nota. Antes cambiar un color obligaba a abrirla, cambiarlo y cerrarla. Mientras ese menú
  está abierto, `PollHoverState` no colapsa el abanico: el menú cae fuera de la zona sensible y
  moverse hacia él contaría como salir.

### Limpieza visual y deduplicación

- **`SettingsWindow` podía crecer más que la pantalla** — `SizeToContent="Height"` con seis secciones
  más la ayuda: en un portátil con escalado, las últimas opciones quedaban fuera **sin scroll para
  alcanzarlas**. Ahora tiene `ScrollViewer` y `MaxHeight` calculado contra el área de trabajo real.
- La barra de scroll oscura y los estilos de las filas de menú **suben a `App.xaml`**: estaban
  definidos dentro de `NotesManagerWindow` y ya hacían falta en dos ventanas más.

### Logo

Se mantiene el concepto (tres pestañas de color cortadas por el canto derecho: el producto dibujado,
con la paleta real). Lo que falla es el tamaño pequeño: a 16 px — bandeja, barra de tareas, Alt+Tab,
donde más se ve — tres barras finas con sus huecos se empastan y el icono se lee como una lista
genérica. El `.ico` se regenera ahora por programa con **dos variantes**: tres barras para 48 px y
más, y **dos barras mucho más gruesas para 16/24/32**. El corte a ras del canto derecho se consigue
recortando el dibujo contra el propio cuadrado redondeado, que es lo que conserva la idea de
"ancladas al borde". Script en el scratchpad de la sesión; si hay que repetirlo, está descrito aquí.

### Ronda de feedback sobre lo anterior

Cuatro cosas, tres de ellas fallos reales encontrados usando la app:

1. **Bug: el abanico se desplegaba mal tras crear una nota.** Ver su propia sección más abajo — hizo
   falta instrumentar la app real para dar con la causa, y el primer arreglo no era el bueno.
2. **La pestaña de arriba se veía como un rectángulo de canto recto, sin sombra.** La sombra de las
   pestañas se proyecta hacia arriba a propósito (cada una sombrea a la de encima), pero la primera
   no tiene ninguna encima y su sombra caía fuera del contenido — y el `ScrollViewer` recorta a su
   viewport. Añadido `EdgeGeometry.TabShadowHeadroom` (14 px) como **margen de la lista**, no como
   `Padding` del `ScrollViewer`: el recorte ocurre justo en ese borde, así que como relleno la
   sombra se habría seguido perdiendo. El test que fija la composición de `WindowLength` detectó el
   cambio y se actualizó.
3. **"Siempre encima" no decía qué hacía** (el usuario preguntó literalmente qué era). Ahora el
   botón dice el estado actual y debajo lo explica en una línea. Y **el menú lleva ya "Convertir en
   tarea" con su atajo escrito al lado** (`Ctrl+L`): un atajo que solo está documentado en Ajustes
   no lo descubre nadie.
4. **Título duplicado — resuelto quitando el texto de la cabecera.** El título es la primera línea
   del cuerpo, que está dos centímetros más abajo, y es lo que enseña la pestaña del dock. La
   cabecera sigue leyéndose como la pestaña que viajó con la nota por su color y su troquelado. Se
   descartó hacerla editable (sería un campo de título de verdad: columna nueva en `Note`,
   migración, qué mostrar cuando está vacío — lo que la spec v1 evitó) y se descartó la vista previa
   al pasar el ratón; las dos razones, en `ROADMAP.md`.

### El bug de la entrada escalonada, diagnosticado con instrumentación

Merece sección propia porque **la primera hipótesis era plausible, encajaba con el historial del
proyecto, y era falsa** — y porque el registro que lo resolvió es reproducible.

**Síntoma** (afinado por el usuario en dos rondas): crear una nota → apartar el ratón → volver a
pasarlo. El abanico "hace la animación pero peor". Sigue mal en cada despliegue posterior **hasta que
se abre y se cierra una nota**, y entonces vuelve a ir bien.

**Primera hipótesis, descartada**: `PlayEntrance` no limpiaba sus animaciones (`FillBehavior.HoldEnd`)
y los `Opacity = …` posteriores eran no-ops silenciosos. Era un fallo real —este proyecto ya había
tropezado dos veces con `HoldEnd`— y se arregló, **pero no era la causa**: el usuario confirmó que
seguía pasando.

**Cómo se resolvió**: instrumentación temporal en `SetNotes`, `OnTabLoaded`, `ApplyState` y
`ReplayTabEntrance` volcando a `%LOCALAPPDATA%\Fanote\dock-debug.log`, y el usuario reproduciendo.
El registro dio la respuesta en dos líneas comparadas:

```
al CREAR una nota:   OnTabLoaded indice=0..6, expandido=True,  opacidad fijada a 1
al CERRAR una nota:  OnTabLoaded indice=0..6, expandido=False, opacidad fijada a 0
```

**Causa raíz**: `PlayEntrance` anima la opacidad de 0 a 1 **con `BeginTime`** (el escalonado). Durante
ese retardo la animación todavía no manda y WPF pinta el **valor base** de la propiedad. Al crear una
nota, `OnTabLoaded` corre con el abanico abierto y deja todas las pestañas con opacidad base **1**;
así que en el siguiente despliegue cada pestaña se veía entera desde el primer frame, **pegaba un
salto a invisible** al arrancar su animación, y solo entonces hacía el fundido: un parpadeo
escalonado en lugar de una entrada. Y no se corregía solo porque al colapsar nadie devuelve las
pestañas a 0 — solo lo hacía un `SetNotes` con el dock ya cerrado, que es exactamente lo que ocurre
al cerrar una nota. De ahí el "hasta que no abro y cierro una nota no vuelve a ir bien".

**Arreglo**: `PlayEntrance` fija ahora los valores de partida (`Opacity = 0`, `translate.X = from`)
antes de lanzar cada animación, en vez de dar por hecho que alguien los dejó bien. La animación es
autosuficiente y ya no depende del estado previo.

**Lección para la próxima**: con `BeginTime`, el valor base es lo que se ve durante el retardo —
fijarlo siempre explícitamente. Y ante un bug de estado en la UI, instrumentar antes que deducir: la
hipótesis "encaja con un fallo que ya tuvimos" costó una ronda entera.

### La última pestaña se cortaba, y el abanico dejaba de ser compacto

Dos fallos de geometría encontrados con ~18 notas de prueba.

1. **La última pestaña salía recortada por abajo.** El solape se consigue con un `Margin.Bottom`
   **negativo** en cada pestaña (paso 26 con pestañas de 52 → −26). La última también lo llevaba, y
   ahí no solapa con nada: solo hacía que el `StackPanel` se midiera 26px más corto de lo que esa
   pestaña ocupa de verdad, así que el `ScrollViewer` la recortaba justo por esa diferencia. Ahora la
   última no lleva margen (`OnTabLoaded`). Con pocas notas no se veía porque el paso natural (60) es
   mayor que el alto (52) y el margen sale positivo.
2. **La ventana crecía sin tope.** `MaxFanLength` existe para que el dock siga siendo compacto, pero
   `MinPitch` (el suelo de legibilidad) manda sobre el reparto de `PitchFor`, así que a partir de
   ~19 notas el abanico se pasaba del presupuesto y, como `WindowLength` se dimensionaba al abanico,
   **crecía la ventana** en vez de entrar a funcionar el scroll; con 40 notas habría ocupado casi
   toda la pantalla. Extraído `EdgeGeometry.FanBudget` (el presupuesto, ahora compartido por
   `PitchFor` y `WindowLength`) y añadido `VisibleStripLength`: lo que pasa del tope se alcanza con
   la rueda. El `ScrollViewer` pasa de `Hidden` a `Auto` para que haya alguna pista de que hay más.

**Efecto secundario que hubo que atender a la vez**: la tira de guiones en reposo dibuja uno por nota
y **no hace scroll**. Mientras la ventana crecía con las notas, eso quedaba disimulado; con tope, los
guiones sobrantes se habrían recortado contra el borde. Añadidos `RestDashCapacity` /
`VisibleRestDashes`, y `RestingVisibleRect` pasa a medir contra los guiones que **se dibujan de
verdad**, para que la zona sensible al ratón coincida con lo que se ve (ya hubo una vez un guion
visible que no respondía al ratón, ver más arriba).

Un test existente (`WindowLength_AlwaysLeavesRoomForTheFooter`) detectó el cambio de significado:
comparaba contra el abanico total y ahora tiene que comparar contra el visible. 6 tests nuevos.

Tests: 198/198. **Pendiente de verificación manual del usuario.**

## Reordenar el mazo arrastrando (sesión 2026-09-06, quinta ronda)

Último punto pendiente de la lista del usuario junto con la internacionalización.

### Cómo se guarda el orden

Tabla propia, `NoteOrder (NoteId TEXT PRIMARY KEY, Position REAL)` — **no** una columna en `Note`,
por lo mismo que `NotePlacement`: esa tabla tiene el contenido real y no hay migraciones.

`Position` es `REAL` y no un índice entero **a propósito**: mover una nota entre otras dos es
escribir **una sola fila** (el punto medio de sus vecinas) en vez de renumerar la lista entera en
cada arrastre. La lógica vive en `Aldune.Core.NoteOrdering` (pura, 16 tests).

Dos casos que hay que cubrir sí o sí, y están cubiertos:

- **Notas sin orden todavía** (las de antes de que esto existiera): `GetByState` ordena por
  `COALESCE(o.Position, 1e18), CreatedAt`, así que conviven sin numerar nada por adelantado y una
  nota nueva aparece al final, que es donde se la espera. La primera vez que se arrastra,
  `MoveNote` numera la lista entera de una vez.
- **El hueco se agota.** Partir un intervalo por la mitad muchas veces seguidas en el mismo sitio
  acaba topando con la precisión del `double`. `NoteOrdering.Between` devuelve `null` ahí y
  `MoveNote` renumera y reintenta — sin eso, a partir de cierto momento arrastrar dejaría de hacer
  nada en silencio. Hay un test que hace 60 movimientos al mismo hueco y comprueba que no se pierde
  ni se duplica ninguna nota.

### El gesto

- Umbral de arrastre: el del sistema (`SystemParameters.MinimumVerticalDragDistance`), no uno
  inventado — por debajo de eso Windows lo considera un clic, y mucha gente mueve el ratón un par de
  píxeles al pulsar.
- **Solo se mueve la pestaña arrastrada**; las demás no se apartan en vivo. Con el solape del
  abanico, animar huecos exigiría recolocarlas todas en cada frame, y el orden real no se conoce
  hasta soltar. Al soltar, la lista se refresca ya ordenada.
- Dos interferencias que había que desactivar durante el arrastre: el `Click` del botón (soltar tras
  arrastrar habría abierto además la nota — bandera `_suppressNextClick`) y el sondeo de hover, que
  habría colapsado el abanico al salirse el gesto de la zona sensible.

Tests: 223/223. **Pendiente de verificación manual del usuario.**

## El título, resuelto: la cabecera edita la primera línea (sesión 2026-09-07)

El usuario dijo que quitar el texto de la cabecera **no le convencía**, así que se maquetaron las
cuatro alternativas y se renderizaron al lado, en vez de discutirlas. Al hacerlo apareció el
argumento que faltaba y que descartó la opción "campo de título aparte": **el texto de la nota se
cifra en un solo bloque**; un título guardado por separado tendría que cifrarse por su cuenta (blob,
nonce y tag propios) o quedarse en claro, filtrando justo lo más descriptivo de cada nota. Y meterlo
dentro del mismo bloque cifrado es, literalmente, "la primera línea".

De ahí salió una quinta opción que no estaba sobre la mesa y es la que se hizo: **la cabecera muestra
y edita la primera línea; el cuerpo empieza en la segunda.**

- `Aldune.Core.NoteText.Split`/`Join` (puro, 16 tests, incluida la ida y vuelta y un test que
  comprueba que el título de la cabecera coincide con el que enseña la pestaña del dock).
  `Join` no añade salto de línea con el cuerpo vacío: si no, una nota de una línea acumularía uno
  nuevo en cada apertura.
- **Nada cambia en cómo se guarda**: la nota sigue siendo un texto único que se cifra de una pieza.
- El cursor cruza entre los dos cuadros: Enter y Abajo bajan al cuerpo, Arriba desde la primera línea
  del cuerpo sube al título. **No** se implementa unir con Retroceso — exige fusión de líneas y a
  medias se siente roto; al principio de un cuadro de texto, que Retroceso no haga nada es lo normal.
- Una nota vacía abre el foco en el título; una que ya tiene texto, al final del cuerpo.
- **`WindowChrome.CaptionHeight` sube de 22 a 40** (el alto de la cabecera). El cuadro del título se
  comía la mitad de la franja de arrastre, y arrastrar es como se coloca una nota — más ahora que
  recuerdan su sitio. Con la franja completa se arrastra por el hueco alrededor del título, que queda
  excluido vía `IsHitTestVisibleInChrome`, igual que la barra de pestañas de un navegador.

### También: el arrastre del mazo se movía con demasiado poco

Reportado nada más probarlo. El destino se redondeaba al hueco más cercano, y como las pestañas se
solapan el paso es de 26px con pestañas de 52: moverla 13px ya la recolocaba. Correcto sobre el papel
y desagradable en la mano. Añadida `NoteOrdering.SlotHysteresis` (0.75): hay que arrastrar tres
cuartos de hueco para que cambie de sitio, y el destino se calcula desde el índice que ocupaba más el
desplazamiento, no desde la posición absoluta — así no depende del origen de la lista ni del scroll.

### Dos ajustes inmediatos al probarlo

1. **El marcador "Título" se quedaba pintado detrás del título escrito.** `TitlePlaceholderStyle`
   usaba `BasedOn` sobre `PlaceholderStyle`, y **heredar un estilo hereda también sus disparadores**:
   el marcador del título se hacía visible cuando el **cuerpo** estaba vacío. Ahora es un estilo
   suelto con su propio disparador. *Cuidado con `BasedOn` cuando el estilo base lleva triggers.*
2. **Tipografía del título.** Decidido con render comparativo (8 fuentes de Windows, título a tamaño
   real sobre el color de la nota). El cuerpo **se queda en la fuente de sistema** y el título pasa a
   **Ink Free** (manuscrita, con respaldo a la de sistema).

   El motivo de no llevar la manuscrita al cuerpo lo enseñó el render y no se deduce razonando: las
   fuentes manuscritas **no tienen los glifos `☐`/`☒`**, así que Windows los saca de otra fuente y
   vuelven a verse desalineados — justo lo que se arregló eligiendo `☒` por sus métricas. Además
   ocupan más alto y caben menos líneas. El título es una línea y no lleva casillas: ahí la
   personalidad sale gratis.

   **Descartado un selector de fuentes.** Según la búsqueda, en las apps de notas de Windows lo que
   la gente pide de verdad es **tamaño** (legibilidad y accesibilidad), no familia; y un selector de
   familia reintroduciría el desalineado de las casillas. Si se retoma, que sea de tamaño. Ver
   `ROADMAP.md`.

Tests: 247/247. **Pendiente de verificación manual del usuario.**

## Internacionalización a inglés (sesión 2026-09-07, Sonnet)

Último pendiente de la lista original de esta sesión. Cambio de modelo explícito del usuario: de
Opus (las rondas anteriores, con criterio de diseño real) a Sonnet para este, que es sustituir
cadenas en ~12 ficheros — trabajo mecánico, no de razonar.

### La decisión antes del código: ¿inglés sin más, o selector?

El propio usuario usa Aldune en español a diario. Traducir todo a inglés sin más se lo habría
quitado. Se preguntó explícitamente y se eligió: **selector Español/Inglés en Ajustes**, con
resolución en tres pasos —

1. `AppSettings.Language` (`"es"`/`"en"`/`null`). `null` = "sigue el idioma de Windows", y sigue
   así mientras el usuario nunca elija uno a mano: si `Language` es `null`, no se fija nada en el
   fichero, así que la app reacciona sola si el idioma de Windows cambiara entre arranques.
2. Elegir un idioma en Ajustes lo fija de forma explícita y permanente — mismo patrón que
   `TargetMonitorIndex`/`DockEdge`.
3. `App.OnStartup` resuelve y fija `Aldune.Resources.Strings.Current` **antes** de construir
   cualquier ventana. Se llama dos veces: una nada más entrar (adivinando por el idioma de Windows,
   por si el arranque falla antes de leer los ajustes de verdad — así hasta los mensajes de error
   más tempranos salen en el idioma que toca la mayoría de las veces) y otra en cuanto
   `settings.Language` está disponible de verdad.

**El cambio de idioma exige reiniciar Aldune para verse en todas las ventanas**, decisión explícita
y documentada en el propio texto de Ajustes: los enlaces `{x:Static}` de WPF se resuelven al
construir cada ventana, no cuando cambia una propiedad después. Reconstruir en caliente todas las
ventanas abiertas —incluidas notas con texto sin guardar— para simular un cambio en vivo habría sido
más frágil que pedir un reinicio, así que no se intentó.

### Por qué diccionario a mano y no .resx

Se decidió explícitamente **no** usar el mecanismo estándar de recursos de .NET (`.resx` +
ensamblados satélite por cultura). Dos motivos:

1. La generación de código de un `.resx` (`Strings.Designer.cs`) depende de herramientas de Visual
   Studio no garantizadas en cualquier máquina donde esto se compile con `dotnet build` a secas.
2. Con dos idiomas y un fichero satélite por cada uno, el error más común es traducir uno y
   olvidarse del otro. `Aldune.Resources.Strings` (nuevo) es una clase con una propiedad estática
   por texto y **las dos versiones en la misma línea** (`T("English", "Español")`), así que no hay
   dos ficheros que se puedan desincronizar.

Encaja además con el estilo ya establecido del proyecto (P/Invoke a mano en vez de paquetes,
ensamblado del `.ico` por código en vez de herramientas externas): menos piezas moviéndose, más
control.

### El único hueco que queda a propósito

`Aldune.Core.HotkeyBinding.DisplayName` (el nombre del atajo, "Ctrl + Shift + N") se queda **sin
traducir**: "Ctrl"/"Alt"/"Shift"/"Win" y las letras/números ya son universales, pero "Espacio",
"Supr" y "sin asignar" seguirán en español aunque la interfaz esté en inglés. Vive en Core, que es
la capa deliberadamente libre de Win32 *y* de idiomas, y sus tests (`HotkeyBindingTests`) fijan esos
tres textos literalmente. Cambiarlo exigía convertir una propiedad en un método parametrizado y
tocar esos tests por tres palabras que casi nunca se ven (el atajo por defecto no usa ninguna, y
hace falta rebindear a Espacio/Supr o dejarlo sin asignar para que aparezcan). Se dejó así a
propósito en vez de tocarlo de pasada; anotado por si se retoma.

### `NoteTitleHelper.PlaceholderTitle`, de `const` a mutable

Es Core, así que no sabe de idiomas — pero es lo que ven la pestaña del dock y la barra de tareas
para una nota vacía. Pasó de `const string = "Nueva nota"` a una propiedad mutable con valor por
defecto en inglés (`"New note"`), y `App.OnStartup` la fija a `Strings.NewNotePlaceholder` al
resolver el idioma. No rompió ningún test: `NoteTitleHelperTests` ya comparaba contra
`NoteTitleHelper.PlaceholderTitle` simbólicamente, no contra un literal.

Tests: 247/247 (sin cambios — nada de esto tenía lógica nueva que probar, es cableado). Verificado
que la app arranca sin excepciones con `Language` en `"en"`, `"es"` y ausente (sigue Windows).
**Pendiente de verificación visual del usuario** — es la primera vez que se ve la interfaz en
inglés de verdad.

## Ajustes a dos columnas (sesión 2026-09-07)

El usuario probó la interfaz en inglés y compartió una captura: con siete secciones más la ayuda
rápida, la ventana de una sola columna (420px) se salía por abajo en su pantalla — justo el punto
#1 que había quedado pendiente en la revisión de apariencia de una ronda anterior ("SettingsWindow
puede crecer más que la pantalla", ver `docs/ROADMAP.md`).

**Verificado con una maqueta antes de tocar el XAML real** (misma técnica que las comparaciones de
fuentes/título/icono): renderizando el contenido real a dos columnas de 760px de ancho, la altura
baja de ~1050px a ~610px — la mitad. Aprobado por el usuario antes de implementar.

- **Columna izquierda** ("cómo se usa la app en general"): Inicio con Windows, atajo de teclado,
  idioma.
- **Columna derecha** ("dónde y cómo vive el dock"): pantallas, borde del dock, ocultar ante
  pantalla completa, recordar posición de las notas.
- **Ayuda rápida** se queda a todo el ancho abajo, fuera de las columnas: es texto de referencia
  largo, se lee peor partido en una columna estrecha que en una franja ancha.
- El `ScrollViewer`/`MaxHeight` de la ronda anterior se mantienen como red de seguridad para
  pantallas muy pequeñas o muy escaladas, aunque con dos columnas ya no deberían hacer falta en el
  caso normal.

**Se preguntó también** si Ajustes y "Gestionar notas" debían recordar una posición fija en vez de
centrarse en el monitor del cursor cada vez que se abren (comportamiento actual, sin cambios): el
usuario prefirió dejarlo como está — centrado es predecible y nunca deja la ventana fuera de
pantalla si cambia la configuración de monitores, que sí sería un riesgo real si se persistiera una
posición exacta como hacen las notas.

Tests: 247/247 (sin cambios en lógica, solo XAML). **Pendiente de verificación visual del usuario.**

## Cómo seguir desde aquí

**Todo lo anterior está ya integrado**; los cambios compilan con 0 advertencias y 0 errores.

El diseño actual, en una frase: **la ventana del dock no cambia de tamaño
nunca**, es transparente, y su contenido son dos capas que se cruzan con
fundido — la tira de guiones en reposo y el abanico de pestañas horizontales
desplegado. No queda nada de la maquinaria de regiones.

Checklist manual antes de dar la rama por buena:

1. En reposo se ve una tira corta y oscura, despegada del canto, con un guión
   de color por nota. Todos responden al ratón, **incluido el último**.
2. Al pasar el ratón, las pestañas entran escalonadas deslizándose desde el
   canto; la última asienta en ~350ms como mucho.
3. Las curvas se ven **suaves**, sin escalones de píxeles: las esquinas de las
   pestañas, las tapas de la tira y sobre todo los círculos de "+" y engranaje.
4. Cada pestaña muestra su título entero y, debajo, las primeras palabras del
   cuerpo. Una nota sin cuerpo centra su título en vez de dejar un renglón
   vacío.
5. Con 8 notas o más las pestañas se solapan y el abanico deja de crecer. Se
   leen como fichas apiladas, no como un bloque: cada una tiene un canto oscuro
   sobre la anterior.
6. Al solaparse mucho, la vista previa desaparece entera (no cortada a media
   línea) y el título sigue leyéndose.
7. Los botones "+" y engranaje quedan alineados a la derecha con las pestañas,
   sin pegarse al canto, y el "+" pesa más que el engranaje.
8. Al hacer clic, la nota sale deslizándose con su cabecera por delante, y su
   pestaña desaparece del mazo dejando el hueco. Al cerrarla vuelve hacia el
   mazo y la pestaña reaparece.
9. **La animación no se dibuja nunca en el otro monitor.**
10. Crear una nota anima solo esa pestaña, no rehace el abanico entero.
11. "Gestionar notas" se abre **en el monitor desde el que pulsaste**, con su
    barra de scroll fina y oscura. Sin selección, las tres acciones están
    deshabilitadas. Cada filtro vacío dice algo útil.
12. Archivar o enviar a la papelera desde un filtro concreto anima la fila
    saliendo; desde "Todas" no, porque ahí la nota no se va a ninguna parte.
13. Con un juego o vídeo a pantalla completa delante, el dock desaparece de ese
    monitor y vuelve al salir. Con una ventana solo **maximizada** sigue
    viéndose.
14. Apagar un monitor con la app abierta deja **un solo dock**, no dos apilados
    en el que queda; al encenderlo vuelve el segundo.

Riesgo específico de esta rama, sin verificar: el dock pasó a
`AllowsTransparency`, lo que cambia cómo compone WPF. Si aparece parpadeo o
lentitud al desplegar, es nuevo y viene de ahí.

### Lo siguiente, por orden y con el porqué

1. **Inglés y selector de idioma** (pedido explícitamente). Sigue siendo lo
   siguiente natural: ~60 cadenas en cinco ventanas más el menú de bandeja.
   Plan: `.resx`, selector en Ajustes, e idioma del sistema como valor inicial.
2. **`Ctrl+F` para enfocar la búsqueda** dentro de "Gestionar notas" (la
   búsqueda en sí ya existe — ver sección de arriba — esto es solo el atajo de
   teclado para llegar a la caja sin usar el ratón).
3. **Más atajos**: desplegar/ocultar el abanico, `Esc` para cerrar la nota
   activa.
4. **Instalador**. Ojo: sin certificado de firma de código (de pago) SmartScreen
   avisará igual, así que el instalador no quita esa fricción, solo la mueve.
5. Sub-entrega 2 de la Fase 3 (ver prerrequisitos arriba): toggle de
   Ajustes para monitor único, IDs estables de dispositivo, hotplug en
   caliente — probablemente necesita su propio brainstorming (algunas
   piezas, como IDs estables, tocan el modelo de datos).
6. Modo "Papel vintage" (ver spec v1).

**Descartado, no pendiente**: gestos de trackpad. Windows no expone gestos de
panel táctil a las aplicaciones como tales; el atajo de teclado es la respuesta
realista a esa petición.

Si arrancas esto en una sesión/IA nueva: lee este archivo, la spec, y el plan
de la última fase fusionada, y sigue el mismo flujo de skills descrito arriba
(brainstorming → writing-plans → subagent-driven-development) para lo que sea
que decidas hacer a continuación.

## Borrar tareas completadas solas (sesión 2026-09-09, bounded)

Idea del usuario, brainstorming corto en el chat (sin spec/plan formal — extensión acotada sobre
`Aldune.Core.TaskLines`, que ya existía). Ajuste nuevo en Ajustes, **desactivado por defecto**: al
activarlo, una tarea marcada como hecha (☒) se borra sola de la nota pasado un plazo configurable.

### Decisiones tomadas en el brainstorming

- **Es un borrado de verdad, no un ocultar visual.** El cuerpo de la nota es un `TextBox` plano
  atado directamente al texto real (ver `Aldune.Core.TaskLines`) — mostrar algo distinto de lo que
  hay guardado exigiría el texto enriquecido que la spec v1 ya descartó a propósito. Al vencer el
  plazo, la línea se quita del texto de la nota, como si el usuario la hubiera borrado él mismo.
- **Desactivado por defecto.** Es una edición automática del texto de la nota: nadie la sufre sin
  haberla pedido explícitamente en Ajustes.
- **Plazo configurable de verdad, no solo unos preajustados.** Número + unidad (minutos / horas /
  días / semanas), no un desplegable fijo de "1 día / 1 semana" — el usuario lo pidió explícitamente
  tras ver la primera propuesta de tres opciones fijas.

### Cómo se guarda cuándo se marcó cada tarea

Tabla nueva, `TaskCompletion (NoteId, LineHash, CompletedAt)` — aparte de `Note`, mismo motivo que
`NoteOrder`/`NotePlacement`: esa tabla tiene el contenido real del usuario y esta app no tiene
sistema de migraciones.

Cada tarea se identifica por un **hash de su contenido** (`Aldune.Core.TaskCompletion.HashLine`,
SHA-256 del texto sin el glifo), no por su posición en la nota: la posición cambia con cualquier
edición alrededor, y el hash sigue apuntando a la misma tarea aunque la nota crezca o encoja por
otro sitio. El hash se calcula sin el glifo a propósito, así que desmarcar y volver a marcar la
misma tarea más tarde reinicia el reloj en vez de arrastrar el momento en que se marcó la primera
vez. Dos tareas con texto idéntico en la misma nota comparten hash y por tanto también el reloj —
igual que `NoteOrdering` acepta posiciones duplicadas, se acepta aquí por la misma razón: colisión
rara y sin consecuencia grave.

`Aldune.Core.TaskCompletion.Prune` (puro, TDD) recibe el texto, los `CompletedAt` conocidos, la
hora actual y el plazo, y devuelve el texto sin las líneas vencidas más los hashes que ya no
corresponden a ninguna tarea marcada (editada, desmarcada o borrada a mano por otro camino) para
que el llamante los limpie — así un registro huérfano no se queda para siempre sin necesitar un
barrido aparte.

**Bug real encontrado por los propios tests, antes de tocar la app real**: un `string.Join('\n',
kept)` ingenuo para reconstruir el texto deja un `\r` colgando al borrar la **última** línea de una
nota en CRLF (`"algo\r\n☒ hecho"` se quedaba en `"algo\r"` en vez de `"algo"`) — el `\r` de la
primera línea pertenece a su propio separador, no a la línea borrada. Arreglado en
`TaskCompletion.JoinLines`, con test de regresión.

### Cuándo se aplica de verdad

Dos disparadores, mismo espíritu que `NotesRepository.PurgeExpiredTrash` (barrido barato, no un
reloj en tiempo real):

1. **Al arrancar la app** (`App.xaml.cs`, dentro del mismo `try` que `BuildDocks` — `GetByState` ya
   descifra, así que un fallo de clave tiene que traducirse al mismo mensaje que el resto del
   arranque): recorre todas las notas activas.
2. **Al abrir una nota concreta** (`NoteWindow`, evento `Loaded`): cubre el hueco de una nota que
   lleva cerrada más que el plazo pero la app sigue corriendo desde antes.

Además, en cada ciclo del autoguardado de una nota abierta (barato, reaprovecha un temporizador que
ya existe) — **no cubre** dejar la nota abierta sin tocarla durante todo el plazo, porque el
autoguardado no se dispara sin editar. Aceptado como limitación conocida, igual que la purga de
papelera solo corre al arrancar: el ajuste por defecto es "1 día", así que el caso real (una nota
que se deja abierta sin editar más de un día seguido) es raro.

### Ajustes

Nueva sección en la columna izquierda (junto a Idioma): casilla "Borrar automáticamente las tareas
completadas" +, debajo, un campo numérico y una fila de chips (Minutos/Horas/Días/Semanas) para el
plazo — mismo patrón visual que el resto de Ajustes (chips como en el filtro de "Gestionar notas",
tarjetas de radio para el resto). Nada de `ComboBox`: habría introducido un control nuevo sin
estilo oscuro ya hecho en la app, cuando los chips ya resuelven lo mismo reutilizando lo que existe.

Tests: 271/271. Build limpio. Portable republicado y relanzado sin errores. **Pendiente de
verificación manual del usuario** (marcar una tarea, activar el ajuste con un plazo corto, y
comprobar que desaparece sola).

## Tres retoques sueltos de la nota y Ajustes (sesión 2026-09-09, bounded, sobre la marcha)

Pedidos directamente en el chat mientras se discutía sincronización, sin brainstorming formal por
ser acotados sobre UI ya existente:

- **Asa de arrastre en la cabecera de la nota.** El usuario reportó que no se notaba dónde se podía
  pinchar para mover la ventana (la cabecera entera ya era arrastrable vía
  `WindowChrome.CaptionHeight`, pero sin ninguna pista visual). Añadido un glifo de "agarre"
  (`&#xE76F;`, Segoe Fluent Icons/MDL2) a la izquierda de la cabecera, **sin** `IsHitTestVisibleInChrome`
  — sigue siendo zona de arrastre, el icono es puramente indicativo. `TitleBox` se corrió de
  `Margin="16,..."` a `"32,..."` para dejarle sitio.
- **Casillas de tarea: hover visible + zona de clic más generosa.** Antes solo cambiaba a cursor de
  texto normal al pasar por encima de una casilla, indistinguible del resto de la nota. Como el
  cuerpo es un `TextBox` plano (texto real, no controles — ver `Aldune.Core.TaskLines`), resaltar
  "solo la casilla" no es gratis: se añadió un `Canvas` `IsHitTestVisible="False"` superpuesto al
  `TextBox` (`TaskHoverHighlight`, un `Border` que seguimos posicionando por código con
  `GetRectFromCharacterIndex`) que seguimos con el ratón. Aprovechado el mismo cambio para dos cosas
  más: la zona de **clic** para marcar ahora cubre la indentación + el glifo + su espacio (no solo el
  carácter exacto del glifo) — el usuario pidió poder marcarla sin acertar a pixel —, y el cursor
  cambia a mano dentro de esa zona. `TaskLines.LineStart` pasó de privado a público para que
  `NoteWindow` pueda mapear la casilla de una línea a su posición absoluta en el texto completo. La
  lógica de "encontrar la casilla bajo un punto" vive en un solo sitio (`NoteWindow.FindCheckboxZoneAt`)
  y la usan tanto el clic como el hover, para no duplicar el cálculo.
- **Animación al abrir Ajustes.** Mismo fundido + crecimiento desde el 95% que ya usa
  `NoteWindow.PlayOpenAnimation` (180ms, `QuinticEase`), copiado tal cual a `SettingsWindow` y
  disparado desde `AppCoordinator.OpenSettings` justo después de `Show()` — mismo patrón que ya
  sigue `AppCoordinator.OpenOrActivateNote` para las notas.

Tests: 271/271 (sin cambios de lógica en Core salvo la visibilidad de `LineStart`). Build limpio.
**Pendiente de verificación manual del usuario** — hover/clic de casillas y asa de arrastre son
interactivos, no se pueden probar sin mover el ratón de verdad.

### Feedback tras probarlo: dos arreglos más (misma sesión)

- **"Gestionar notas" seguía sin animación al abrir.** Solo se había pedido para Ajustes; al verlo
  al lado ya animado, se notó que esta ventana desentonaba. Mismo `PlayOpenAnimation` copiado a
  `NotesManagerWindow`, disparado desde `AppCoordinator.OpenOrActivateNotesManager` tras `Show()` —
  las tres ventanas de la app (nota, Ajustes, gestor) se abren ahora igual.
- **El resaltado de la casilla se veía "más grande... y desplazado".** Causa: `GetRectFromCharacterIndex`
  da el rectángulo de **caret** (alto de línea entero, con interlineado) del carácter, no la caja
  visual del glifo — y encima se extendía hasta el espacio que sigue al glifo, no solo hasta el
  propio glifo. Arreglado en `NoteWindow.FindCheckboxZoneAt`: el ancho se acota al glifo solo (sin
  el espacio) y el alto pasa a un cuadrado del tamaño de la fuente, centrado verticalmente dentro de
  la línea en vez de ocupar todo su alto. La zona de **clic** (más generosa, indentación + glifo +
  espacio) no cambia — el ajuste fue solo del rectángulo que se pinta, no de qué cuenta como clic.

Tests: 271/271. Build limpio. Portable republicado y relanzado. **Pendiente de verificación visual
del usuario** — el ajuste de tamaño/posición del resaltado se hizo a ciegas por descripción, no
viendo el render.

### El resaltado de la casilla, arreglado de verdad: medido a pixel, no descrito de palabra

El primer arreglo de arriba (cuadrado de `FontSize*1.3` centrado en la línea) también se descartó:
el usuario mandó una captura y siguió sin convencerle. En vez de seguir ajustando números a ciegas
por descripción, se aplicó la misma técnica que ya usó este proyecto para decidir ☒ frente a ☑ y el
ancho de las pestañas — renderizar en aislamiento y medir, esta vez con un script de PowerShell +
WPF (`Add-Type -AssemblyName PresentationCore, PresentationFramework, WindowsBase`, sin lanzar la
app ni abrir ninguna ventana real) que:

1. Rasteriza el `TextBox` real (mismo `FontFamily`/`FontSize` que `NoteWindow.TextBody`) con fondo
   blanco puro y escanea los píxeles para encontrar los límites exactos de la tinta del glifo ☐,
   distintos del rectángulo de **caret** que da `GetRectFromCharacterIndex` (ese rectángulo mide la
   línea entera con su interlineado, no la tinta visible — con `FontSize=14` la tinta real mide
   ~9.9px de alto contra los ~18.6px de la línea completa, **el doble**).
2. Con esos números, renderiza varias propuestas una al lado de otra sobre el mismo fondo cian de la
   nota (`#83E7F2`, el color exacto de la captura del usuario) y las lee de vuelta con la
   herramienta de lectura de imágenes — comparando el resaltado ya implementado contra el corregido
   antes de tocar el código de verdad, en vez de adivinar.

Con eso se confirmó a ojo que el resaltado implementado sí era visiblemente más grande que el propio
glifo (se salía por los cuatro lados). `NoteWindow.FindCheckboxZoneAt` pasa de "centrar un cuadrado
de `FontSize*1.3`" a una caja derivada de fracciones medidas de la tinta real (0.093/0.82 de la
anchura de avance, 0.282/0.53 de la altura de línea) más 3px de aire alrededor — una caja de ~13x13
para el tamaño de fuente actual, ajustada al glifo visible en vez de al rectángulo de caret que lo
contiene. **Los números son específicos de este glifo a 14px en Segoe UI Variable Text**: si el
tamaño de fuente cambiara algún día (ver "tamaño de texto" en `ROADMAP.md`), habría que remedir, no
solo reescalar la fórmula a ojo.

Tests: 271/271. Build limpio. Portable republicado y relanzado. **Pendiente de verificación visual
del usuario** — esta vez sí verificado con un render antes de tocar código, pero el veredicto final
en la app real (sombras, DPI del monitor, etc. pueden diferir del render aislado) lo tiene que dar
quien lo use.

### Dos bugs más encontrados al probarlo: casilla pegada al texto, y resaltado que se quedaba flotando

- **Marcar una tarea sin espacio entre el glifo y el texto.** Hasta ahora `TaskLines.GlyphIndex`
  exigía un espacio justo detrás del glifo para contar la línea como tarea — protección deliberada
  contra un ☐ suelto en mitad de una frase. El usuario pidió explícitamente que también se pudiera
  marcar una casilla con el texto pegado sin espacio (p. ej. tras borrar el espacio sin querer
  mientras se edita). Se quitó esa exigencia: ahora basta con que el glifo sea el primer carácter no
  en blanco de la línea. Aldune **sigue sin escribir** nunca una tarea así (`Prefix` sigue siendo
  `"☐ "`, con espacio) — solo se relajó el reconocimiento de una que ya llegue así.
  - Nuevo `TaskLines.PrefixLength(line, glyphIndex)`: 2 si hay espacio detrás del glifo, 1 si no.
    Centraliza la única diferencia real entre una tarea bien escrita y una con el texto pegado, para
    no repetir la comprobación en cada sitio que necesitaba saber "cuántos caracteres quito". Tres
    sitios lo necesitaban y antes asumían `+2` a ciegas: `ToggleTaskLineAt` (Ctrl+L, o se comía la
    primera letra real de la tarea al quitar el prefijo), `EnterContinuation` (mismo problema al
    calcular si la tarea estaba vacía) y `TaskCompletion.HashLine` (el hash del ajuste de
    autoborrado habría cambiado cada vez según si quedaba o no el espacio, rompiendo el seguimiento
    de cuándo se marcó). `ToggleCheckboxAt` no necesitó cambios: solo voltea un carácter, le da igual
    lo que venga detrás.
- **El resaltado de la casilla a veces tardaba en irse.** Causa real: el ajuste nuevo de "borrar
  tareas completadas solas" puede quitar una línea del texto **sin que el ratón se mueva** (se
  dispara desde el temporizador de autoguardado, ver `PruneExpiredTasks`) — si el cursor estaba
  quieto sobre la casilla que acababa de desaparecer, el resaltado se quedaba pintado en su última
  posición conocida hasta el siguiente movimiento real del ratón, que era quien lo recalculaba.
  Arreglado enganchando también el resaltado a `TextBody.TextChanged` (con un `UpdateLayout()` antes
  de preguntar por rectángulos de caracteres, para no leer el layout todavía viejo): cualquier
  cambio de texto —lo escriba el usuario o lo borre el autoborrado— recalcula el resaltado contra la
  posición *actual* del ratón (`Mouse.GetPosition`), no solo los eventos de movimiento.

Tests: 280/280 (9 nuevos: `PrefixLength`, el caso sin espacio en `ToggleTaskLineAt`/`EnterContinuation`/
`ToggleCheckboxAt`, y que `TaskCompletion.HashLine` no cambie según haya o no espacio). Build limpio.
Portable republicado y relanzado. **Pendiente de verificación manual del usuario** para ambos —
sobre todo el del resaltado flotante, que depende de que el autoborrado dispare de verdad mientras
el ratón está quieto encima.

### Un tercer bug del mismo hover: se podía marcar sin estar encima de verdad

Reportado nada más probar los dos arreglos de arriba: con el ratón bastante por debajo de una
casilla, en el hueco vacío del cuerpo de la nota, seguía dejando marcarla. Causa:
`GetCharacterIndexFromPoint(position, snapToText: true)` (cambiado de `false` a `true` en la ronda
del ancho del resaltado, ver más arriba) solo mira qué carácter está horizontalmente más cerca — un
clic muy por debajo de la última línea "cae" igualmente en un carácter de esa línea, sin tener en
cuenta la distancia vertical real. `FindCheckboxZoneAt` ahora comprueba además que el punto del
ratón caiga dentro del alto real de la línea de la casilla (`GetRectFromCharacterIndex(glyphIndex).Top`/
`.Height`) antes de aceptar la zona — si está por encima o por debajo de esa franja, no cuenta, por
muy cerca que quede horizontalmente.

Build limpio (fix solo en `NoteWindow`, capa WPF sin cobertura de `Aldune.Core.Tests`). Portable
republicado y relanzado. **Pendiente de verificación manual del usuario.**

### Cursor de "mover" en el asa de arrastre

Pedido explícito: que el ratón cambie a la cruceta de mover (`Cursors.SizeAll`) al pasar por encima
del asa de la cabecera (ver más arriba, sesión anterior), no solo verse el icono.

**No es tan simple como poner `Cursor="SizeAll"` en el `TextBlock` del asa.** El resto de la
cabecera se arrastra dejando que `WindowChrome` la trate como zona de "caption" implícita (sin
`IsHitTestVisibleInChrome`) — pero una zona de caption es territorio de Windows, no de WPF: los
eventos normales de ratón (`MouseEnter`, y con ellos cualquier `Cursor` que WPF quisiera aplicar)
no llegan ahí. Por eso el asa pasa a ser un elemento normal
(`WindowChrome.IsHitTestVisibleInChrome="True"`, `Background="Transparent"` para que el `Padding`
también cuente como zona sensible) con su propio `Cursor="SizeAll"`, y el arrastre en sí se dispara
a mano en `OnGripMouseDown` con `DragMove()` — el método estándar de WPF para iniciar el arrastre
nativo de una ventana desde un control cualquiera, en vez de depender de que `WindowChrome` lo
reconozca como caption.

Tests: 280/280 (sin cambios en Core). Build limpio. Portable republicado y relanzado. **Pendiente
de verificación manual del usuario.**

### Color de selección de texto, y cursor al reabrir una nota

- **Selección de texto sin el azul del sistema.** `TitleBox`/`TextBody` ganan
  `SelectionBrush="#40000000"` + `SelectionOpacity="1"` — mismo tono que ya usan los botones de la
  cabecera al pasar el ratón (`NoteActionButtonStyle`), reutilizado en vez de inventar un color
  nuevo. Verificado con un render contra los seis colores de la paleta antes de tocar el XAML real
  (la selección de un `TextBox` no se pinta sin foco de teclado real, así que el render simula el
  mismo tinte a mano sobre el rectángulo real del texto en vez de fiarse de `TextBox.Select()` en un
  árbol visual desconectado — mismo tipo de limitación que ya se documentó al comparar tratamientos
  de hover para la casilla de tarea, ver más arriba).
- **Cursor en una línea nueva al reabrir una nota con texto.** Antes el cursor iba justo al final
  del último carácter, así que seguir escribiendo continuaba sin querer la última palabra. Ahora, si
  el cuerpo no termina ya en un salto de línea, se le añade uno antes de posicionar el cursor —
  puramente de trabajo: no marca la nota como editada (se deshace a mano el `_hasPendingEdit` que el
  propio `TextChanged` dispara al añadirlo), así que cerrar la nota sin escribir nada más no deja
  una línea en blanco de más guardada. Si se aprovecha esa línea para escribir algo, se guarda como
  cualquier otro cambio normal.

Tests: 280/280 (sin cambios en Core — los dos cambios son de UI). Build limpio, 0 advertencias
(hubo que adelantar la construcción de `_autosaveTimer` antes del primer `Loaded`, que ahora lo usa,
para que el análisis de nulabilidad no se quejara). Portable republicado y relanzado. **Pendiente de
verificación manual del usuario.**

## Mover líneas con Alt+Arriba/Abajo, abrir todas las notas, y papelera configurable (sesión 2026-09-09)

Tres peticiones sueltas en la misma sesión.

### Alt+Arriba/Alt+Abajo para reordenar líneas

Pedido original: poder arrastrar tareas con casilla para reordenarlas dentro de una nota. Se le puso
delante el coste real antes de implementar nada: el cuerpo es un `TextBox` plano a propósito (spec
v1, sin texto enriquecido), así que arrastrar líneas ahí dentro exige simular el gesto a mano
(distinguirlo del clic de marcar la casilla, dibujar una línea fantasma seguir el ratón, reconstruir
el texto al soltar) — bastante más complejo que nada hecho hasta ahora, y en tensión con esa misma
decisión de diseño. El usuario eligió la alternativa más simple: un atajo de teclado que intercambia
la línea del cursor con la de arriba o abajo, igual que en cualquier editor de código.

- **`Aldune.Core.LineMovement`** (nuevo, TDD): puro, no se limita a tareas — cualquier línea se
  puede subir o bajar, que es lo que hacen otros editores con este mismo atajo y evita una
  restricción arbitraria. **Bug real atrapado por los propios tests antes de tocar la app**:
  intercambiar dos líneas obtenidas con `text.Split('\n')` sin más se lleva por delante el `\r` de
  cada una (que en realidad pertenece al separador de esa posición, no al contenido que se mueve) —
  mismo tipo de bug que ya apareció una vez al borrar líneas en `TaskCompletion.JoinLines`. Arreglado
  separando cada línea en (contenido, terminador) y intercambiando solo el contenido; el terminador
  se queda fijo por posición.
- `NoteWindow.OnBodyKeyDown` traduce el gesto, con prioridad sobre "Arriba en la primera línea sube
  al título" (para que Alt+Arriba en la primera línea no cruce al título en su lugar). Documentado
  en la Ayuda rápida de Ajustes.

### Botón "Abrir todas las notas" en el dock

Pedido para ver todas las notas a la vez, como una mesa de post-its, sin abrirlas de una en una.
`AppCoordinator.OpenAllNotes()` recorre las notas activas y abre las que no lo estén ya, reutilizando
`OpenOrActivateNote` nota a nota — el cascadeo automático que ya calcula
`EdgeDockWindow.PositionNoteWindow` (a partir de cuántas ventanas de nota hay abiertas) evita que
salgan todas exactamente superpuestas, sin ningún cálculo nuevo. Icono nuevo en el pie del dock
(`&#xE8A9;`, Segoe Fluent Icons) junto a "Gestionar notas" y "+"; **elegido sin verificación visual
del glifo real** — puede que no sea el más claro y convenga revisarlo la próxima vez que se vea en
pantalla.

### Auditoría de ajustes fijos, y papelera configurable

El usuario pidió revisar qué valores fijos del código tendría sentido dejar elegir al usuario. Se
repasaron las constantes de comportamiento (no las de geometría/temporización interna, que no son
material de Ajustes): la que destacó fue `NotesRepository.DefaultTrashRetentionDays` (30, fijo) —
sin ningún motivo técnico para que 30 sea mejor que otro número para alguien en concreto, mismo
argumento que ya llevó al plazo configurable de "borrar tareas completadas". Nueva
`AppSettings.TrashRetentionDays` (por defecto 30, el mismo valor de fábrica), nueva sección en
Ajustes (número + "días", mismo patrón visual que el resto). `App.xaml.cs` pasa a usar
`settings.TrashRetentionDays` en vez de la constante directamente.

**Candidato encontrado y aplazado a propósito, no implementado esta vez**: tamaño de texto
(pequeño/normal/grande, aplicado a todas las notas) — ya está en `docs/ROADMAP.md` como "si algún
día se añade algo, que sea esto", respaldado por la investigación de mercado. Se dejó fuera de esta
ronda porque toca tipografía en varias ventanas a la vez y es lo bastante grande como para merecer
su propio paso, no colarse de refilón en una sesión de arreglos sueltos.

Tests: 291/291 (11 nuevos: `LineMovement` completo, y el round-trip de `TrashRetentionDays`). Build
limpio, 0 advertencias. Portable republicado y relanzado. **Pendiente de verificación manual del
usuario** en los tres — sobre todo el icono de "abrir todas", que no se ha visto renderizado de
verdad todavía.

### "Abrir todas" pasa a ser un interruptor

Pedido nada más probar el botón: que darle otra vez cierre todas las notas, no solo las abra. Se
decidió la regla exacta con el usuario (no estaba claro qué debía pasar con algunas abiertas y otras
no): **la pregunta es binaria, "¿hay algo abierto ahora mismo?"**, no de tres vías
(ninguna/algunas/todas) — si hay alguna nota abierta, sea como sea que se abriera (por el botón o a
mano), el botón las cierra todas; si no hay ninguna, las abre todas. Más predecible que intentar
distinguir "algunas" de "todas".

`AppCoordinator.CloseAllNoteWindows()` (nuevo) cierra cada ventana de nota abierta —
`.ToList()` antes de recorrer `_openNoteWindows.Values`, porque cerrar cada una dispara su `Closed`,
que se quita a sí misma del diccionario mientras se está recorriendo. `ToggleAllNotes()` decide entre
abrir y cerrar según `OpenNoteWindowCount`; el botón del dock llama a este método en vez de
`OpenAllNotes()` directamente. Tooltip actualizado para explicar el interruptor.

Tests: 291/291 (sin cambios en Core — cambio solo en `AppCoordinator`, capa WPF). Build limpio.
Portable republicado y relanzado. **Pendiente de verificación manual del usuario.**

### Tooltips oscuros en toda la app

El tooltip de sistema (claro) era el único texto flotante de Aldune que no seguía su propio estilo —
desentonaba tanto sobre el chrome oscuro como sobre el pastel de una nota. `App.xaml` gana un
`Style TargetType="ToolTip"` **implícito** (sin `x:Key`, mismo patrón que los estilos ya existentes
de `Window`/`ScrollBar` ahí mismo): cualquier `ToolTip="..."` ya existente en cualquier ventana sale
con este aspecto sin tocar ninguno de los sitios donde se usa. Mismo tratamiento visual que
`ActionsPopup` (fondo `Ground` #2A261F, esquinas redondeadas, sombra caída), para que se sienta de
la misma familia que el resto de paneles flotantes de la app.

Build limpio. Portable republicado y relanzado. **Pendiente de verificación manual del usuario** —
no se verificó con un render aislado (un `ToolTip` vive en su propio `Popup`/capa, más enrevesado de
forzar a renderizar fuera de pantalla que un control normal); el estilo replica uno ya probado
(`ActionsPopup`), pero conviene confirmarlo pasando el ratón de verdad.

## El dock a la izquierda no quedaba pegado al borde de la pantalla — arreglado

Reportado con capturas (2026-09-09): con `AppSettings.DockEdge = Left`, tanto la tira de reposo como
el abanico desplegado se veían con un hueco claro entre ellos y el borde físico izquierdo de la
pantalla — a la derecha (el borde ya probado) no pasaba. **No es lo mismo** que la desaparición
intermitente de la tira documentada arriba en "Deuda técnica conocida": esto era un desplazamiento
reproducible siempre, no algo esporádico.

### Causa

`EdgeGeometry.WindowRect` ya distinguía los cuatro bordes y posicionaba la propia ventana del dock
correctamente pegada al lado físico que tocara. El bug estaba un nivel más adentro: `RestStrip`
(la tira de guiones), las pestañas del abanico (`TabsList`, dentro de su `DataTemplate`) y el pie de
botones (`FooterBorder`, sin nombre hasta ahora) estaban alineados **`HorizontalAlignment="Right"`
directamente en el XAML**, sin condicionar por borde — el valor correcto solo para
`EdgePosition.Right`, el único borde con el que se diseñó originalmente todo esto (Izquierda se
añadió después reutilizando la misma plantilla visual sin espejarla). Con el dock a la izquierda, la
ventana se colocaba bien pegada al borde físico, pero su contenido seguía alineado contra el lado
**interior** del dock en vez del exterior — de ahí el hueco.

`EdgeDockWindow.PositionNoteWindow` (dónde se abre la nota en sí al pulsar una pestaña) **nunca tuvo
este bug**: ya calculaba la posición distinguiendo los dos bordes correctamente. Lo que hacía que
todo el conjunto se viera roto era solo el contenido del dock, no la nota que se abre desde él.

### Arreglo

Nuevo `EdgeDockWindow.ApplyEdgeAlignment()`, llamado una vez en el constructor: si `_edge` es
`EdgePosition.Left`, espeja `HorizontalAlignment` a `Left` y el margen (leído del propio `Margin`
declarado en XAML, no un número repetido a mano) de `RestStrip` y `FooterBorder`. Las pestañas del
abanico se generan de nuevo en cada `SetNotes` (vía `ItemsControl`/`DataTemplate`), así que no basta
con corregirlas una vez: `OnTabLoaded` (que ya se ejecuta por cada pestaña generada) aplica el mismo
espejado por botón.

No se tocó nada de `EdgeGeometry` ni de `PositionNoteWindow` — el bug estaba enteramente en
alineaciones de XAML no condicionadas por borde, nunca en la geometría en sí (que ya era correcta,
como demuestran los tests de `EdgeGeometryTests` que siguen pasando sin cambios).

Build limpio. Portable republicado y relanzado. **Pendiente de verificación manual del usuario**
—cambio puramente visual, dependiente de tener `DockEdge = Left` activo para verlo.

### La forma de la pestaña también estaba pensada solo para la derecha

Tras el arreglo de arriba, el usuario señaló con una captura que faltaba algo: la propia **forma**
de cada pestaña del abanico (`NoteTabButtonStyle`) sigue redondeada solo por la izquierda —
`CornerRadius="10,0,0,10"` a mano en el XAML, con el comentario original explicándolo: *"el lado
derecho va a ras del canto de la pantalla y redondearlo dejaría ver el escritorio por una muesca"* —
correcto para `EdgePosition.Right`, pero con el dock a la izquierda es el lado **izquierdo** el que
toca el canto real, así que la curva tenía que espejarse igual que la alineación. El texto de dentro
se queda tal cual, alineado a la izquierda — pedido explícito del usuario, y de hecho no había que
tocarlo: el redondeo es de la forma exterior, no de dónde se ancla el texto.

**Verificado con un render antes de tocar el XAML real** (mismo tipo de comparación aislada que ya se
usó para el resaltado de la casilla): tarjeta derecha (actual) contra tarjeta izquierda (espejada)
lado a lado, mismo `CornerRadius`/`BorderThickness`/degradado que usaría la app de verdad — confirmó
visualmente que el espejo queda limpio antes de aplicarlo.

- `NoteTabButtonStyle` gana `x:Name` en los dos `Border` que llevaban la forma (`CardBorder`,
  `SheenBorder` — `HoverOverlay` ya lo tenía), para poder alcanzarlos desde código vía
  `button.Template.FindName(...)` una vez aplicada la plantilla.
- Nuevo recurso `TabSheenLeftEdge`: el mismo degradado `TabSheen` (reflejo de luz en el canto
  redondeado, sombra tenue en el canto a ras) pero con `StartPoint`/`EndPoint` invertidos — más
  simple que reconstruir los `GradientStop` a mano, y evita duplicar los cuatro valores de color.
- `EdgeDockWindow.ApplyLeftEdgeTabShape(Button)` (llamado desde `OnTabLoaded`, junto al espejado de
  alineación que ya existía ahí): espeja `CornerRadius` de los tres bordes y `BorderThickness` del
  borde principal, y cambia el `Background` del borde de brillo al recurso invertido. Dos funciones
  puras y reutilizables, `Mirror(CornerRadius)`/`MirrorHorizontal(Thickness)`, en vez de escribir los
  números al revés a mano en cada sitio.
- **No se tocó** `CardShadow` (la sombra de la tarjeta, `Direction="95"`, casi vertical — la asimetría
  es mínima y no se consideró que mereciera la pena la complejidad de espejar también un ángulo de
  sombra) ni el margen del texto dentro de la pestaña (el usuario pidió explícitamente dejarlo como
  está).

Build limpio. Portable republicado y relanzado. **Pendiente de verificación manual del usuario en la
app real** — el render aislado confirmó la forma, pero no las sombras/DPI del monitor real.

### La causa real del hueco que quedaba: el `Grid` raíz, no cada elemento por separado

El usuario reportó que, tras los dos arreglos de arriba, seguía habiendo hueco — tira, abanico y
**botones** del pie, todos por igual. Causa encontrada: el `Grid` que contiene *todo* el contenido
del dock (`RestStrip`, `FanPanel`, `FooterBorder`) lleva `Margin="18,18,0,18"` **en el propio XAML**
— asimétrico a propósito, sin margen a la derecha porque ese lado va a ras del canto real de la
pantalla y no hay sombra que alojar ahí (comentario original, ver el XAML). Correcto solo para
`EdgePosition.Right`. Con la izquierda, el lado que va a ras es el opuesto — así que aunque
`RestStrip`/`FooterBorder`/las pestañas ya estuvieran bien alineados **dentro** de este `Grid` (los
dos arreglos anteriores), el propio `Grid` seguía siendo un lienzo recortado 18px por el lado
equivocado: sus hijos podían alinearse "a la izquierda" todo lo que quisieran, que seguían viviendo
18px más adentro de lo que debían.

Esto explica por qué "los botones también" — están dentro del mismo `Grid`, así que arreglar
`FooterBorder` por separado no bastaba mientras el contenedor que lo envuelve siguiera encogido.

**Arreglo**: el `Grid` gana `x:Name="ContentGrid"`, y `ApplyEdgeAlignment` (el mismo método de los
arreglos anteriores) le espeja el margen para `EdgePosition.Left` — `Margin.Left` pasa a
`Margin.Right` y viceversa, leídos del propio XAML en vez de repetir `18`/`0` a mano.

Build limpio. Portable republicado y relanzado — el usuario ya tenía `DockEdge` en Izquierda
guardado de antes, así que este relanzamiento debería mostrar el arreglo sin tocar Ajustes.
**Pendiente de verificación manual del usuario.**

### El pie de botones, mismo espejo: orden invertido, no solo movido de sitio

Última pregunta del usuario tras ver el hueco ya arreglado: los tres botones del pie (abrir todas,
gestionar, nueva nota) seguían en el mismo orden de lectura que a la derecha — ¿se deja así, o se
invierte también? Respuesta aplicada: **se invierte**. En el XAML (pensado para la derecha) "+" va
último, y como el grupo entero se pega al lado derecho, ser el último de la fila significa ser el más
cercano al canto real de la pantalla. Mover solo el grupo entero a la izquierda sin tocar el orden
interno dejaba "+" como el más *lejano* del canto en vez del más cercano — cada botón cambiaba su
posición relativa a la pantalla, justo lo contrario de lo que se busca en un espejo (que cada cosa
conserve su distancia al borde, no su orden de lectura).

`ApplyEdgeAlignment` invierte la colección `FooterPanel.Children` para `EdgePosition.Left` — ahora
"+" sigue siendo el botón más cercano al canto real en los dos bordes.

Build limpio. Portable republicado y relanzado. **Pendiente de verificación manual del usuario.**

## Tres arreglos más para cerrar la ronda (sesión 2026-09-11)

### Arrastrar una pestaña hacia abajo la dejaba detrás del pie de botones

Causa: `FanPanel` es un `Grid` de dos filas — fila 0 el abanico (`TabsScroll`), fila 1 `FooterBorder`.
Al declararse después en el XAML, `FooterBorder` pinta por encima por defecto. El
`Panel.SetZIndex(_dragButton, 1000)` que ya pone `OnTabDragMove` en la pestaña arrastrada solo compite
con sus hermanas **dentro** del `StackPanel` de `TabsList` — no alcanza a `FooterBorder`, que vive un
nivel más arriba, en otra fila del mismo `Grid`. Arrastrar una pestaña lo bastante abajo como para
solapar con esa fila la dejaba por detrás de los botones. Arreglado con `Panel.ZIndex="1"` fijo en
`TabsScroll`, para que el abanico entero (arrastre incluido) quede siempre por encima del pie.

### El resaltado de la casilla podía aparecer solo, sin que el ratón la tocara

Reportado al crear una tarea con Ctrl+L: la casilla nueva a veces salía ya con el tinte de "casilla
bajo el ratón", sin haber pasado el ratón por ahí. Causa: el arreglo de la ronda anterior (el
resaltado se recalcula en cada `TextChanged`, no solo al mover el ratón, para que una tarea
autoborrada no dejara el tinte flotando) tenía un efecto secundario no querido — cualquier cambio de
texto por **teclado** (Ctrl+L, escribir, Enter) también recalculaba contra la posición *actual* del
ratón, y si el ratón estaba quieto encima de donde cae la casilla nueva, el tinte se encendía sin que
nadie lo hubiera pedido con un gesto de ratón de verdad.

Arreglo: separar las dos direcciones del cambio. `RefreshCheckboxHoverAfterTextChange` (nuevo, es lo
que ahora llama el `TextChanged`) solo actúa si el resaltado **ya estaba visible** — puede apagarlo o
recolocarlo, pero nunca encenderlo desde cero. Solo un movimiento real del ratón
(`OnBodyMouseMove`) puede pasarlo de oculto a visible. Con esto, el caso de la tarea autoborrada
sigue arreglado (el resaltado estaba visible, se apaga) y el de Ctrl+L también (el resaltado estaba
oculto, sigue oculto).

### Las notas no recordaban su posición al apagar o reiniciar el equipo

El usuario reportó perder las posiciones guardadas al cerrar la app y reabrirla, o al apagar/reiniciar
el PC. Investigado el camino normal de cierre (tray "Salir" → `Application.Current.Shutdown()`): los
tres handlers de `Closing` de `NoteWindow` (`SavePlacementOnce`, `OnClosingWithAnimation`, el que hace
`Flush()`) se disparan los tres en la **misma pasada**, aunque el segundo cancele el cierre para
reproducir la animación de salida — así que guardar posición y texto ya ocurría de forma síncrona ahí,
sin depender de que la animación de ~140ms llegara a completarse. Ese camino ya estaba bien.

**El hueco real estaba en el apagado/reinicio del sistema**: no había ningún manejo de
`Microsoft.Win32.SystemEvents.SessionEnding` (el aviso que Windows manda antes de cerrar la sesión).
Sin él, Windows puede terminar el proceso sin pasar por el ciclo normal de `Closing` de cada ventana
—y menos aún esperar a que la animación de cierre complete—, así que una nota abierta en ese momento
podía perder tanto el texto sin guardar como la posición.

- `NoteWindow.FlushForShutdown()` (nuevo, `internal`): guarda texto y posición ya, sin pasar por el
  ciclo de cierre normal ni su animación — llama a `Flush()` y `SavePlacementOnce` directamente.
- `AppCoordinator.FlushAllOpenNotes()` (nuevo): lo llama para cada nota abierta.
- `App.xaml.cs` engancha `SystemEvents.SessionEnding` (mismo patrón que ya usa
  `DisplaySettingsChanged`, con su baja correspondiente en `Exit`) para llamar a
  `FlushAllOpenNotes()` en cuanto llega el aviso, con margen antes de que Windows fuerce el cierre.

Tests: 291/291 (sin cambios en Core — los tres arreglos son de capa WPF). Build limpio. Portable
republicado y relanzado. **Pendiente de verificación manual del usuario** en los tres — el de
apagar/reiniciar en particular solo se puede confirmar de verdad reiniciando el equipo de verdad con
una nota abierta y sin guardar.

## Ajustes se salía por abajo en la pantalla del portátil

Reportado por el usuario: en multimonitor (portátil + externo), Ajustes se cortaba por abajo en la
pantalla más pequeña del portátil. Causa: `SettingsWindow` calculaba su `MaxHeight` contra
`SystemParameters.WorkArea` — que en WPF es **siempre** el área de trabajo del monitor **primario**
del sistema, nunca la del monitor donde la ventana se muestra de verdad. Con el monitor externo
como primario (caso típico de este tipo de configuración), el tope salía calculado contra la
pantalla grande, y de nada servía si Ajustes terminaba abriéndose en la del portátil.

`EdgeDockWindow.CenterOnThisMonitor` (que ya centra la ventana contra el monitor correcto, el del
propio dock que la abrió) ahora también fija `window.MaxHeight` contra `_workingArea.Height * 0.9` —
el área de trabajo real del monitor donde se va a mostrar, no la del primario. Corre antes de
`Show()`, así que la ventana nunca llega a pintarse con el tope equivocado. La línea original en el
constructor de `SettingsWindow` se queda como valor de reserva para el caso (no usado en la práctica)
de mostrarla sin pasar por `AppCoordinator.OpenSettings`. Mismo arreglo beneficia de paso a
`NotesManagerWindow`, que comparte el mismo método aunque tenga alto fijo en vez de `SizeToContent`.

Tests: 291/291 (sin cambios en Core). Build limpio. Portable republicado y relanzado. **Pendiente de
verificación manual del usuario** en la pantalla del portátil.

## Cursor en una tarea vacía al reabrir, y parpadeo al cambiar de pantalla en Ajustes (sesión 2026-09-11)

### El cursor no respetaba una tarea ya vacía esperando texto

Pedido del usuario: si se deja una nota con una casilla puesta pero sin texto detrás (`"☐ "` solo),
al reabrirla el cursor tiene que ir justo ahí, no en una línea nueva debajo — una tarea vacía ya es
en sí misma "una línea en blanco esperando texto", así que añadirle otra debajo deja un hueco de más
antes de poder escribir la tarea.

Nuevo `TaskLines.IsEmptyTaskLine(line)` (puro, TDD): una tarea es "vacía" si no tiene contenido real
después del prefijo (glifo, o glifo+espacio según `PrefixLength`). Se reaprovecha también dentro de
`EnterContinuation`, que ya calculaba exactamente lo mismo a mano para decidir si Enter debía
terminar la lista — un sitio menos con la misma lógica repetida. El `Loaded` de `NoteWindow` ahora
comprueba la última línea del cuerpo con este método antes de decidir si añade la línea en blanco de
trabajo: si ya es una tarea vacía, no añade nada y dan el cursor cae de forma natural justo detrás
del prefijo.

Tests: 299/299 (8 nuevos para `IsEmptyTaskLine`). Build limpio.

### El dock parpadeaba al cambiar de pantalla en Ajustes

Reportado por el usuario: al cambiar el destino del dock (una pantalla concreta, o todas) en
Ajustes, se veía un instante el pie de botones (el "+", el engranaje...) fuera de sitio antes de que
el dock se recolocara. Causa: `App.BuildDocks()` (el mismo método que arma los docks al arrancar y al
reconstruir tras un cambio de pantallas) mostraba cada `EdgeDockWindow` con `Show()` **antes** de
decirle qué notas hay — un dock recién construido nace con `_noteCount = 0`, y `ApplyState` trata
eso como el caso "vacío" de verdad (a propósito: sin notas, enseña los botones directamente porque
son la única acción posible). Ese estado "vacío" se pintaba en la pantalla real durante el instante
que tardaba en llegar el `RefreshAll()` de después, que es lo que de verdad rellena `_noteCount` con
las notas reales y corrige el estado — justo el parpadeo descrito.

Arreglo: `dock.Refresh()` antes de `dock.Show()` en el bucle de `BuildDocks()`, para que el dock ya
tenga sus datos reales (y por tanto el estado correcto de reposo/vacío) desde el primer frame que se
llega a pintar. El `RefreshAll()` final se queda igual, por si acaso, pero ya no tiene nada que
corregir en el caso normal.

Tests: 299/299 (sin cambios en Core — el arreglo es de capa WPF). Build limpio. Portable republicado
y relanzado. **Pendiente de verificación manual del usuario** en los dos.

## Exportar a Markdown (sesión 2026-09-11)

Primer punto de `docs/ROADMAP.md` que se implementa de la lista de "pendiente y decidido: se hará".
La regla de conversión de casillas ya estaba decidida ahí; quedaba por decidir alcance, formato y
forma de guardar — resuelto con `superpowers:brainstorming` (bounded, sin spec formal) antes de
tocar código:

- **Alcance**: una nota a la vez (desde `NoteWindow`) **y** en bloque (desde `NotesManagerWindow`).
- **Formato**: solo Markdown — es el que ya menciona el roadmap y el que mejor conserva estructura.
- **Ubicación**: diálogo nativo (`Microsoft.Win32.SaveFileDialog`/`OpenFolderDialog`, este último ya
  disponible en .NET sin tirar de WinForms pese a que el proyecto tiene `UseWindowsForms` habilitado
  solo por `TrayIcon`), no una carpeta fija.

### `Aldune.Core.MarkdownExport` (nuevo, TDD)

Puro, mismo patrón que `NoteTitleHelper`/`TaskLines`, sin dependencia de WPF:

- `ToMarkdown(string noteText)`: reutiliza `NoteTitleHelper.GetTitle` y `NoteText.Split` tal cual
  (el título exportado es el mismo que ya se ve en la pestaña del dock, no un cálculo aparte) —
  título como encabezado `# `, y cada línea de tarea del cuerpo (`TaskLines.GlyphIndex`/
  `PrefixLength`/`IsChecked`, ☐/☒/☑) traducida a `- [ ]`/`- [x]` conservando la sangría delante del
  guion. El resto del texto no se toca.
- `SuggestedFileName(string noteText)`: título saneado (fuera los caracteres inválidos de nombre de
  fichero en Windows, recorte a 80 caracteres, `"Nota"` si queda vacío tras sanear) + `.md`.

14 tests nuevos: encabezado, cuerpo normal intacto, las tres variantes de casilla, sangría
preservada, el caso "glifo pegado al texto sin espacio" (`PrefixLength`), varias tareas mezcladas
con texto suelto, y el saneado de nombre de fichero (caracteres inválidos, texto vacío, título muy
largo).

### Capa WPF

- **`NoteWindow`**: nueva entrada "Exportar a Markdown" en el menú "⋯" (`ActionsPopup`), separada de
  Archivar/Papelera por su propio divisor — exporta el texto **en vivo** de la ventana (título+cuerpo
  tal como están en pantalla en ese momento, no lo último guardado en la base de datos), con
  `SaveFileDialog` y el nombre sugerido por `MarkdownExport.SuggestedFileName`.
- **`NotesManagerWindow`**: nuevo botón "Exportar" en la barra de herramientas (mismo `WrapPanel` que
  ya usan los demás, por la misma razón documentada más arriba en "Botón Archivadas + gestor de
  notas": un botón más no cabía en una sola línea). A diferencia de Archivar/Restaurar/Papelera, este
  botón **no exige selección**: exporta las filas marcadas si hay alguna, o todas las del filtro
  activo si no hay ninguna — así sirve tanto para sacar una nota suelta como para un volcado completo
  de "Todas" sin tener que marcarlas una a una. Un fichero `.md` por nota en la carpeta elegida, con
  sufijo numérico `" (2)"`, `" (3)"`... si dos notas generan el mismo nombre de fichero o si ya existe
  uno igual de una exportación anterior en esa misma carpeta.
- Ninguno de los dos vuelca el BLOB cifrado: `NoteWindow` ya tiene el texto en claro en sus dos
  `TextBox`, y `NotesManagerWindow` ya carga `Note.Text` descifrado vía `NotesRepository.GetByState`
  (como el resto de la ventana) — `ContentCipher` no aparece en ningún punto de este cambio.
- Icono del botón de exportar en bloque (`&#xE896;`, Segoe Fluent Icons) **elegido sin verificación
  visual del render real** — mismo aviso que ya se dejó anotado para el icono de "abrir todas" (ver
  más arriba); puede que convenga revisarlo la próxima vez que se vea en pantalla.
- Textos nuevos en `Aldune.Resources.Strings` (ES/EN): `ExportToMarkdown`, `MarkdownFileFilter`,
  `Export`, `ExportFolderDialogTitle`.

Tests: 313/313 (14 nuevos, todos en Core). Build limpio, 0 advertencias. **Pendiente de verificación
manual del usuario** en los dos puntos — sobre todo el diálogo de carpeta en bloque, que no se ha
visto abrirse de verdad todavía.

### Además del `.md` suelto, un `.zip` en el export en bloque — revisado dos veces

Preguntado tras ver el resumen de arriba: ¿carpeta con `.md` sueltos, o `.zip`? Ya estaba
implementado como "carpeta normal" (lo de arriba). El usuario pidió poder elegir entre las dos —
aclarado que se refería a un diálogo, pero dejó la decisión abierta ("lo que consideres mejor"). Se
optó primero por **las dos cosas siempre, sin preguntar** (mismo criterio que el descarte del
selector de color al crear nota, "complejidad innecesaria para el beneficio") — implementado así una
primera vez.

**El usuario corrigió esa decisión de inmediato**: prefiere el diálogo después de todo, en concreto
para no llenar la carpeta de descargas con el mismo contenido dos veces (los `.md` sueltos y el `.zip`
a la vez). Motivo válido que el criterio anterior no había pesado — aquí no aplica el mismo argumento
que con el selector de color (ese evitaba un paso sin coste alguno para el resultado; aquí "siempre
las dos cosas" sí tiene un coste real, duplicar contenido en el disco del usuario).

**Diseño final**: `MessageBox.Show` con `YesNoCancel` antes de elegir destino — "Sí" exporta como
`.zip` único (`SaveFileDialog`), "No" como carpeta con `.md` sueltos (`OpenFolderDialog`),
mutuamente excluyentes. En el camino del `.zip`, las notas se escriben **directamente como entradas
del archivo** (`ZipArchiveEntry.Open()` + `StreamWriter`) sin pasar por ficheros `.md` sueltos en
disco primero — así el `.zip` no deja nada más a su lado tampoco. `UniqueFileName` se generalizó a
`UniqueName` con un `Func<string, bool>? alsoTaken` opcional: la comprobación contra disco
(`File.Exists`) solo tiene sentido en el camino de carpeta, el `.zip` siempre es un fichero nuevo.

**Corrección de proceso, anotada para no repetirla**: el usuario pidió explícitamente dejar de
reconstruir y relanzar el portable tras cada cambio suelto — solo al cierre de una sesión larga de
cambios. `dotnet build`/`dotnet test` y la documentación en `STATUS.md`/`ROADMAP.md` siguen
haciéndose en cada cambio; lo que se agrupa es solo publicar+relanzar el `.exe`.

Tests: 313/313 (sin cambios — el zip es capa WPF, mismo patrón que el resto del export en bloque).
Build limpio.

## Recordatorios con notificación de Windows (sesión 2026-09-11/12)

Segundo punto de `docs/ROADMAP.md` §2 implementado — "el mayor hueco funcional frente a la
competencia de Windows" según la investigación de mercado. Spec y plan escritos con
`superpowers:brainstorming`/`writing-plans` (`docs/superpowers/specs/2026-09-11-aldune-reminders-design.md`,
`docs/superpowers/plans/2026-09-11-aldune-reminders.md`), ejecutados con
`superpowers:subagent-driven-development` en un worktree aparte (`fanote-reminders`), 5 tareas + una
ronda de arreglos tras la revisión final de toda la rama.

### Qué hay

- **Recordatorio puntual por nota** (uno activo a la vez, se reemplaza al poner uno nuevo): entrada
  "Recordatorio" en el menú "⋯" de `NoteWindow`, con panel desplegable — tres atajos (`ReminderPresets`:
  En 1 hora / Esta noche a las 20:00 / Mañana a las 9:00, puro y testeado en el límite exacto de las
  20:00) o calendario + hora/minuto manual.
- **Aviso nativo de Windows** (`ReminderScheduler`): sondea cada 30s con un `DispatcherTimer` y hace
  una pasada de catch-up al arrancar, para que un recordatorio vencido con la app cerrada avise igual
  al reabrir. Reutiliza el `NotifyIcon` que ya crea `TrayIcon` (no un segundo icono de bandeja) y
  `ShowBalloonTip`; clic en el aviso abre la nota (`AppCoordinator.OpenNoteById`, nuevo).
- **Insignia en la pestaña del dock**: reloj pequeño (`&#xE917;`, Segoe Fluent Icons) en la esquina de
  cualquier pestaña con recordatorio pendiente — verificado visualmente que el glifo se ve como un
  reloj reconocible, no hizo falta cambiarlo.
- Tabla nueva `NoteReminder` (`NoteId` como clave primaria, sin FK — mismo patrón que `NotePlacement`/
  `TaskCompletion`), nunca una columna en `Note`. Los timestamps se guardan en UTC
  (`DateTimeOffset.ToUniversalTime().ToString("O")`), igual que `CreatedAt`/`UpdatedAt`; los cálculos
  de cara al usuario (presets, el selector) trabajan en hora local.

### Lo que encontró la revisión final de toda la rama (y se arregló, una sola ronda)

Las cinco tareas pasaron su propia revisión individual limpias, pero una revisión de conjunto
(modelo más capaz, sobre las cinco a la vez) encontró 5 huecos de integración que ningún task-review
aislado podía ver:

1. Saltar un recordatorio no refrescaba el dock — la insignia se quedaba pegada hasta una acción no
   relacionada. Arreglado: `ReminderScheduler` llama a `_coordinator.RefreshAll()` tras avisar.
2. Una nota archivada o en la papelera seguía disparando su recordatorio (y no había insignia en
   ningún sitio para avisar de eso, porque el dock solo lista notas activas). Arreglado: `GetDueReminders`/
   `GetPendingReminders` ahora filtran por `Note.State = Active`, con 4 tests nuevos.
3. `ReminderScheduler` asumía que solo hay un aviso visible a la vez para decidir qué nota abrir al
   hacer clic — pero en Windows 10/11 los avisos se apilan en el Centro de actividades y siguen siendo
   pulsables. Si sonaban dos recordatorios de nota única seguidos, pulsar el primero (ya viejo) podía
   abrir la nota del segundo. No tiene arreglo completo (la API de `NotifyIcon` no expone qué aviso
   concreto se pulsó) — mitigado: al cerrarse un aviso (`BalloonTipClosed`) se olvida qué nota
   recordaba, así que un clic tardío sobre un aviso viejo abre como mucho el gestor de notas, nunca la
   nota equivocada. Comentario del código corregido para no afirmar una garantía que no se cumple.
4. La pasada de catch-up al arrancar corría fuera de la protección de errores de `OnStartup` (el
   propio comentario del fichero ya avisaba de que el manejador global no coge de forma fiable
   excepciones ahí), y un fallo persistente en el sondeo de 30s habría abierto un cuadro de diálogo
   modal cada 30 segundos para siempre. Arreglado: el catch-up se difiere con `Dispatcher.BeginInvoke`
   para que caiga dentro de la protección normal, y `CheckDueReminders` para el temporizador si algo
   falla (como mucho un aviso, no un bucle infinito).
5. `ReminderScheduler` no se destruía nunca — un aviso disparado justo al cerrar la app, después de
   `_trayIcon.Dispose()`, podía lanzar `ObjectDisposedException` sobre el `NotifyIcon` ya liberado.
   Arreglado: `ReminderScheduler` implementa `IDisposable` (para el temporizador, desengancha sus dos
   eventos) y se destruye antes que `_trayIcon` al salir.

Los cinco arreglos fueron a una segunda revisión (acotada, solo el diff del arreglo) que los confirmó
sin encontrar nada nuevo roto.

### Pendiente de verificación manual del usuario

El único punto que ni la implementación ni las revisiones pudieron confirmar de forma visual: que el
aviso de Windows aparece de verdad en pantalla y que al pulsarlo abre la nota correcta. Se confirmó de
forma indirecta (a nivel de base de datos: `ClearReminder` se ejecuta, y la misma rama de código llama
a `ShowBalloonTip` justo después, incondicionalmente) pero no se vio el globo en sí — un intento de
capturarlo con capturas de pantalla no dio con la región correcta de la pantalla (barra de tareas en
modo autoocultar) y se descartó insistir para no repetir una captura de escritorio completo (una de
ellas capturó de refilón contenido personal ajeno a la app, borrada al momento). **Para probarlo**:
poner un recordatorio a 1 minuto vista y comprobar que salta el aviso de Windows y que el clic abre la
nota correcta.

### Otros hallazgos, anotados pero no arreglados (fuera de alcance de esta sesión)

- `NoteWindow` no refresca en vivo el texto del botón "Recordatorio" si el `ReminderScheduler` lo
  limpia en segundo plano mientras esa misma ventana está abierta — solo se refresca al abrir la
  ventana o al usar Guardar/Quitar. Poco probable que se note en uso real; reabrir la nota lo corrige.
- El selector de fecha/hora no avisa de nada si se pone una fecha ya pasada (dispara en menos de 30s)
  ni distingue eso de un error de escritura.
- Con Asistente de concentración/notificaciones desactivadas para Aldune, un recordatorio se pierde en
  silencio (la fila se borra igual, avise Windows o no) — sin rastro en ningún sitio de que sonó.
- Formato de fecha en el recordatorio (`dd/MM HH:mm`) usa los separadores de la configuración regional
  activa vía interpolación de cadena — en la mayoría de configuraciones (incluida la española) se ve
  bien, pero técnicamente no está forzado a invariante.

Tests: 335/335 (18 nuevos: 11 CRUD de recordatorio + 7 de `ReminderPresets`, más 4 de exclusión por
estado ya contados dentro de los 335). Build limpio, 0 advertencias nuevas.

### Retoque posterior: el `Calendar` del selector en modo oscuro

Preguntado tras ver el panel en uso: el `Calendar` de WPF usa su plantilla por defecto (pensada para
tema claro) y se veía como un recuadro claro ajeno al resto del panel oscuro del "⋯". Se probó primero
`ThemeMode="Dark"` (la propiedad de tema Fluent de WPF en .NET 9+) directamente sobre el control —
**no existe como propiedad de elemento en esta versión** (falla en tiempo de compilación, `MC3072`),
solo parece estar disponible a nivel de `Application`/`Window`, y aplicarla ahí recolorearía toda la
app con la paleta Fluent genérica en vez de la propia. Se optó por lo más quirúrgico: `Foreground` de
`CalendarDayButton`/`CalendarButton` puestos implícitos en `App.xaml` (mismo patrón que `ToolTip`/
`ScrollBar` de esa misma sección) y un `DarkCalendarStyle` con `Background`/`BorderBrush`/`Foreground`
reutilizando exactamente la paleta ya en uso en `NoteWindow.xaml` (`#2A261F`/`#3C3730`/`#EDE7DC`), sin
inventar tonos nuevos. Solo se toca el estado "normal" del día — los estados especiales (hoy,
seleccionado) siguen con los colores por defecto de la plantilla.

Verificado una vez con una captura recortada a los límites reales del panel (mes/año y rejilla de días
legibles en claro sobre oscuro) antes de que un error de cálculo en un recorte posterior capturara
contenido ajeno a Aldune en el monitor equivocado (ver más abajo) y se decidiera parar ahí. **Queda sin
tocar y anotado para si se retoma**: las dos cajas de texto de hora/minuto (`ReminderHourBox`/
`ReminderMinuteBox`) tienen el mismo problema (recuadro blanco por defecto) y no se tocaron en esta
pasada — el usuario preguntó específicamente por el calendario.

**Incidente durante la verificación manual, anotado por transparencia**: al calcular automáticamente
el rectángulo de una captura de pantalla para comprobar el resultado, un cálculo con coordenadas
incorrectas capturó contenido de otra aplicación en el monitor horizontal (que el usuario tenía en uso
en ese momento) en vez de limitarse al panel de Aldune. Se borró el fichero al momento sin más
inspección. El usuario aclaró que cualquier prueba visual futura debe limitarse al monitor vertical.

## Listas con viñeta (flecha), hermanas de las tareas (sesión 2026-09-12)

Pedido del usuario: poder hacer listas con guion/flecha, igual que ya se puede convertir una línea en
tarea. Bounded (`superpowers:brainstorming`, sin spec formal): extiende un patrón que ya existía
(`TaskLines`/Ctrl+L) en vez de crear algo nuevo desde cero.

**Glifo — flecha `→` (U+2192), no un guion.** Decidido con un render aislado (`RenderTargetBitmap`,
misma técnica que ya sirvió para elegir `☒`) comparando guion/guion largo/raya/flecha/viñeta/triángulo
sobre el fondo pastel real de la nota: los seis miden la misma altura de línea en `Segoe UI Variable
Text` (ninguno cae a fuente sustituta), así que la decisión fue de significado, no de métricas. Se
descartó el guion normal (y también la raya) porque es un carácter que alguien podría escribir de
verdad al empezar una frase — el sistema lo detectaría como lista sin querer. La flecha, como `☐`,
nadie la teclea por accidente.

### `Aldune.Core.BulletLines` (nuevo, TDD) + `Aldune.Core.LineText` (extraído)

Hermana de `TaskLines`, mismo patrón exacto (prefijo de texto plano `"→ "`, sin `RichTextBox`) pero sin
estado propio — una viñeta no se marca, solo está o no está. Se extrajo `LineText.Start`/`End` (dónde
empieza/acaba una línea) a una clase compartida pequeña, ya que `TaskLines.LineStart` era público y la
lógica se iba a duplicar entre las dos clases; `TaskLines` ahora delega ahí sin cambiar su API pública.

**Conversión cruzada, no apilado**: pulsar el atajo de tarea sobre una línea que ya es una viñeta la
convierte en tarea (y viceversa), en vez de dejar los dos prefijos juntos — `TaskLines.ToggleTaskLineAt`
y `BulletLines.ToggleBulletLineAt` cada una reconoce el prefijo de la otra family como caso especial.
Enter continúa la lista igual que con tareas (nueva línea con `→ `; en una viñeta vacía, Enter termina
la lista). Ninguna de las dos cuenta para el recuento de tareas del dock (`TaskLines.Count` no cambia).

### Atajo y menú

**Ctrl+Shift+L**, atajo nuevo a propósito (decisión del usuario tras preguntarle): Ctrl+L se queda solo
para tareas, no se convirtió en un ciclo de tres estados para no mezclar los dos significados. Botón
"Convertir en lista" en el menú "⋯", justo debajo de "Convertir en tarea", mismo patrón (atajo escrito
al lado) para quien no lo conoce.

### Exportar a Markdown

Una línea con viñeta sale como `- contenido` (guion estándar de Markdown) — cero traducción especial,
a diferencia de una tarea que necesita `[ ]`/`[x]`. `MarkdownExport.ConvertLine` ahora comprueba tarea
primero y viñeta después, cada una con su propio marcador.

Tests: 374/374 (39 nuevos: 20 de `BulletLines`, 2 de conversión cruzada en `TaskLinesTests`, 4 de
Markdown, y el resto de la reubicación de `LineText` sin romper nada). Verificado a mano contra la app
real (creando y borrando una nota de prueba, limitado al monitor vertical): escribir, Ctrl+Shift+L,
Enter continúa la lista, Ctrl+L sobre una viñeta la convierte en tarea y viceversa, Enter en una viñeta
vacía termina la lista, y el botón del menú "⋯" muestra el texto y el atajo correctos. Build limpio, 0
advertencias nuevas.

## Sangría de listas con Tab/Mayús+Tab — "árboles" sin modelo nuevo (sesión 2026-09-12)

Pedido del usuario ("árboles de listas con divisiones y subdivisiones") + revisar el comportamiento de
Tab en la nota. Bounded (`superpowers:brainstorming`): tras mirar el código, `TaskLines`/`BulletLines`
ya reconocían sangría arbitraria delante del glifo (y `EnterContinuation` ya la conservaba al continuar
con Enter) — lo único que faltaba de verdad era una forma de generarla desde el teclado. Preguntado
explícitamente si además de sangría se quería plegar/colapsar ramas: **no**, solo sangría visual, así
que no hizo falta ningún modelo de árbol nuevo.

### `Aldune.Core.ListIndent` (nuevo, TDD)

`Indent`/`Outdent`: sobre una línea de tarea o viñeta, añaden o quitan 4 espacios al principio (un
nivel). Devuelven `null` si la línea no es ni tarea ni viñeta, para que `NoteWindow` deje pasar el Tab
normal. `Outdent` en el nivel raíz no baja de cero (se queda igual, pero sigue "manejando" la tecla).

### Tab en `NoteWindow` — el comportamiento real que se pidió revisar

Antes, `TextBody` no tenía `AcceptsTab`, así que Tab sacaba el foco del cuadro de texto sin ningún
beneficio en esta ventana. Ahora: sobre una tarea o viñeta, Tab/Mayús+Tab suben/bajan un nivel; sobre
una línea normal, Tab inserta una tabulación literal (`AcceptsTab="True"`, cambio real de
comportamiento) en vez de sacar el foco. Mayús+Tab en línea normal se deja como estaba (navegación de
foco hacia atrás) — gesto raro mientras se escribe prosa, no se ha tocado.

Tests: 386/386 (12 nuevos de `ListIndent`). Verificado a mano contra la app real (limitado al monitor
vertical, nota de prueba borrada al terminar): Tab en una tarea indenta y mantiene el foco, Mayús+Tab
la devuelve al nivel raíz, y Tab en texto suelto inserta una tabulación en vez de escaparse. Build
limpio, 0 advertencias nuevas.

**Anotados por el usuario en la misma sesión, pendientes de arreglar (ver `ROADMAP.md` §5)**: el caret
de `TitleBox` se cruza con la "T" del placeholder cuando el título está vacío; el botón "⋯" no cierra
el menú al pulsarlo de nuevo estando abierto (lo reabre en su lugar, sospecha de bug clásico de
`Popup`/`StaysOpen="False"`). Ninguno de los dos se ha tocado todavía — quedan para el Grupo B o una
sesión aparte.

## Grupo B: la nota se ajusta sola de tamaño al escribir (sesión 2026-09-12)

Pedido del usuario: que la nota crezca sola al escribir en vez de tener que estirar la pestaña a mano,
más un botón para restaurar el tamaño. Bounded. El diseño cambió una vez a mitad de verificación
manual, con el usuario viéndolo en vivo — anotado abajo con el porqué.

### `Aldune.Core.MonitorLookup.MonitorAt` (nuevo, TDD)

`DeviceNameAt` ya encontraba el monitor real (no `SystemParameters.WorkArea`, que siempre da el
principal) donde cae el centro de una ventana, pero solo devolvía su nombre. `MonitorAt` es lo mismo
pero devuelve el `MonitorInfo` entero — hace falta su `WorkArea` para el tope de crecimiento.
`DeviceNameAt` ahora delega en él, sin cambiar su firma pública.

### `NoteWindow.FitHeightToContent`

Ajusta el alto de la ventana al contenido actual en cada `TextChanged` y al abrir la nota — crece si
no cabe, encoge si sobra sitio, entre el alto inicial definido en XAML (320px en la configuración
actual, nunca encoge por debajo) y `MaxHeight` (700px, o el área de trabajo real del monitor si es menor — nunca
`SystemParameters.WorkArea`, mismo aviso que ya tiene `SettingsWindow`).

**Un arrastre manual del borde bloquea el ajuste automático para el resto de esa apertura** — el
usuario ha tomado el control, y seguir tocándole el tamaño mientras escribe sería pelearse con él. El
botón "Restaurar tamaño" (menú "⋯") lo desbloquea y vuelve al tamaño inicial de la ventana, sin
re-crecer en ese mismo clic aunque el contenido ya no quepa ahí. La distinción entre "cambio nuestro" y "arrastre real" no se guarda en
ningún sitio — cada apertura empieza otra vez en modo automático, sea cual sea el alto con el que se
guardó la nota la última vez (ese alto puede venir tanto de un ajuste automático anterior como de un
arrastre real, y no hay forma barata de distinguirlos entre sesiones sin tocar el esquema de la base de
datos).

**Bug encontrado y arreglado a mitad de verificación**: `AppCoordinator.TryRestorePlacement` fija
`Width`/`Height` (para restaurar la posición guardada) *antes* de `Show()` — enganchar `SizeChanged`
directamente en el constructor disparaba ese cambio como si fuera un arrastre manual, bloqueando el
ajuste automático nada más abrir cualquier nota con posición guardada, antes incluso de verse.
Arreglado enganchándolo dentro de `Loaded` en su lugar, cuando la restauración ya ha terminado.

**Cambio de diseño en vivo**: la primera versión solo crecía, nunca encogía (decisión explícita previa
del usuario). Al verlo en uso real, pidió lo contrario — que encoja también al borrar texto, "igual que
crece pero al revés", salvo que se haya tocado a mano. Se cambió sin volver a todo el proceso de
brainstorming: es la misma pieza, con el mismo mecanismo, solo que simétrico en las dos direcciones.

**Ajustado también en vivo**: el límite inicial (90% del alto del monitor) dejaba crecer la nota casi
hasta llenar la pantalla vertical entera, y el usuario reportó parpadeo de la barra de scroll durante
el crecimiento. Bajado a un tope fijo de 700px (con el monitor real como límite aparte para pantallas
pequeñas), y añadido un segundo `UpdateLayout()` tras cambiar el alto para que el nuevo diseño se
asiente antes de que se pinte el siguiente fotograma — evita que la barra de scroll llegue a mostrarse
un instante durante el propio ajuste.

**Incidente durante la verificación manual, anotado por transparencia**: al simular la escritura con
`SendKeys` para probar el límite de crecimiento, el foco se escapó de la ventana de la nota hacia la
terminal del propio usuario en algún punto de una tanda larga de pulsaciones, y varias líneas de prueba
llegaron como mensajes reales suyos. Se cortó esa vía de prueba al momento y el resto de la
verificación se hizo con `ValuePattern.SetValue` (fija el texto vía UI Automation sin simular teclado
real, así que no puede volver a escaparse a ningún sitio).

Tests: 389/389 (3 nuevos de `MonitorLookup.MonitorAt`; el resto de la lógica es capa WPF, sin tests
automáticos por el mismo criterio que el resto de esa capa). Verificado a mano contra la app real
(limitado al monitor vertical, con `ValuePattern.SetValue`, nota de prueba borrada al terminar):
crece hasta el tope de 700px, encoge de vuelta a 320 al borrar texto, un redimensionado manual
(`TransformPattern.Resize`) bloquea el ajuste hasta pulsar "Restaurar tamaño". Ese botón devuelve la
ventana al tamaño inicial real definido al crearla, aunque el contenido actual sea más largo; deja el
scroll disponible si hace falta y reactiva el autoajuste para los siguientes cambios de texto. Build
limpio, 0 advertencias nuevas.

**Pendiente, no abordado en esta sesión**: el rediseño visual de la barra de scroll en sí (el usuario
lo sigue queriendo, pero sin poder describir qué le falla — hace falta una maqueta con opciones
concretas antes de poder decidir, no solo la pregunta abierta).

## Solapamiento de pestañas al arrastrar hacia abajo (sesión 2026-09-12)

Al arrastrar una pestaña hacia abajo, el movimiento y el orden persistían, pero la pestaña podía
quedar visualmente detrás de las que atravesaba. El intento de usar una `DragLayer` con una copia
visual resultó demasiado invasivo y se retiró: podía introducir estados visuales distintos del botón
real. La implementación que queda mantiene la pestaña real en su sitio, pero durante el gesto eleva
el `Button`, el contenedor devuelto por `ItemContainerGenerator` y el `ContentPresenter` real. Al
soltar restaura esos valores y después calcula/persiste el nuevo índice.

Tests: 389/389. Build de la aplicación correcto en salida temporal (la instancia abierta de Aldune
bloquea su DLL/EXE de `bin`); quedan las 4 advertencias CA1416 ya existentes de `DatabaseKeyProvider`.
Pendiente de verificación visual manual con varias pestañas, arrastrando una de las primeras hacia
abajo por delante de las siguientes.

## Bordes superior e inferior del dock (sesión 2026-09-12)

Se habilitaron Arriba y Abajo en Ajustes. Para esos bordes, `EdgeDockWindow` conserva una barra de
reposo horizontal, pero al desplegarse usa la misma columna vertical de tarjetas anchas que el dock
lateral. El solape y el arrastre siguen usando el eje vertical; las formas de las pestañas dejan el
canto físico sin borde redondeado. `PositionNoteWindow` coloca la nota debajo del dock superior o
encima del inferior, manteniendo el acotado al monitor. Izquierda y Derecha conservan su layout y
comportamiento anteriores.

Build de la aplicación correcto, sin advertencias; tests: 389/389. Pendiente de verificación manual
en una instalación con el dock configurado primero en Arriba y después en Abajo, incluyendo muchas
notas, scroll, reordenación y apertura desde la pestaña.

## Rediseño geométrico de Arriba/Abajo (sesión 2026-09-12)

La primera implementación horizontal era una adaptación incompleta del dock lateral: intentaba poner
las tarjetas verticales en fila, desperdiciando el espacio y recortando la lectura del título. Se
reemplazó por un diseño propio para esos bordes: en reposo queda una barra horizontal; al desplegarse,
la ventana conserva el ancho útil del lateral (226px de ventana y 208px por tarjeta) y muestra las
previews en una columna vertical. Arriba crece hacia abajo y Abajo hacia arriba; las muchas notas
usan scroll vertical. El pie de acciones queda junto al canto físico, con una separación compacta de
la primera/última tarjeta, y la barra de reposo conserva sus guiones en horizontal.

Tests: 390/390. Build correcto, 0 errores y 0 advertencias nuevas. El portable se ha regenerado y
abierto desde `publish/portable/aldune.exe` para la validación visual manual.

## Guion final recortado en los docks superior e inferior (sesión 2026-09-12)

Al crear una nota con el dock en Arriba o Abajo, el último guion de color podía quedar cortado:
la barra de reposo se medía contra los 208px interiores de las tarjetas, aunque necesitaba el ancho
completo de la ventana. `RestStrip` usa ahora los 226px completos y la geometría queda cubierta por
una prueba específica para ambos bordes.

Tests: 392/392. Portable republicado y relanzado desde `publish/portable/aldune.exe`.

## Tira superior/inferior para muchas notas y refresco del gestor (sesión 2026-09-12)

Con diez notas, la tira horizontal seguía usando guiones del tamaño lateral y solo mostraba seis
completos más otro recortado. Arriba y abajo usan ahora guiones compactos, calculados con su medida
real, para mostrar diez completos dentro de los 226px disponibles. Los laterales mantienen sus
guiones anteriores.

`NotesManagerWindow` expone una recarga desde repositorio y `AppCoordinator.RefreshAll()` la llama
junto con los docks. Crear, editar, archivar, restaurar o eliminar una nota desde otra ventana ya
actualiza el gestor si está abierto.

Tests: 394/394. Build correcto. Portable republicado y abierto desde `publish/portable/aldune.exe`.

La verificación visual posterior mostró que la barra todavía quedaba limitada por el `ContentGrid`
interior de 208px y los guiones se alineaban a la izquierda. Se separaron ambos espacios: el
`ContentGrid` ocupa ahora los 226px de la ventana y `FanPanel` conserva el margen interior de 9px;
la tira horizontal se centra dentro de toda la ventana.

Tests: 394/394. Portable republicado y abierto de nuevo para validación manual.

## Dock superior/inferior adaptativo y selección múltiple en Gestionar notas (sesión 2026-09-12)

El ancho del dock superior/inferior dejó de ser fijo: crece con la tira de notas, igual que el dock
lateral crece en vertical, con un límite para no ocupar toda la pantalla. Las tarjetas siguen
conservando sus 208px útiles y los guiones mantienen su tamaño legible.

En `Gestionar notas`, la fila completa alterna la selección con un clic. `Shift` selecciona un rango
entre filas y `Ctrl+Shift` añade otro rango a la selección existente. La casilla `Seleccionar todo`
sigue disponible.

Tests: 396/396. Build correcto. Portable republicado y abierto desde `publish/portable/aldune.exe`.

## Transporte WebDAV para sincronización (sesión 2026-09-13)

Aldune ya permite elegir WebDAV/Nextcloud como transporte independiente de la carpeta compartida y
del servidor Aldune. Usa `PROPFIND`, `MKCOL`, `GET`, `PUT` y `DELETE` sobre sobres cifrados por nota.
La URL puede viajar en una invitación de perfil, pero el usuario y la contraseña de aplicación se
configuran por dispositivo; la contraseña queda protegida localmente.

Tests: 428/428. Pendiente: probarlo contra una instalación real de Nextcloud/ownCloud.

## Invitaciones de perfiles de sincronización (sesión 2026-09-13)

Los códigos de perfil pasan a v2 y pueden incluir el nombre del vínculo y la URL del servidor propio
para que la importación sea guiada. El token de acceso y el contenido de las notas nunca se incluyen.
Los códigos v1 existentes siguen siendo válidos. Al importar, Aldune actualiza el transporte y la URL
del servidor y deja el token para introducirlo localmente.

Build correcto. Tests: 426/426. Portable v0.5.0 republicado y abierto.

## Revocación segura de perfiles de sincronización (sesión 2026-09-13)

Ajustes incorpora `Revocar códigos anteriores`. La operación sincroniza antes de rotar la clave,
guarda una clave pendiente reanudable, vuelve a cifrar los sobres y limpia los objetos que ya no
pertenecen al ámbito. Los sobres re-cifrados reciben una versión posterior para que un dispositivo
con la clave antigua no pueda republicarlos. El servidor propio expone el borrado autenticado de
objetos que necesita esta operación.

La prueba de integración confirma que el código antiguo deja de sincronizar y que una invitación nueva
recupera el vínculo. Tests: 427/427.

## Revisión inicial de Gestionar notas (sesión 2026-09-13)

La selección de una fila ahora se distingue también por el borde y la opacidad de la propia nota,
no solo por la casilla. Las filas son enfocables y el doble clic abre la nota completa; el clic normal
conserva la selección para acciones en bloque.

Build correcto. Tests: 425/425. Se ha generado la versión actualizada en
`publish/portable-next/aldune.exe`; la carpeta `publish/portable` no se puede reemplazar mientras
sus dos instancias sigan abiertas.

## Notas contenidas en el monitor y barra de desplazamiento (sesión 2026-09-13)

Las notas recalculan sus límites de ancho y alto según el área de trabajo del monitor actual. Si
crecen cerca del borde inferior, se recolocan dentro de la pantalla; cuando el contenido supera el
alto permitido, el cuerpo mantiene el desplazamiento vertical en vez de dejar salir la ventana.
También se desactiva el desplazamiento horizontal para que el texto se adapte al ancho disponible.

La barra global gana contraste sobre las notas de color, con un pulgar redondeado, borde sutil y
estados diferenciados al pasar el ratón o arrastrar.

Tests: 410/410. Build correcto. Portable republicado y abierto desde `publish/portable/aldune.exe`.

## Atajo de búsqueda en Gestionar notas (sesión 2026-09-13)

`Ctrl+F` enfoca el campo de búsqueda de Gestionar notas y selecciona el texto actual para poder
reemplazarlo directamente. La ayuda rápida de Ajustes también lo documenta en español e inglés.

Tests: 410/410. Build correcto. Portable republicado y abierto desde `publish/portable/aldune.exe`.

## Pulido de ventanas y color libre de nota (sesión 2026-09-12)

Se afinó la respuesta visual del dock y de las ventanas: el cierre del dock espera 90 ms y las
transiciones de entrada/cambio quedan alrededor de 150 ms, conservando el ease-out. `NotesManagerWindow`
ahora tiene algo más de ancho útil, redondeo de píxeles y no muestra el rectángulo de foco azul en
sus controles de chrome.

El marcador `Title` se oculta mientras el campo tiene el foco para que el caret no atraviese la
primera letra. El botón `⋯` captura el segundo clic antes del autocierre del `Popup`, por lo que
ahora abre y cierra de forma determinista.

El menú de acciones de cada nota mantiene las seis pastillas rápidas y añade **Elegir otro color…**
mediante el selector nativo. Los colores personalizados se guardan como RGB hexadecimal y se
rechazan si no alcanzan contraste suficiente con la tinta de la nota.

## Conflictos de sincronización validados (sesión 2026-09-12)

Se añadieron pruebas de extremo a extremo con dos bases de datos, dos identificadores de dispositivo
y una carpeta compartida: edición concurrente con convergencia a la versión más reciente, borrado
posterior a una edición antigua y desempate estable del tombstone. También se corrigió el sobre de
borrado para publicar el identificador real del dispositivo, en vez del valor interno `local`.

Build correcto. Tests: 403/403.

## HTTPS del servidor autohosteable preparado (sesión 2026-09-12)

Se añadió `docker-compose.sync.https.yml` junto con `Caddyfile`. Caddy publica únicamente los
puertos 80/443, obtiene y renueva el certificado del dominio configurado y reenvía al servicio
`fanote-sync` por la red interna de Docker. El puerto HTTP directo no se publica en esta variante.

La composición se validó con `docker compose config`. Falta la prueba de despliegue real, porque
requiere un dominio/DNS y acceso al servidor del usuario.

## Rotación de tokens del servidor (sesión 2026-09-12)

El servidor mantiene compatibilidad con `FANOTE_SYNC_TOKEN` y añade `FANOTE_SYNC_TOKENS`, una lista
separada por comas con prioridad sobre el valor antiguo. La ventana de solapamiento permite migrar
los clientes uno a uno; al eliminar el token viejo de la lista queda revocado. La comparación usa
bytes en tiempo constante y no se añadió ningún endpoint de administración.

Build correcto. Tests: 410/410.

## Interfaz de conflictos de sincronización (sesión 2026-09-12)

Los conflictos guardan ahora la versión perdedora cifrada en la base local. Ajustes muestra el
número pendiente y abre `SyncConflictsWindow`, donde se puede restaurar la versión perdedora o
descartar el registro. Solo queda un conflicto visible por nota y restaurar una versión le asigna
una marca temporal nueva para que la decisión se publique en la siguiente sincronización.

Build correcto. Tests: 410/410.

## Modo simplificado de Ajustes (sesión 2026-09-12)

Se añadió `AppSettings.SimplifiedMode` y un selector persistente en Ajustes. El modo simplificado
oculta el bloque de opciones avanzadas, pero mantiene accesibles el cambio de modo, la versión y la
salida de la aplicación. El cambio se aplica al instante y no altera notas ni el resto de valores
guardados.

Build correcto. Tests: 400/400. Portable v0.5.0 publicada.

## Esquinas superiores del dock (sesión 2026-09-12)

Las tarjetas del dock superior redondean ahora también sus esquinas superiores, manteniendo el dock
inferior espejado con las esquinas interiores redondeadas.

La validación visual mostró un último desajuste: la pastilla negra estaba usando el ancho adaptativo
completo de la ventana, en lugar de ceñirse a los guiones que contiene, y el margen del último guion
dejaba los lados desiguales. La ventana sigue creciendo para las tarjetas, pero la pastilla ahora
usa solo el ancho de su contenido, descuenta el margen final y se centra.

Tests: 396/396. Portable republicado y abierto de nuevo.

## Nitidez de la lista al añadir notas (sesión 2026-09-12)

La pila podía verse borrosa durante las inserciones porque la tarjeta nueva se desplazaba con un
`TranslateTransform` mientras el resto se recolocaba con pasos fraccionarios. Se mantiene el fundido
de entrada, pero las inserciones nuevas ya no se trasladan; el desplazamiento animado se conserva al
abrir el abanico. `EdgeDockWindow` activa además redondeo de layout, píxeles y formato de texto de
display para que las tarjetas se dibujen nítidas.

Tests: 396/396. Build correcto. Portable republicado y abierto desde `publish/portable/aldune.exe`.

## Botones del dock ligados a la etiqueta y descarte masivo de conflictos (sesión 2026-09-15)

Los botones del pie del dock se comportan ahora dentro de la vista en curso. En la vista de una
etiqueta: `+` crea la nota ya etiquetada con ella, "abrir todas" abre y cierra solo las notas de esa
etiqueta y "gestionar" abre el gestor con ese filtro aplicado, también si ya estaba abierto. La
sincronización sigue siendo global, igual que "Cerrar todas" del menú contextual. Los tooltips del
pie cambian con la vista para que se vea qué va a hacer cada botón antes de pulsarlo.

La ventana de conflictos añade "Descartar todo", con confirmación, para vaciar la cola entera de una
vez (`SyncService.DismissAllConflicts` -> `NotesRepository.DeleteAllSyncConflicts`). Descartar no toca
las notas: solo borra los registros de la cola de recuperación, así que la versión ganadora sigue
siendo la activa en cada una.

Tests: 434/434. Build correcto. Portable v0.6.0 publicada.

## Posición del mazo sincronizada (sesión 2026-09-15)

El orden de las pestañas dentro del dock viaja en el sobre cifrado (`Note.DockPosition`, formato de
sincronización 4). Arrastrar una nota refresca su `UpdatedAt` para que el sobre se vuelva a publicar;
sin eso el otro dispositivo conservaría el orden antiguo. Al aplicar un sobre con posición se guarda
en `NoteOrder`, y si el sobre no la trae (versión anterior) se conserva el orden local en vez de
perderlo. `ApplySyncTombstone` también borra `NoteOrder`. La posición de la ventana en pantalla sigue
siendo local: un mismo escritorio no significa nada en monitores distintos, así que solo viaja el
orden del mazo. Las versiones anteriores al formato 4 rechazan los sobres nuevos con un error claro;
todos los dispositivos tienen que usar esta versión para seguir sincronizando.

## Correcciones del dock y de la ventana de conflictos (sesión 2026-09-15)

**Los botones de la cabecera de conflictos no siempre respondían.** La franja superior de esa ventana
es zona de arrastre (`WindowChrome CaptionHeight="34"`), así que el clic sobre los botones nuevos caía
en el arrastre y no en el botón: solo funcionaba el trozo que sobresalía por debajo de la franja, de
ahí el "a veces". Se marcan con `WindowChrome.IsHitTestVisibleInChrome="True"`, igual que ya hacían
`NoteWindow`, `SettingsWindow` y el gestor. Además la ventana se activa al abrirse
(`NativeMethods.ForceActivate`), porque los docks y las notas son `Topmost` y podían dejarla detrás.
El aviso de la cabecera reserva ahora el ancho de los botones, que antes se le echaban encima.

**"Abrir todas" abría en la pantalla equivocada.** Usaba `DockNearCursor()`, y el cursor casi nunca
está sobre el dock que se acaba de pulsar: con un dock en cada monitor, pulsar el botón del dock
principal abría las notas en el vertical. Ahora `OpenAllNotes` y `ToggleAllNotes` reciben el dock que
pidió la acción, así que abrir (y el "Normal cascade" del menú) ocurre en la pantalla de ese dock.
El interruptor sigue cerrando las notas de la vista en curso, estén donde estén, y "Cerrar todas" del
menú contextual sigue siendo global.


## Disposiciones y "restaurar posiciones" en la pantalla del dock (sesión 2026-09-16)

El menú de "Abrir todas" del dock tiene una opción nueva, **"Restaurar posiciones originales"**
(`AppCoordinator.RestoreNotePositions`), debajo del separador y junto a "Cerrar todas las notas":
devuelve cada nota abierta a la posición y el tamaño que tenía guardados, sin tener que cerrarlas y
reabrirlas una a una para deshacer un reparto en cuadrícula o columnas.

Tanto esa opción como las tres disposiciones (cascada, cuadrícula, columnas) actúan **en la pantalla
del dock que se pulsa**, con el destino resuelto por `MonitorLookup.TargetOrFallback`: la pantalla
elegida si se eligió una y sigue conectada, y si no la de ese dock. `ArrangeOpenNotes` ya no agrupa las
ventanas por el monitor en el que estuvieran —con notas en dos pantallas, "cuadrícula" dejaba dos
cuadrículas de una columna— sino que reparte todas en la pantalla de destino. Al abrir en otra pantalla,
la nota nace ya centrada en ella en vez de asomar por la del dock (`AppCoordinator.CenterNoteOnMonitor`).

Con más de una pantalla y alguna sin dock (`AppCoordinator.DockCount`), el menú enseña además un
selector "Abrir las notas en" con una entrada por pantalla (`Strings.ScreenLabel`, la misma etiqueta
que Ajustes). Sin él no habría forma de pedir una pantalla donde no hay dock que pulsar: con un dock en
cada pantalla la elige el propio dock, y por eso el selector solo aparece cuando alguna se queda sin
él. La elegida va marcada con ✓ y el clic no cierra el menú, para poder elegir pantalla primero y
disposición después.

Las notas vuelven a su sitio con la posición recordada en esa pantalla (la tabla `NotePlacement` que ya
usa `TryRestorePlacement` al abrir una nota, así que no hay estado nuevo que mantener ni que
sincronizar), validada contra ESA pantalla y no contra todas: una posición recordada en el monitor que
se acaba de desenchufar no sirve para devolver la nota a esta. Las que no tengan ninguna —o cuya
posición ya no caiga dentro, porque la pantalla cambió de resolución o de sitio, o porque "recordar la
última posición" está desactivado— vuelven al reparto inicial de siempre, la cascada: antes se quedaban
donde estaban y la opción parecía no hacer nada.

Los movimientos del coordinador van marcados (`NoteWindow._isLayoutMove`) para que `OnLocationChanged`
no reacote la ventana contra el monitor de turno ni guarde la posición a mitad de camino: al cruzar de
pantalla, la posición intermedia se guardaba como si fuera la elegida por el usuario, y "restaurar
posiciones originales" la tomaba por buena después. El flag se suelta al terminar la animación y lo
limpia el arrastre del usuario al empezar, que es el único otro camino que mueve la ventana.

Tests: 441/441 (seis nuevos en `MonitorLookupTests`). Build correcto.

## Símbolos de depuración fuera del publish, y la trampa del `-p:DebugType=None` (sesión 2026-09-16)

`scripts/build-installer.ps1` apagaba los símbolos con `-p:DebugSymbols=false -p:DebugType=None` en la
línea de comandos. Una propiedad global de MSBuild se aplica también a los proyectos referenciados, así
que `Aldune.Core` se compilaba en Release sin `.pdb` y su copia de `bin\Release\net10.0` desaparecía.
Como la compilación de Core quedaba "al día", el `.pdb` no se regeneraba, y el siguiente
`dotnet test -c Release` fallaba con `MSB3030: Could not copy the file ...\Aldune.Core.pdb` al
intentar copiarlo. Ese ajuste vive ahora en `Aldune.csproj` (solo Release y solo para el proyecto de
la app, que es el único que se publica), donde no alcanza a Core. El instalador sigue generando el
mismo `aldune.exe` sin `.pdb` al lado.

Tests: 435/435. Build correcto. Instalador y portable generados en `dist/`.

## El dock aguanta un margen tras cambiar de vista o etiqueta (sesión 2026-09-16)

Cambiar de vista o de etiqueta es ponerse a buscar una nota, no cerrar el dock, pero era justo cuando
más brusco se quedaba: mientras el selector está abierto el sondeo de hover no decide nada, y al
cerrarse ve el cursor fuera de la zona sensible —que además acaba de cambiar de tamaño con el nuevo
recuento de notas— y pliega el dock al instante.

Ahora esas acciones arman un margen de cortesía de 4 segundos (`EdgeDockWindow.InteractionGrace` /
`HoldOpenForNextInteraction`): se arma al abrir el selector y se renueva al elegir vista o etiqueta.
Durante el margen el sondeo no decide nada (igual que con un popup abierto) y el abanico se mantiene
desplegado; al caducar manda la comprobación normal de dentro/fuera, así que si el cursor volvió a
pasar por encima no cambia nada, y si sigue fuera el dock se pliega con el pliegue de siempre.

Es una cortesía puntual por acción, no un modo: el ajuste "mantener el dock abierto"
(`AppSettings.KeepDockOpen`) sigue mandando cuando está activo. El mecanismo es genérico — cualquier
otro botón que invite a una interacción consecutiva solo tiene que llamar a
`HoldOpenForNextInteraction()`.

De paso, el `PlacementTarget` del selector apuntaba a un `DockViewButton` que no existe en el XAML
(error de binding silencioso): ahora apunta a `ManageArchiveButton`, el botón de verdad al que
`OpenDockViewPopup` ya reasignaba el destino por código.

Tests: 441/441. Build correcto. Portable nuevo probado arrancando.

## Feedback al sincronizar desde el dock (sesión 2026-09-16)

El botón de sincronizar del dock no decía nada al terminar: solo había aviso cuando fallaba, así que
un clic sin respuesta visible no permitía saber si sincronizó de verdad.

Ahora, mientras corre, la flecha gira y el botón se desactiva (una segunda sincronización a la vez no
aporta nada); al terminar, el botón enseña un ✓ verde o un aviso rojo durante un par de segundos, con
el resultado en el tooltip ("Sincronización completada: X enviadas, Y recibidas", el mismo texto que
Ajustes) y su tooltip de siempre restaurado después. El aviso con detalles del error sigue existiendo
para los fallos, igual que antes.

## El dock no se pliega mientras sincroniza, y las notas siguen a la vista (sesión 2026-09-16)

Dos cambios pedidos por el uso diario:

**Sincronizar sin que el dock se pliegue.** Hasta que la sincronización del botón del dock no termina
—y no se ve su resultado— el dock no se pliega: el sondeo de hover también respeta la marca
`_syncBusy`, así que se mantiene desplegado aunque el cursor ya no esté encima. Al terminar, el
resultado conocido (✓ verde o aviso rojo durante unos segundos) arma de nuevo el margen de cortesía
para que se pueda leer, y después decide el sondeo normal.

**Cambiar de vista cierra lo que ya no toca.** `AppCoordinator.SetDockView` cierra automáticamente las
notas abiertas que no pertenecen a la nueva vista (`CloseNotesOutsideCurrentView`, contra
`NotesForCurrentDockView`): cambiar de etiqueta —o de vista— es cambiar de mesa de trabajo, y una
ventana abierta de otra etiqueta se quedaba en la pantalla representando un filtro que el dock ya no
aplicaba. Cerrar guarda la nota, no pierde nada.

Tests: 441/441. Build correcto. Portable nuevo probado arrancando.


## Margen del dock tras crear una nota o cambiar de etiqueta (sesión 2026-09-16)

El margen de cortesía de 4 segundos (`HoldOpenForNextInteraction`) existía pero ya no se armaba en dos
de sus casos originales: `CreateNote` y las elecciones de vista/etiqueta no lo llamaban, y
`OnDockViewPopupClosed` además lo borraba (`_interactionGraceUntil = DateTime.MinValue`) al cerrarse el
selector — justo cuando el usuario va a usar el dock. Ahora elegir vista/etiqueta lo arma antes de
cerrar el selector (el selector sí se cierra; lo que aguanta es el dock), crear una nota lo arma tras
`RefreshAll`, y el cierre del selector ya no lo destruye. `HoldOpenForNextInteraction` fija también
`_pointerInside` y limpia `_hoverLayoutHold` para que el sondeo no lo pille a mitad de la transición.

## Colores oscuros con texto adaptable (sesión 2026-09-16)

El editor bloqueaba cualquier color cuyo contraste con la tinta oscura fija no llegase a AA, así que
no se podía poner una nota oscura. Ahora `NoteColorContrast` (Core, WCAG con luminancia linealizada)
elige el texto: tinta cálida sobre claros, blanco sobre oscuros, y negro en la franja intermedia donde
tinta y blanco se quedan bajo 4.5:1. Se aplica a cuerpo, título, caret, selección, placeholders,
controles de cabecera y vista previa del editor de color; el aviso del dock (`LabelFor`) también se
adapta, conservando los tonos de la paleta pastel. Los errores del editor ahora solo validan HEX.

## Aviso de nuevas versiones (sesión 2026-09-16)

`ReleaseUpdateChecker` (Core) consulta `api.github.com/repos/NachoPola16/aldune/releases/latest` con
User-Agent y cabeceras de API propias; solo acepta tags numéricos estables (`vX.Y.Z`), descarta
draft/prerelease, y nunca usa `html_url` ni assets del JSON: construye la URL de la página de la
release con el tag validado. `UpdateNotifier` (Windowing) sondea 20 s después de arrancar y luego cada
24 h, avisa con una ventana modeless que no roba el foco (Descargar / Más tarde; Esc cierra) y muestra
feedback en las comprobaciones manuales del menú de la bandeja ("Buscar actualizaciones…"). Nunca
instala nada. Recordado por versión durante la sesión. Versión subida a **0.9.0** (función nueva) y
documentación de versión actualizada.

Tests: 506/506 unitarios más la prueba WPF de humo (`tests/Aldune.Ui.SmokeTests`, escenario real
popup → elección de etiqueta → `Closed` → gracia de 4 s → caducidad con el cursor fuera; PASS).
Build correcto. Portable nuevo probado arrancando.

## Hover del dock: cortesía al abrir ventanas y zona de reposo más generosa (sesión 2026-09-18)

Dos fallos de puntería que hacían el dock "soso" justo al usarlo deprisa:

1. **Pulsar un botón del pie nada más abrir el dock no respondía.** Si el cursor llegaba a
   "Gestionar notas" (o a ajustes/color/protección) antes de que el abanico terminara de
   desplegarse, la ventana abierta robaba el foco y el cursor quedaba sobre un hueco que
   `PollHoverState` interpretaba como salida: el dock se plegaba bajo los pies y, con
   `_hoverReentryBlocked` armado, dejaba de responder hasta sacar el ratón lejos. Arreglo:
   `EdgeDockWindow.HoldOpenForWindow()` arma una cortesía de 3 s (`HoverOpenGrace`) que congela el
   estado de hover, limpia `_hoverReentryBlocked` y mantiene el abanico abierto mientras la nueva
   ventana toma el foco. La llaman los clics que abren ventana desde el dock
   (`OnManageArchiveClick`, `OnSyncRightClick`, `OnTabMenuCustomColorClick`,
   `OnTabMenuProtectionClick`).

2. **Al volver a abrir el dock con varias notas ya abiertas se cerraba solo.** En reposo la zona
   sensible era la tira de guiones, de unos 12 px. Con todas las notas abiertas tapando el canto, el
   cursor casi nunca cae justo encima al reabrir: el primer sondeo lo daba por fuera y lo plegaba.
   Arreglo: `EdgeGeometry.RestingVisibleRect` se ensancha `RestHitSlop` (12 px) por cada lado, sin
   salirse de la ventana. El hit-test de WPF sigue sin cubrir el hueco transparente (eso no
   cambia); es solo el sondeo de hover el que perdona la puntería.

Los tests que fijaban el grosor exacto de la tira se actualizan a la nueva banda
(`RestingVisibleRect_*`: ahora la zona es la tira + la holgura, nunca más estrecha).
507/507 en verde. Versión subida a **0.9.1**.

## Ronda de pulido de interacción (sesión 2026-09-18)

Ronda entera dedicada a interacción y pulido, pedida por el usuario como lista de puntos. El detalle
punto por punto, con la causa raíz de cada uno y las decisiones tomadas, está en `docs/ROADMAP.md`
→ "8. Ronda de pulido de interacción". Resumen de lo que cambió:

- **Ventanas propias al frente**: gestor de notas, Ajustes y conflictos pasan a `Topmost` y se abren con
  `AppCoordinator.RaiseAppWindow`, que aparta las notas de la capa superior mientras viven (el mecanismo
  del menú del dock, ahora con contador para que dos dueños no se pisen) y sube la ventana al frente.
- **Minimizar una nota** vuelve a poner su ficha en el mazo; antes la borraba del dock.
- **Selección de texto legible**: `ApplyColor` pintaba la selección con la tinta **opaca** (un bloque
  macizo del color del texto); ahora es un lavado translúcido.
- **Las notificaciones salen en la pantalla del origen**, no siempre en la primaria.
- **Aire entre la barra de scroll y el texto** de la nota, con un estilo implícito de `ScrollBar` en los
  recursos del `TextBox` (el `Padding` del control no habría servido: la barra vive dentro del
  `ScrollViewer` interno y el padding mueve texto y barra a la vez).
- **La casilla de tarea vacía ya no se marca** al clicar al lado: la zona de clic es exactamente la caja
  que se resalta al pasar el ratón.
- **Las plantillas reparten en orden del mazo**, así que la nota que cae en cada celda ya no parece
  aleatoria.
- **Autoscroll con clic central** (`AutoScrollManager`) en el abanico del dock, la lista del gestor y el
  cuerpo de la nota: el cursor manda y la vista se desplaza sola; otro clic lo apaga.
- **Los desplegables son interruptor** (`PopupToggle`): volver a pulsar el disparador cierra en vez de
  reabrir. Causa comprobada en la fuente de WPF: con `StaysOpen="False"` el popup toma la captura del
  ratón y se descarta en el mouse-down, así que el handler del disparador nunca ve `IsOpen=true`.
- **Menú "⋯" de la nota agrupado** en cuatro bloques por intención (escritura, recordatorio, estado de la
  nota, guardar fuera/destruir).
- **El menú de una pestaña del dock ya no se cierra solo** al alejar el ratón (fuera el timer de 550 ms).
- **"Cascada junto al dock"** (nueva) y "Restaurar posiciones originales" conservada como estaba.
- **Menú contextual de copiar/pegar/cortar** con la estética oscura, por estilos implícitos.
- **Ocultar el dock con `Ctrl+Alt+H`** y desde la bandeja, para las pantallas completas que Windows no
  reporta como tales.
- **Ajuste de gestos de trackpad** (`TrackpadGestures`), disponible solo si `TouchpadDetector` encuentra
  un trackpad de precisión. **No verificado en hardware**: el equipo de desarrollo no tiene trackpad.
- **Bug de monitores**: al apagar y encender una pantalla, el dock se quedaba en la otra. Ahora la
  reconstrucción continúa hasta que el conjunto de pantallas vuelve al de antes del cambio
  (`MonitorSignature`), en vez de parar a los 5 ticks. **Pendiente de verificar por el usuario.**

Versión subida a **0.10.0**. 507/507 tests y build correctos.

## Auditoría general + arreglos de nitidez y cascada (sesión 2026-09-22, bounded)

Auditoría de codebase pedida por el usuario (bugs, seguridad, visual/UX, calidad) vía un subagente de
exploración de solo lectura, seguida de una ronda de arreglos directos (brainstorming → implementación,
sin spec/plan formal). Detalle completo del análisis en la conversación; aquí solo lo que cambió y lo
que queda pendiente.

- **Menú contextual de copiar/pegar (y otros paneles flotantes) borrosos en monitores con escala >100%
  — ARREGLADO.** Causa raíz: `DropShadowEffect` aplicado al mismo `Border` que el contenido fuerza a
  WPF a rasterizar todo el subárbol (texto incluido) a un bitmap de resolución fija que luego se
  estira, emborronando el texto en monitores con escala DPI >100% (la app es `PerMonitorV2` aware,
  `app.manifest`). Arreglo aplicado al `ContextMenu`/`ToolTip` implícitos de `App.xaml` y a los 5
  popups con el mismo patrón en `NoteWindow.xaml` (ActionsPopup) y `EdgeDockWindow.xaml`
  (NewNoteMenuPopup, OpenAllMenuPopup, DockViewPopup, TabMenuPopup): separar la sombra en una capa sin
  contenido (un `Border` de fondo con el `Effect`, sin hijos) de la capa de contenido (mismo `Border`
  encima, sin `Effect`) dentro de un `Grid`. **Pendiente, mismo patrón sin tocar todavía**: `CardShadow`/
  `CardShadowTop`/`CardShadowBottom`/`ButtonShadow` en `EdgeDockWindow.xaml` (tarjetas del abanico y
  botones circulares del dock) tienen el mismo problema pero el `Effect` va directo en el `Grid`/
  `Ellipse` que ya tiene estructura propia (con `TemplateBinding`, triggers) — separar la capa ahí es
  más invasivo y se dejó fuera de esta ronda a propósito.
- **"Desplegar todas" volvía a cascada tras mover una nota a mano — ARREGLADO.** La disposición elegida
  se guarda como un único valor global (`AppSettings.DefaultNoteLayout`), y nada invalidaba ese valor
  al arrastrar una nota. Ahora `NoteWindow.OnGripMouseDown` llama a `AppCoordinator.NoteMovedManually()`,
  que vuelve el ajuste a `Normal` si había una plantilla automática activa (cascada, cuadrícula,
  columnas) — la próxima vez que se pulse "Desplegar todas" ya no sobrescribe la posición manual.
- **Notas completamente ocultas en cascada a partir de la sexta — ARREGLADO.** `NoteCascade.Position`
  clampaba el nivel de escalonado a un tope fijo (`MaxLevels=5`) sin tener en cuenta el tamaño del
  mazo ni de la pantalla: a partir del sexto nivel todas las notas caían en las mismas coordenadas,
  tapándose del todo. Ahora el paso entre notas (`StepFor`) se comprime según el hueco real disponible
  en pantalla para el mazo completo, con un piso de `MinStep=18` — nunca menos, así siempre asoma algo
  de cada nota; con pocas notas el paso sigue siendo el natural (`Step=32`). Mismo criterio que ya usa
  `EdgeGeometry.PitchFor` para comprimir el paso entre pestañas. `NoteCascadeTests` reescrito: se quitó
  el test que fijaba el clamp a `MaxLevels` (ya no existe) y se añadió uno que reproduce el bug directo
  (`ManyNotes_NeverLandOnTheExactSamePosition`, 20 notas, ninguna posición repetida).
- **Seguridad**: revisión general sin hallazgos (cifrado AES-GCM + DPAPI correctos, sin
  `eval`/`innerHTML`/deserialización insegura, rutas de sync con `{id:guid}`). El punto pendiente de la
  ronda anterior (URI de descarga pasada a `Process.Start` en `UpdateNotifier.cs`) se verificó a fondo:
  `ReleaseUpdateChecker.CheckAsync` nunca usa `html_url` ni assets del JSON de la API de GitHub —
  construye la URL con un prefijo HTTPS fijo (`https://github.com/NachoPola16/aldune/releases/tag/`)
  más un tag validado por `TryParseVersion` (solo dígitos y puntos, sin separadores de ruta posibles).
  No es una URI controlable por el atacante; sin cambios necesarios. El otro `Process.Start`
  (`SettingsWindow.OnRestartClick`) solo relanza el propio ejecutable vía `Environment.ProcessPath`,
  también sin entrada de usuario. Sin hallazgos de seguridad pendientes.
- **Accesibilidad — ARREGLADO.** Ningún botón/control de solo icono tenía `AutomationProperties.Name`,
  así que un lector de pantalla no anunciaba nada útil al enfocarlos (o leía el glifo Unicode crudo).
  Añadido `AutomationProperties.Name` con el mismo texto que el `ToolTip` ya existente en ~20 controles
  de `EdgeDockWindow.xaml`, `NoteWindow.xaml`, `SettingsWindow.xaml`, `NotesManagerWindow.xaml` y
  `CustomColorWindow.xaml` (trabajo mecánico, vía subagente `mecanico`); dos botones de cerrar que ni
  siquiera tenían `ToolTip` (`NotesManagerWindow` y `SettingsWindow`) recibieron ambos atributos.
  **No tocado a propósito**: `FocusVisualStyle="{x:Null}"` en `ContextMenu`/`MenuItem` sigue quitando el
  anillo de foco por teclado — es una decisión estética explícita de rondas anteriores, cambiarla es un
  trade-off visual que no venía pedido en esta ronda.
- **Pendiente, sin tocar esta ronda — calidad de código**: `AppCoordinator.cs` (1026 líneas) concentra
  demasiada responsabilidad (abrir/cerrar notas, disposiciones, coordinar docks, ventanas de
  ajustes/gestor/sync); y existen dos algoritmos de cascada independientes — `NoteCascade` (para
  "Cascada junto al dock", arreglado arriba) y la cascada ad-hoc de `EdgeDockWindow.xaml.cs` al abrir
  una nota individual junto a su pestaña. Evaluado esta sesión: no es la misma necesidad (una coloca
  el mazo entero desde el dock, la otra una nota nueva relativa a su pestaña con clamp a pantalla
  propio) y unificarlas es un cambio de arquitectura, no un bug — se deja como deuda documentada en vez
  de forzarlo dentro de una ronda de bugs/pulido.
- **Tarjetas del abanico del dock (título/preview) también borrosas — ARREGLADO.** Mismo patrón que el
  menú de copiar/pegar: `CardShadow`/`CardShadowTop`/`CardShadowBottom` (`EdgeDockWindow.xaml`) iban en
  el mismo `Grid` (`CardRoot`) que el texto de cada pestaña. Separado en una capa `CardShadowLayer` sin
  contenido (su `CornerRadius` sigue el de `CardBorder` por binding, así que el espejado de
  `ApplyLeftEdgeTabShape`/`ApplyTopBottomTabShape` no necesitó tocarse) y el `Effect` (incluido el que
  cambia dinámicamente según el borde en `ApplyTopBottomTabShape`) se movió ahí. `ButtonShadow` (botones
  circulares del pie y tira de reposo) no necesitaba arreglo: ya estaba en una `Ellipse`/`Border` sin
  texto, separada del `ContentPresenter`/dashes de color.
- **Seguridad — sin cambios.** Revisado a fondo el único punto pendiente (URI de actualización) y
  confirmado que ya era seguro por construcción — ver más abajo.

Tests: 534/534 (`dotnet test tests/Aldune.Core.Tests`). Build limpio. Publicado como **0.10.5**.

## Cascada seguía reapareciendo tras mover una nota por la cabecera (sesión 2026-09-22, bounded)

Reportado por el usuario nada más instalar la 0.10.5: cascada junto al dock, mueve las notas a su gusto,
las cierra con el botón, vuelve a pulsarlo y reaparecen en cascada otra vez (con el dock en un borde
horizontal, cerca del centro de la pantalla, porque ahí es donde ese borde centra su ventana) — "no
quiero eso en ninguna situación".

**Causa real**: el arreglo de la ronda anterior (`NoteWindow.NoteMovedManually()` desactivando la
plantilla automática) solo se disparaba desde `OnGripMouseDown`, el asa explícita de la cabecera
(`DragHandle`, un icono pequeño arriba a la izquierda). Pero el resto de la cabecera se arrastra sola
como zona de "caption" implícita de `WindowChrome` — la forma natural y más común de mover una nota,
agarrándola por cualquier punto de la cabecera — y ese camino nunca pasa por `OnGripMouseDown`, así que
`DefaultNoteLayout` nunca se reseteaba a `Normal` si el usuario movía la nota así.

**Arreglo**: el aviso se movió a `NoteWindow.OnLocationChanged`, dentro de la rama que ya existía para
cuando el botón izquierdo sigue pulsado (`NativeMethods.IsLeftButtonDown()`) — ese es el único punto que
ve el arrastre en curso sea cual sea el mecanismo (asa con `DragMove()` o caption implícita de
`WindowChrome`), porque ambos generan los mismos eventos `LocationChanged` mientras el botón está
pulsado. Se quitó la llamada duplicada de `OnGripMouseDown`. `AppCoordinator.SetDefaultNoteLayout` ya no
toca disco una vez el ajuste está en `Normal` (comprobación previa), así que llamarlo en cada frame del
arrastre no genera escrituras de más — solo la primera vez que detecta que había una plantilla activa.

Tests: 534/534. Build limpio. Publicado como **0.10.6**, y el servidor de sincronización actualizado por
SSH tras el release (ver `docs/SYNC.md` para el procedimiento general — la configuración concreta del
servidor del usuario es intencionalmente privada y no vive en este repositorio).

## La cascada seguía apareciendo en el centro tras "Desplegar todas" (sesión 2026-09-22, bounded)

El usuario probó la 0.10.6 y el síntoma persistía: cascada junto al dock, mover las notas a mano, cerrar,
reabrir con el botón — vuelven a aparecer en una cascada diagonal, esta vez claramente **centrada en la
pantalla**, no cerca del dock. El arreglo anterior (mover el aviso de `NoteMovedManually` a
`OnLocationChanged`) era correcto pero insuficiente: no era la misma causa.

**Causa real**: `AppCoordinator.OpenAllNotes` — el método detrás del botón "Desplegar todas" — abre cada
nota (que ya cae en su posición recordada vía `TryRestorePlacement`, gracias a `arrangeAfterOpen: false`)
y **después, incondicionalmente, para cualquier plantilla incluida `Normal`**, encolaba una llamada a
`ArrangeOpenNotes`. Esa función, para `Normal`, ejecuta la rama `default` de
`ArrangeWindowsOnMonitor`: una cascada diagonal cuyo punto de partida se calcula centrado en el área de
trabajo (`startLeft`/`startTop` a partir de `(area.Width - totalWidth) / 2`) — exactamente la cascada
centrada de la captura. Es decir, **ignoraba a propósito la posición recién restaurada de cada nota** y
las reordenaba todas en diagonal, sin relación con dónde estaban antes de cerrarlas.

Este comportamiento tenía sentido para un camino de la UI que ya no existe: elegir "Normal" explícitamente
desde el menú del dock mientras había notas abiertas, para que tuviera "un efecto visible" (comentario
original). Ese botón de menú (`OnOpenAllNormalClick`) está en el code-behind pero no está enlazado a
ningún control del XAML actual — código muerto, ver `docs/ROADMAP.md`. El único camino real por el que
`layout == Normal` llega a `OpenAllNotes` hoy es tras un movimiento manual (el arreglo de la ronda
anterior), y ahí lo único correcto es no tocar nada: cada nota ya cayó donde tenía que caer dentro del
propio bucle de apertura.

**Arreglo**: `OpenAllNotes` ahora corta antes del `Dispatcher.BeginInvoke` cuando `layout == Normal` — ni
siquiera encola el reparto. Coincide con lo que ya hace el camino de abrir **una** nota suelta
(`OpenOrActivateNote`, línea con el comentario "Con la disposición Normal... no se toca nada"): antes
había dos caminos con el mismo nombre de plantilla y comportamiento contradictorio (uno respetaba la
posición, el otro la sobrescribía); ahora los dos respetan `Normal` de la misma forma. Grid/Columns/
DockCascade siguen llamando a `ArrangeOpenNotes` sin cambios, porque esas sí necesitan repartir todas las
notas a la vez.

Sin tests nuevos: `AppCoordinator`/`ArrangeOpenNotes` es lógica de ventanas WPF, fuera de
`Aldune.Core.Tests` (mismo patrón que el resto de `Windowing`). Verificado leyendo el camino completo
(`OpenAllNotes` → `ArrangeWindowsOnMonitor` → rama `default`) y contrastado con la captura de pantalla del
usuario, que mostraba el patrón de cascada centrada exacto que produce esa rama. Tests: 534/534
(Aldune.Core, sin cambios). Build limpio. Publicado como **0.10.7**.

