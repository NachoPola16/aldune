# Fanote — Estado del proyecto

Documento de continuidad: si retomas este proyecto en otra sesión, otro chat, u
otra IA, empieza por aquí. Todo lo importante vive en el repositorio (specs,
planes, commits), no solo en una conversación concreta.

## Qué es Fanote

App de notas para Windows (WPF/.NET 10), inspirada en Hold My Notes / Noty /
noty-sepia (macOS): notas ancladas al borde de la pantalla que se despliegan
en abanico al pasar el ratón por encima. Ver el diseño completo en
`docs/superpowers/specs/2026-08-30-fanote-v1-design.md` — ese documento es la
autoridad de diseño; todo lo demás (planes, código) se argumenta contra él.

## Qué hay hecho (fusionado en `master`)

- **Fase 1** (`docs/superpowers/plans/2026-08-30-fanote-phase1-window-mechanics.md`):
  mecánica de ventana — pill anclado al borde, hover con abanico, ventana de
  nota que activa el foco correctamente, animación respetando el ajuste de
  accesibilidad de Windows. Un solo monitor, notas falsas en memoria.
- **Fase 2** (`docs/superpowers/plans/2026-08-30-fanote-phase2-persistence.md`):
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
  presentación (`NoteTitleHelper.GetTitle`, en `Fanote.Core`, puramente
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
- **Fase 3a** (`docs/superpowers/plans/2026-09-02-fanote-phase3a-multimonitor-dpi.md`):
  primera sub-entrega de la Fase 3 — un `EdgeDockWindow` real por cada
  monitor conectado (antes solo el principal), con geometría y DPI reales
  de Win32 (`MonitorEnumerator`, `app.manifest` con `PerMonitorV2`), y un
  `AppCoordinator` a nivel de app para que las notas y la ventana de
  gestión no se dupliquen entre docks. Verificado contra los dos monitores
  reales del usuario (uno en vertical), incluido el checklist manual de
  interacción — encontró y arregló un bug real de refresco entre docks
  (ver historial más abajo) y, a raíz de probarlo, también se hizo que el
  tamaño del pill/panel se ajuste al número de notas en vez de ser fijo.

Tests: 130/130 pasando (`dotnet test` desde la raíz del repo).

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
  directamente lanzando `dotnet run --project src/Fanote` y siguiendo un
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

## Prerrequisitos para la Fase 3, resto por hacer (sub-entregas 2+)

Los puntos 1 y 2 originales (coordinador a nivel de app, geometría/DPI real
por Win32) ya están resueltos por la Fase 3a — ver el historial más abajo.
Queda:

1. **`ScreenOrigin` sigue fijo al literal `"primary"` en todas las notas.**
   Una sub-entrega futura tiene que decidir qué hacer con ese valor
   centinela frente a IDs de dispositivo reales y estables (Win32 hoy solo
   da `\\.\DISPLAY1`-style, que cambia al reconectar monitores — ver
   `MonitorInfo.DeviceName` en `Fanote.Core`, documentado ahí como "no es
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
   (paleta compartida, extraída a `Fanote.Windowing.NoteColorPalette` para
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
- **Selección en listas WPF**: `NoteRow` (`Fanote.Windowing`) envuelve cada
  `Note` con un `IsSelected` bindable (`INotifyPropertyChanged`) —
  deliberadamente fuera de `Fanote.Core`, la selección es un concepto de
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

Spec: `docs/superpowers/specs/2026-09-02-fanote-phase3a-multimonitor-dpi-design.md`.
Plan: `docs/superpowers/plans/2026-09-02-fanote-phase3a-multimonitor-dpi.md`.
Implementado siguiendo el plan tarea por tarea (5 tareas, 5 commits) mientras
el usuario estaba fuera — ver ese hueco de verificación abajo.

- **`Fanote.Core.MonitorInfo` + `DpiConversion`** (TDD): tipo de dato puro
  por monitor (nombre de dispositivo, área de trabajo, escala de DPI,
  si es el principal) y la conversión píxeles→DIP que necesita Win32
  (Win32 da píxeles físicos; `Window.Left/Top/Width/Height` de WPF son
  DIPs relativas al DPI de cada monitor una vez declarado `PerMonitorV2`).
  3 tests nuevos.
- **`Fanote.Interop.MonitorEnumerator`**: `EnumDisplayMonitors` +
  `GetMonitorInfoW` + `GetDpiForMonitor` (Win32 puro, sin dependencia
  nueva, mismo patrón que `NativeMethods.cs`). Si `GetDpiForMonitor` falla
  para un monitor, asume 96 DPI en vez de propagar el error. Verificado
  contra el hardware real del usuario (un volcado temporal a fichero, ya
  que no había forma de mostrar un `MessageBox` interactivo sin el
  usuario delante): detectó correctamente el monitor principal
  (2560×1440) y el secundario en vertical (1440×2560, con offset negativo
  respecto al principal).
- **`src/Fanote/app.manifest`** declarando `PerMonitorV2`, referenciado
  desde `Fanote.csproj` (`<ApplicationManifest>`). No existía ningún
  manifest antes — la app corría con el DPI-awareness por defecto de
  Windows para un proceso sin declarar.
- **`AppCoordinator`** (`Fanote.Windowing`, una instancia para toda la
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

Spec: `docs/superpowers/specs/2026-09-02-fanote-fan-tabs-redesign-design.md`.
Plan: `docs/superpowers/plans/2026-09-02-fanote-fan-tabs-redesign.md`.
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
- **`Fanote.Core.EdgeGeometry.ExpandedPerNoteLength`** subió de 40 a 88 —
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

Iterado en 4 rondas de maqueta (Artifact, no en el código real):
`https://claude.ai/code/artifact/25194ada-e021-4222-bc03-722f78250544`
(léela con `Artifact` acción `"read"` si retomas esto en otra sesión).
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
  Fanote nunca puede ser `AllowsTransparency` (rompe ClearType, ya
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

- `Fanote.Core.FullscreenDetection.CoversMonitor` (puro, con tests) compara la
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
ventanas visibles de Fanote pasan de 1 a 0 y vuelven a 1 al cerrarla. Y con una
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

### `FANOTE_MONITOR_INDEX`

Variable de entorno que restringe la app a un monitor (índice 0-based sobre el
orden de `MonitorEnumerator`; valor inválido se ignora). Nació de una
necesidad real — poder probar sin invadir la pantalla donde el usuario estaba
jugando — y es la pieza mínima del punto 3 de "Prerrequisitos para la Fase 3".
Cuando ese punto se aborde de verdad, debería pasar a `AppSettings`.

### Petición del usuario de esa ronda — ya implementada

Que el dock se esconda ante una ventana a pantalla completa: hecho en la cuarta
ronda, ver su sección más arriba.

## Cómo seguir desde aquí

Rama `dock-motion-shape` (sobre `worktree-fanote-fan-tabs-redesign`). Dos
rondas hechas: primero el modelo de movimiento (ventana fija + región
animada), después el de la pestaña como lomo de la nota. Verificado por
geometría y por render aislado, **pendiente de verificación manual**.

Checklist antes de darlo por bueno y fusionar:

1. En reposo se ve una tira corta de guiones de color, uno por nota, centrada
   en el borde. No debe haber ninguna franja gris ni banda muerta al final.
2. Todos los guiones responden al ratón, **incluido el último** (este fue un
   bug real: con 5 notas el quinto se veía y no se podía pulsar).
3. Al pasar el ratón, cada guión crece hasta ser su pestaña, escalonado y
   deslizándose hacia fuera. La última asienta en ~350ms como mucho.
4. Al salir, se cierra en orden inverso (primero la de más abajo).
5. Entrar y salir rápido varias veces no deja el dock a medio abrir.
6. La etiqueta vertical se lee (mayúsculas con tracking, en el tono oscuro de
   su propio color) y **no asoma en reposo**.
7. Se ve la línea de troquelado punteada cerca del borde derecho de cada
   pestaña.
8. Al hacer clic, la nota **se desliza hacia la izquierda** llevando su lomo
   por delante, alineada con la altura de su pestaña — y esa pestaña
   desaparece del mazo dejando el hueco.
9. El lomo de la nota abierta muestra la misma etiqueta y el mismo troquelado
   que tenía la pestaña.
10. Al cerrar la nota, su pestaña vuelve a su sitio.
11. Con más de 4 notas: hay scroll con la rueda en el desplegado, y ninguna
    pestaña fuera de vista deja un agujero de fondo suelto.
12. Los botones "+" y engranaje aparecen con la última pestaña y se pulsan.
13. Nada se rompe en el segundo monitor (lanzar sin `FANOTE_MONITOR_INDEX`).
14. Al abrir una nota, la animación **no se dibuja en el otro monitor** — este
    era el bug reportado con un juego a pantalla completa al lado.
15. Con un juego o un vídeo a pantalla completa delante, el dock desaparece de
    ese monitor y vuelve al salir. Con una ventana solo **maximizada** debe
    seguir viéndose.

Si algo falla, el sitio es `EdgeDockWindow.ApplyRegion` (forma por frame),
`TabRegionShape` (curva y escalonado, con tests) o `NoteWindow.SlideInFrom`.

Lo que ninguna de las verificaciones automáticas cubre es **cómo se siente**:
sobre todo si el deslizamiento se lee como "tirar de una ficha de un fichero"
o solo como que la nota aparece por la derecha.

Después de eso, sigue abierto elegir entre, para lo siguiente:

1. Sub-entrega 2 de la Fase 3 (ver prerrequisitos arriba): toggle de
   Ajustes para monitor único, IDs estables de dispositivo, hotplug en
   caliente — probablemente necesita su propio brainstorming (algunas
   piezas, como IDs estables, tocan el modelo de datos).
2. Modo "Papel vintage" (ver spec v1).

Si arrancas esto en una sesión/IA nueva: lee este archivo, la spec, y el plan
de la última fase fusionada, y sigue el mismo flujo de skills descrito arriba
(brainstorming → writing-plans → subagent-driven-development) para lo que sea
que decidas hacer a continuación.
