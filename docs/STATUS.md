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

Tests: 65/65 pasando (`dotnet test` desde la raíz del repo).

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
- **Pulido visual pendiente** (identificado mirando capturas de Hold My
  Notes/noty-sepia, no implementado todavía): el pill en reposo no debería
  ir a ras del borde de pantalla — mejor con un pequeño margen, esquinas
  redondeadas y sombra suave (ahora mismo es un rectángulo recto pegado al
  borde). La ventana de nota abierta debería tener el fondo teñido con el
  color pastel de la nota (como un post-it de verdad), no siempre blanco
  como ahora.
- **Modo "Papel vintage"** (textura de papel, inclinación por nota, tipografía
  manuscrita, sonidos): sigue tal cual estaba en la spec original, como
  paquete opcional aparte — no confundir con el pulido visual moderno de
  arriba, que es una cosa distinta y más prioritaria ahora mismo.

## Bugs visuales encontrados en la última prueba (sin arreglar todavía)

1. **El panel desplegado tiene tamaño fijo (`ExpandedThickness`/`ExpandedLength` en
   `EdgeGeometry.cs`) y corta las notas que no caben** en vez de mostrarlas
   todas o hacer scroll — con varias notas, las últimas quedan cortadas a la
   mitad. Hay que decidir: ¿el panel crece con el número de notas, o hace
   scroll dentro de un tamaño máximo?
2. **Durante el fotograma de la animación de despliegue se ve un glitch
   visual momentáneo** (contenido mal encajado un instante). El usuario
   sospecha que se arregla solo al resolver el punto 1 (si el contenido ya
   no se corta, puede que el frame intermedio deje de verse mal) — a
   confirmar una vez arreglado el punto 1, no asumir que ya está resuelto.

## Cómo seguir desde aquí

No hay una única "siguiente tarea" fijada — quedó abierto a elegir entre:

1. Terminar el pulido visual moderno pendiente (bordes redondeados/sombra del
   dock, ventana de nota teñida de su color).
2. Diseñar e implementar el botón "Archivadas" (reemplaza vista del dock).
3. Empezar la Fase 3 (multi-monitor + DPI) — leer los prerrequisitos de
   arriba antes de escribir el plan.

Si arrancas esto en una sesión/IA nueva: lee este archivo, la spec, y el plan
de la última fase fusionada, y sigue el mismo flujo de skills descrito arriba
(brainstorming → writing-plans → subagent-driven-development) para lo que sea
que decidas hacer a continuación.
