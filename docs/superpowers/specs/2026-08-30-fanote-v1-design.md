# Fanote v1 — Design Spec

## Contexto y objetivo

Fanote es una aplicación de notas para Windows inspirada en Hold My Notes,
Noty (aimen08/noty) y noty-sepia: notas ancladas al borde de la pantalla
que viven como una fina franja ("pill") y se despliegan en abanico al
pasar el ratón por encima. Ninguna de las referencias existe para
Windows — todas son apps nativas de macOS (Swift/SwiftUI).

Uso previsto: herramienta personal del autor para apuntar cosas rápido,
como un post-it real — sin gestión de fechas ni recordatorios, sin
cuenta ni sincronización. Objetivo secundario, no bloqueante para v1: si
el proyecto sale bien, publicarlo para que otros lo usen.

## Alcance de v1

### Núcleo (mecánica de ventana)

- Una franja fina anclada a un borde de pantalla (arriba/abajo/
  izquierda/derecha), que se despliega en abanico al pasar el ratón por
  encima.
- Clic en una pestaña del abanico → la nota se abre a tamaño completo en
  su sitio.
- Multi-monitor: la app puede mostrarse en todas las pantallas
  conectadas o solo en una, configurable en Ajustes.
- Posición de borde: por defecto la misma en todas las pantallas
  activas (casilla "misma posición en todas las pantallas", activada);
  si se desactiva, cada pantalla tiene su propio selector de borde
  independiente.
- La ventana es siempre-encima (always-on-top) y, fuera del área visible
  de la franja/pestañas, deja pasar los clics a lo que haya detrás
  (click-through).
- Consciente de DPI por monitor (Per-Monitor V2): cada pantalla puede
  tener un factor de escala distinto (p. ej. portátil a 150% + monitor
  externo a 100%); la app se re-renderiza nítida en cada una en vez de
  dejar que Windows estire un mapa de bits ya renderizado.
- Si se desconecta un monitor con notas ancladas a su franja, esas
  notas se reasignan automáticamente a la pantalla principal — nunca se
  pierden ni quedan inaccesibles.

### Contenido de la nota

- Texto libre, con checkboxes inline: una línea que empieza con `[ ]`
  se muestra como ☐ y, al hacer clic, pasa a `[x]`/☑. Es sintaxis
  dentro del propio campo de texto de la nota — no hay una tabla de
  tareas separada.
- Color de nota, elegible por el usuario.
- Fecha de creación y de última edición, guardadas como metadato (no se
  exponen como sistema de organización, solo como dato).
- Autoguardado silencioso: no hay botón "Guardar", el texto se persiste
  solo mientras se escribe (con un pequeño debounce para no escribir a
  disco en cada pulsación).
- Archivar: oculta la nota de la vista activa sin borrarla.
- Papelera: el borrado no es instantáneo ni definitivo; las notas
  borradas pasan a un estado "Papelera" recuperable. Archivado y
  papelera son el mismo mecanismo de estado (`Active` / `Archived` /
  `Trashed`), no dos sistemas distintos — solo cambia qué filtro de
  vista se aplica.

### Personalización

- Tema claro/oscuro automático, siguiendo el tema del sistema operativo
  (sin selector propio).
- Tipografía fija en el tema por defecto, elegida por legibilidad — sin
  selector de fuente/tamaño en v1.
- La app respeta el ajuste de accesibilidad de Windows "Mostrar
  animaciones en Windows" (Configuración → Accesibilidad → Efectos
  visuales): si está desactivado, las transiciones (abanico,
  apertura/cierre de nota) se aplican de forma instantánea en vez de
  animada. No existe un interruptor de "ahorro de batería" propio — las
  animaciones son demasiado ligeras para que tenga sentido un mecanismo
  dedicado, y el ajuste de accesibilidad ya cubre a quien quiera menos
  efectos por el motivo que sea.
- **Modo "Papel vintage"** (opcional, desactivado por defecto,
  activable en Ajustes; con un sub-interruptor para separar el efecto
  visual del sonido):
  - Visual: fondo con textura de papel envejecido/kraft y paleta cálida
    (sepia), ligera inclinación aleatoria por nota (como un post-it
    pegado a mano), tipografía tipo máquina de escribir/manuscrita,
    borde con aspecto de papel rasgado, icono de chincheta o cinta
    washi en la esquina de la nota.
  - Envejecimiento por antigüedad: las notas más viejas se ven algo más
    amarillentas/gastadas que las recientes, calculado a partir de la
    fecha de creación ya almacenada (no requiere ningún dato nuevo).
  - Sonido: un sonido suave de papel al abrir/cerrar una nota, y un
    sonido de "papel arrugado" al mandar una nota a la papelera
    (acompañado de una pequeña animación de arrugado). Los archivos de
    audio deben ser de un banco libre de derechos (p. ej. freesound.org
    con licencia CC0) — no se usa ningún asset con copyright.

### Almacenamiento

- Local, en SQLite. Contenido cifrado en reposo con una clave derivada
  vía DPAPI (ligada al usuario/equipo de Windows) — sin servidor, sin
  cuenta, sin telemetría.
- **Exportar/Importar** (para mover las notas a otro ordenador o hacer
  copia de seguridad): genera un archivo propio (`.fanotebackup`, un
  zip con JSON dentro) que contiene las notas **sin cifrar**. Es una
  decisión deliberada: la clave DPAPI está ligada a la máquina y no
  puede viajar con el archivo, así que cifrar el export exigiría pedir
  una contraseña al usuario — se descarta por fricción. En su lugar, se
  sigue el mismo patrón que usa Firefox para exportar marcadores
  (JSON sin cifrar por defecto, cifrado solo como extensión opcional de
  terceros): el archivo de export es responsabilidad del usuario
  mientras exista (no dejarlo en una carpeta compartida, moverlo a
  mano). El import valida el formato antes de tocar la base de datos
  real; si no es válido, se rechaza sin modificar nada de lo existente.
  Al importar, las notas se vuelven a cifrar con la clave del equipo de
  destino.

### Integración con Windows

- Icono en la bandeja del sistema, con menú: abrir todas las notas,
  ajustes, salir.
- Atajo de teclado global configurable para crear una nota nueva desde
  cualquier sitio.
- Inicio automático con Windows (activable/desactivable en Ajustes).

### Explícitamente fuera de v1

Búsqueda entre notas, etiquetas/carpetas, sincronización en la nube,
recordatorios o notas con fecha/notificación, editor de temas libre
(colores arbitrarios elegidos por el usuario), export cifrado con
contraseña. Se dejan fuera a propósito para no convertir un post-it
simple en un gestor de tareas — si con el uso real hace falta alguna,
se añade en una versión posterior sin que el diseño actual la bloquee.

## Stack tecnológico

**C# + WPF (.NET), Windows-only.**

Justificación (no por familiaridad con otros lenguajes del autor, sino
por criterio técnico puro):

- La dificultad real de esta app no está en la lógica de negocio, está
  en lograr que Windows deje a una ventana comportarse de forma no
  estándar: transparente, sin bordes, con click-through parcial,
  anclada a un borde, siempre-encima, consciente de DPI por monitor.
- WPF renderiza su propia UI por composición vectorial en vez de
  delegar en controles nativos de Windows, y por eso tiene 18 años de
  precedente documentado exactamente para este tipo de "hack de
  ventana" (paneles ocultos que se despliegan, notas ancladas a un
  borde). Mantiene acceso completo a Win32 vía P/Invoke cuando hace
  falta bajar de nivel (icono de bandeja, atajo global).
- Se descarta C++/Win32 puro: da control máximo pero a un coste de
  desarrollo y de riesgo de errores de memoria que esta app no
  necesita, porque no tiene ninguna exigencia real de rendimiento
  (pasa el 99% del tiempo inactiva).
- Se descarta Rust + windowing nativo: técnicamente sólido y sin
  garbage collector, pero el patrón concreto de ventana que necesita
  Fanote tiene muchísimo menos precedente en el ecosistema Rust en
  Windows que en WPF — mayor riesgo de quedarse atascado resolviendo
  problemas de plataforma por primera vez.
- Se descartan Electron y Tauri: Electron tiene un coste de RAM en
  reposo (100-200MB+) inasumible para una app que vive todo el día en
  el borde de la pantalla; Tauri resuelve ese problema pero la UI sigue
  siendo un webview, con el mismo problema de ventanas "raras" que
  Electron.
- Se descarta WinUI 3 frente a WPF pese a ser la dirección "moderna" de
  Microsoft: restringe mucho más la personalización de ventana por
  consistencia/seguridad, justo en la parte que hace única a esta app,
  con menos precedente de comunidad para resolver dudas.

## Arquitectura

Aplicación WPF única (.exe), patrón MVVM, dividida en capas:

- **Capa de ventanas/shell**: una instancia de `EdgeDockWindow` por cada
  pantalla activa según Ajustes. Gestiona la franja, la detección de
  hover y la animación de abanico usando eventos nativos de WPF
  (`MouseEnter`/`MouseLeave`) sobre la propia ventana anclada — no hace
  falta ningún hook global de ratón (`WH_MOUSE_LL`) ni sondeo por
  temporizador, porque el cursor está literalmente sobre esa ventana
  cuando el hover ocurre. Activa/desactiva el click-through vía un
  estilo de ventana extendido (interop) según si el cursor está sobre
  el pill o sobre zona transparente.
- **Capa de persistencia**: SQLite + cifrado DPAPI, papelera/archivado,
  serialización a/desde el formato de backup portable.
- **Capa de integración con Windows**: icono de bandeja (interop, WPF no
  tiene control nativo de bandeja), atajo global (`RegisterHotKey`),
  inicio automático (entrada de Registro o acceso directo en la carpeta
  de Inicio).
- **Ajustes**: archivo de configuración local aparte de la base de
  datos de notas (pantallas activas, posición de borde global o por
  pantalla, atajo de teclado, inicio automático, modo vintage).

### Componentes

1. `EdgeDockWindow` — pill + abanico por monitor.
2. `NoteWindow` — nota expandida (texto, checkboxes, color,
   archivar/borrar/restaurar).
3. `NotesRepository` — acceso a SQLite, cifrado, lógica de
   papelera/archivado.
4. `ImportExportService` — genera/lee el archivo de backup portable.
5. `TrayIconService` — icono de bandeja + menú.
6. `HotkeyService` — registro del atajo global.
7. `SettingsService` — lee/escribe el archivo de ajustes.
8. `MonitorService` — detecta pantallas conectadas/desconectadas y
   cambios de DPI, posiciona cada `EdgeDockWindow`.

Cada componente tiene una responsabilidad única y es comprensible/
testeable sin conocer los demás — `NotesRepository` no sabe nada de
ventanas, `EdgeDockWindow` no sabe nada de SQLite.

## Modelo de datos

Tabla `Note`:

- `Id`
- `Text` (contenido libre; los checkboxes viven como texto `[ ]`/`[x]`
  dentro de este campo)
- `Color`
- `ScreenOrigin` (en qué pantalla vive)
- `CreatedAt`
- `UpdatedAt`
- `State` (`Active` | `Archived` | `Trashed`)

No hay tablas de etiquetas, carpetas ni recordatorios — quedaron fuera
de alcance.

## Manejo de errores

- **Base de datos corrupta/ilegible al arrancar**: se copia el archivo
  dañado antes de tocar nada, y se ofrece crear una base nueva vacía en
  vez de fallar sin más.
- **Clave DPAPI no disponible** (p. ej. perfil de Windows dañado):
  mensaje claro explicando que sin esa clave no se puede leer lo
  cifrado, sugiriendo restaurar desde un backup si existe.
- **Import de un `.fanotebackup` corrupto o de formato inválido**: se
  valida el formato antes de tocar la base de datos real; si falla, se
  rechaza con un mensaje claro sin modificar nada de lo existente.
- **Atajo de teclado global ya usado por otra app**: se detecta el
  fallo de registro y se muestra en Ajustes, en vez de fallar en
  silencio.
- **Monitor desconectado con su dock abierto**: las notas de esa
  pantalla se reasignan automáticamente a la pantalla principal.

## Testing

- `NotesRepository`, `ImportExportService` y `SettingsService` son
  testeables de forma aislada (sin levantar ventanas): pruebas
  unitarias sobre CRUD, papelera/archivado, cifrado/descifrado, y el
  ciclo export→import.
- La parte de ventanas/hover/multi-monitor depende del compositor de
  Windows y es la más difícil de automatizar; se valida con una lista
  de escenarios manuales dirigidos: monitor único, dos monitores con
  DPI distinto, conectar/desconectar un monitor con el dock abierto,
  cambio de posición de borde (global y por pantalla).

## Nombre

**Fanote** (fan + note) — sin colisiones encontradas en búsquedas de
apps/software existentes en el momento de escribir esta spec. Se
descartaron "EdgeNotes" (app de notas de borde de pantalla ya existente
en Microsoft Store, mismo concepto), "Perch" (tomado por Perch AI) y
"Margin" (usado por varias apps de notas ya establecidas). Se evita
cualquier nombre que incluya "Post-it", marca registrada de 3M.
