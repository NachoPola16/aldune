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

Tests: 140/140 pasando (`dotnet test` desde la raíz del repo).

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

### `FANOTE_MONITOR_INDEX`

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
- **Icono** (`src/Fanote/Assets/fanote.ico`, generado con un script de un solo
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

- **`Fanote.Core.HotkeyBinding`** (record con modificadores y tecla virtual).
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

`src/Fanote/Properties/PublishProfiles/portable.pubxml`, y se usa así:

    dotnet publish src/Fanote -p:PublishProfile=portable   →   publish/portable/Fanote.exe

Decisiones que no son obvias leyendo el fichero:

- **Perfil, no propiedades en el `.csproj`.** `RuntimeIdentifier` también afecta
  a `dotnet build` y `dotnet run`, así que meterlo en el proyecto ralentizaría
  cada compilación de desarrollo por algo que solo importa al publicar.
- **Autocontenido** (~82 MB): un portable que antes exige instalar el runtime de
  .NET no es portable.
- **Sin recorte (`PublishTrimmed=false`)**: WPF usa reflexión por todas partes y
  el recortador se lleva tipos que hacen falta. Fallaría al abrir una ventana, no
  al compilar, que es la peor forma de fallar.
- **Dos propiedades para un solo `.pdb`**: `DebugType=none` silencia a Fanote,
  pero el `.pdb` de Fanote.Core llegaba por otra vía — se copia como *fichero
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

- **`Fanote.Core.NoteSearch.Matches(texto, consulta)`**: en Core, no en la
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

Resuelve la incidencia donde Fanote se iba al fondo o desaparecía al interactuar con aplicaciones maximizadas (p. ej. seleccionar pestañas en Chrome), mantiene la ocultación ante videojuegos en pantalla completa real, y traslada el control de monitor de la variable de entorno a la UI de Ajustes.

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
   - `AppSettings.TargetMonitorIndex`: índice opcional para fijar Fanote en un monitor concreto (`null` = todas las pantallas conectadas).
   - `AppSettings.HideOnFullscreen`: opción booleana para habilitar/deshabilitar la ocultación ante videojuegos (por defecto `true`).
   - `SettingsWindow.xaml`: nueva sección visual "Pantallas donde mostrar Fanote" con selector de tarjetas estilizadas (`MonitorRadioStyle`) generado dinámicamente con las pantallas conectadas (nombre, resolución, indicación de monitor principal), y casilla para el auto-ocultado ante videojuegos.
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

3. **Hipótesis sobre la animación brusca tras inactividad**: Fanote vive casi siempre sin foco (nadie
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
   - `Fanote.Core.MonitorLookup.DeviceNameAt` (nuevo, con tests): dado un rectángulo, en qué monitor
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
hallazgo que la motiva: **el concepto de Fanote no existe en Windows**; las dos apps equivalentes
(Hold My Notes y noty) son solo macOS.

### Casillas de tarea (`Fanote.Core.TaskLines`, TDD)

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
cada arrastre. La lógica vive en `Fanote.Core.NoteOrdering` (pura, 16 tests).

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

- `Fanote.Core.NoteText.Split`/`Join` (puro, 16 tests, incluida la ida y vuelta y un test que
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

El propio usuario usa Fanote en español a diario. Traducir todo a inglés sin más se lo habría
quitado. Se preguntó explícitamente y se eligió: **selector Español/Inglés en Ajustes**, con
resolución en tres pasos —

1. `AppSettings.Language` (`"es"`/`"en"`/`null`). `null` = "sigue el idioma de Windows", y sigue
   así mientras el usuario nunca elija uno a mano: si `Language` es `null`, no se fija nada en el
   fichero, así que la app reacciona sola si el idioma de Windows cambiara entre arranques.
2. Elegir un idioma en Ajustes lo fija de forma explícita y permanente — mismo patrón que
   `TargetMonitorIndex`/`DockEdge`.
3. `App.OnStartup` resuelve y fija `Fanote.Resources.Strings.Current` **antes** de construir
   cualquier ventana. Se llama dos veces: una nada más entrar (adivinando por el idioma de Windows,
   por si el arranque falla antes de leer los ajustes de verdad — así hasta los mensajes de error
   más tempranos salen en el idioma que toca la mayoría de las veces) y otra en cuanto
   `settings.Language` está disponible de verdad.

**El cambio de idioma exige reiniciar Fanote para verse en todas las ventanas**, decisión explícita
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
   olvidarse del otro. `Fanote.Resources.Strings` (nuevo) es una clase con una propiedad estática
   por texto y **las dos versiones en la misma línea** (`T("English", "Español")`), así que no hay
   dos ficheros que se puedan desincronizar.

Encaja además con el estilo ya establecido del proyecto (P/Invoke a mano en vez de paquetes,
ensamblado del `.ico` por código en vez de herramientas externas): menos piezas moviéndose, más
control.

### El único hueco que queda a propósito

`Fanote.Core.HotkeyBinding.DisplayName` (el nombre del atajo, "Ctrl + Shift + N") se queda **sin
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
