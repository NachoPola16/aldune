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

Tests: 80/80 pasando (`dotnet test` desde la raíz del repo).

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
  **Pendiente de verificación visual humana** (los subagentes no tienen
  ratón): que la animación escalonada realmente se repita al pasar el
  ratón, que las etiquetas se lean mejor con la pestaña más alta, y que
  los botones "+"/engranaje sean alcanzables en la práctica.

## Cómo seguir desde aquí

Rediseño de pestañas en abanico cerrado (build+tests limpios, revisión
final de rama sin hallazgos pendientes) — falta el pase de verificación
manual del usuario descrito arriba antes de darlo por completamente
cerrado. Sigue abierto elegir entre, para lo siguiente:

1. Sub-entrega 2 de la Fase 3 (ver prerrequisitos arriba): toggle de
   Ajustes para monitor único, IDs estables de dispositivo, hotplug en
   caliente — probablemente necesita su propio brainstorming (algunas
   piezas, como IDs estables, tocan el modelo de datos).
2. Modo "Papel vintage" (ver spec v1).

Si arrancas esto en una sesión/IA nueva: lee este archivo, la spec, y el plan
de la última fase fusionada, y sigue el mismo flujo de skills descrito arriba
(brainstorming → writing-plans → subagent-driven-development) para lo que sea
que decidas hacer a continuación.
