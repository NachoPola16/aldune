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
  sección más abajo): vista combinada archivadas+papelera en el dock con
  etiqueta de estado por pestaña, "Restaurar" en `NoteWindow` para notas no
  activas, purga automática de la papelera a los 30 días
  (`NotesRepository.PurgeExpiredTrash`), y una ventana aparte
  (`NotesManagerWindow`) para archivar/restaurar/borrar en bloque con
  filtro por estado.

Tests: 71/71 pasando (`dotnet test` desde la raíz del repo).

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

## Prerrequisitos para la Fase 3 (multi-monitor + DPI) — detectados por la revisión final de la Fase 2

Importante leer esto antes de empezar la Fase 3, para no repetir bugs ya
arreglados una vez:

1. **`_openNoteWindows` (el diccionario que evita abrir la misma nota dos
   veces) y `NoteWindow._owner`/`Refresh()` viven hoy en `EdgeDockWindow`**,
   que es una instancia por monitor. Con multi-monitor habrá un dock por
   pantalla — si esto no se mueve a un coordinador a nivel de aplicación
   antes de crear el segundo dock, se reproduce exactamente el bug de
   "ventanas duplicadas" y "etiqueta desactualizada" que costó varias rondas
   arreglar en la Fase 2.
2. **`SystemParameters.WorkArea` solo devuelve el área de trabajo del
   monitor PRINCIPAL.** La Fase 3 necesita los límites reales de cada
   monitor vía Win32 (`MonitorFromWindow`/`GetMonitorInfo`), con DPI
   Per-Monitor V2 de verdad, no esta API.
3. **`ScreenOrigin` está fijo al literal `"primary"` en todas las notas
   creadas hasta ahora.** La Fase 3 tiene que decidir qué hacer con ese
   valor centinela frente a identificadores reales de dispositivo estables.

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
- **Ver notas archivadas/en papelera**: se pospone (no hay plan todavía),
  pero la dirección elegida SÍ está decidida: no una ventana nueva de
  lista+detalle (como hace noty-sepia con ⌥⌘A) — mejor un botón
  "Archivadas" que **reemplaza** temporalmente lo que se ve en el propio
  dock, reutilizando toda la lógica de pestañas que ya existe. Pendiente de
  diseñar/planificar cuando se retome.
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

## Idea pendiente de brainstorming (no iniciada): pestañas en abanico estilo Hold My Notes

El usuario compartió una captura de referencia (landing de una app tipo
Hold My Notes) con un patrón más ambicioso que el dock actual: en reposo,
un pill fino con un guión de color por nota (ya tenemos esto); al pasar el
ratón, las notas se despliegan como un abanico de pestañas *escalonadas*
por el borde, cada una con su color y una etiqueta de texto rotada
verticalmente (el nombre de la nota, no solo el color); al abrir una,
la nota se desliza a tamaño completo **desde la posición de su propia
pestaña**, no desde un panel genérico. Es una diferencia real de
interacción frente al `ItemsControl` vertical con botones que hay hoy —
cada nota necesitaría una posición/etiqueta estable y la animación de
apertura tendría que originarse en esa posición. No es una tarea
"bounded": si se retoma, empezar por `superpowers:brainstorming` en modo
completo (probablemente arquitectónico, toca cómo `EdgeDockWindow`
representa y anima cada nota) antes de tocar código.

## Botón "Archivadas" + gestor de notas (sesión 2026-09-02, bounded)

Implementado y verificado a mano. Resumen para no repetir decisiones si se
retoca:

- **Dock**: botón "Archivadas"/"Activas" (`EdgeDockWindow`, campo
  `_viewingArchive`) alterna `TabsList` entre notas activas y
  archivadas+papelera combinadas (dos `GetByState` + `Concat`, sin método
  nuevo de repositorio — no hace falta orden global para una vista de
  repaso). Cada pestaña muestra una etiqueta pequeña "Archivada"/"Papelera"
  (`NoteStateLabelConverter`, vacía y colapsada para notas activas —
  puramente derivada de `Note.State`, sin necesidad de saber en qué vista
  está el dock). `NoteWindow` muestra "Restaurar" en vez de
  Archivar/Papelera cuando `Note.State != Active`. Sin borrado permanente
  manual.
- **Purga automática de la papelera**: `NotesRepository.PurgeExpiredTrash`
  (TDD) borra notas en `Trashed` con `UpdatedAt` más viejo que
  `DefaultTrashRetentionDays` (30, decisión del usuario). Se usa
  `UpdatedAt` en vez de una columna `TrashedAt` nueva **a propósito**: esta
  app no tiene sistema de migraciones (`NotesDatabase` solo hace
  `CREATE TABLE IF NOT EXISTS`, una vez) — añadir una columna rompería las
  bases de datos ya existentes. Se ejecuta al arrancar (`App.xaml.cs`) y
  cada vez que se entra en la vista Archivadas del dock.
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

## Cómo seguir desde aquí

Sigue abierto elegir entre:

1. Empezar la Fase 3 (multi-monitor + DPI) — leer los prerrequisitos de
   arriba antes de escribir el plan.
2. Modo "Papel vintage" (ver spec v1).
3. Rediseño de pestañas en abanico estilo Hold My Notes (ver sección de
   arriba) — más ambicioso, necesita su propio brainstorming.

Si arrancas esto en una sesión/IA nueva: lee este archivo, la spec, y el plan
de la última fase fusionada, y sigue el mismo flujo de skills descrito arriba
(brainstorming → writing-plans → subagent-driven-development) para lo que sea
que decidas hacer a continuación.
