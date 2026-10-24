# Aldune v1 — Design Spec

## Contexto y objetivo

Aldune es una aplicación de notas para Windows inspirada en Hold My Notes,
Noty (aimen08/noty) y noty-sepia: notas ancladas al borde de la pantalla
que viven como una fina franja ("pill") y se despliegan en abanico al
pasar el ratón por encima. Ninguna de las referencias existe para
Windows — todas son apps nativas de macOS (Swift/SwiftUI).

Uso previsto: herramienta personal del autor para apuntar cosas rápido,
como un post-it real — sin gestión de fechas ni recordatorios, sin
cuenta ni sincronización. Objetivo secundario, no bloqueante para v1: si
el proyecto sale bien, publicarlo para que otros lo usen.

Esta spec fue revisada de forma independiente (segunda opinión) antes de
pasar a plan de implementación; los cambios de esa revisión ya están
incorporados en las secciones siguientes.

## Alcance de v1

### Núcleo (mecánica de ventana)

- Una franja fina anclada a un borde de pantalla (arriba/abajo/
  izquierda/derecha), que se despliega en abanico al pasar el ratón por
  encima.
- Clic en una pestaña del abanico → la nota se abre a tamaño completo en
  su sitio.
- Multi-monitor: la app puede mostrarse en todas las pantallas
  conectadas o solo en una, configurable en Ajustes.
- Posición de borde: **una sola, global, para todas las pantallas
  activas** en v1 (ver "Aplazado a v1.1" para la variante por pantalla).
- La ventana **cambia de tamaño real** en vez de usar una superficie
  transparente con click-through conmutado: pequeña (solo el pill) en
  reposo, crece para ocupar el área del abanico mientras está expandida,
  y vuelve a encogerse al colapsar. Así no existe nunca una zona
  transparente que gestionar ni un estado de "pasar clics a través" que
  activar/desactivar — se descartó ese diseño porque era contradictorio
  (una ventana con el estilo de click-through activado no recibe
  eventos de ratón, así que nunca podría detectar el hover que lo
  desactivaría). Este enfoque evita además que `AllowsTransparency`
  desactive el suavizado de texto normal (ClearType) del sistema, que
  chocaría con "tipografía elegida por legibilidad".
- El pill **no roba el foco** al expandirse por hover (estilo de
  ventana `WS_EX_NOACTIVATE`) — no debe interrumpir lo que se esté
  escribiendo en otra aplicación. La ventana de nota expandida
  (`NoteWindow`), en cambio, sí se activa y recibe foco al abrirse,
  porque ahí se espera que el usuario escriba.
- El colapso del abanico no depende solo de `MouseLeave` (poco fiable:
  oscila en el límite del área expandida, y puede no dispararse si otra
  ventana queda por encima) — se aplica un pequeño retardo antes de
  colapsar (~150-250ms) más una comprobación de posición del ratón de
  baja frecuencia **solo mientras está expandido**, como red de
  seguridad. Esto no contradice "sin sondeo": la detección del hover en
  sí sigue siendo por eventos nativos de la ventana; el temporizador
  solo entra en juego como respaldo del colapso.
- Si el borde elegido coincide con el de la barra de tareas de Windows,
  el dock se ancla al área de trabajo (excluyendo la barra), no al
  borde físico de la pantalla, para no solaparse ni pelear con su
  z-order.
- Consciente de DPI por monitor (Per-Monitor V2): cada pantalla puede
  tener un factor de escala distinto (p. ej. portátil a 150% + monitor
  externo a 100%); la app se re-renderiza nítida en cada una en vez de
  dejar que Windows estire un mapa de bits ya renderizado.
- Cada pantalla se identifica por un id estable de dispositivo (no por
  el número `\\.\DISPLAY1`, que puede cambiar al reconectar monitores).
  Si un monitor con notas ancladas se desconecta, esas notas se
  **muestran** temporalmente en la pantalla principal sin **reescribir**
  su pantalla de origen almacenada — al reconectar el monitor original,
  vuelven a su sitio. Nunca se pierden ni quedan inaccesibles, y nunca
  se pierde la disposición original por un desconectado/reconectado.

### Contenido de la nota

- Texto libre, con checkboxes inline: una línea que empieza con `[ ]`
  se muestra como ☐ y, al hacer clic, pasa a `[x]`/☑. La sintaxis vive
  dentro del propio campo de texto de la nota (no hay tabla de tareas
  separada), pero renderizar un ☐ clicable dentro de una línea es
  trabajo de UI real: la nota necesita un modo de visualización
  (lista de líneas, con las de checkbox como control clicable) distinto
  de su modo de edición (texto plano) — no es "gratis" solo por
  guardarse como texto.
- Color de nota, elegible por el usuario.
- Fecha de creación y de última edición, guardadas como metadato (no se
  exponen como sistema de organización, solo como dato).
- Autoguardado silencioso: no hay botón "Guardar", el texto se persiste
  solo mientras se escribe (con un pequeño debounce para no escribir a
  disco en cada pulsación). Al cerrar la aplicación (incluido un cierre
  de sesión de Windows) se fuerza un guardado inmediato de cualquier
  cambio pendiente del debounce, para no perder las últimas pulsaciones.
- Archivar: oculta la nota de la vista activa sin borrarla.
- Papelera: el borrado no es instantáneo ni definitivo; las notas
  borradas pasan a un estado "Papelera" recuperable. Archivado y
  papelera son el mismo mecanismo de estado (`Active` / `Archived` /
  `Trashed`), no dos sistemas distintos — solo cambia qué filtro de
  vista se aplica.

### Personalización

- Tema claro/oscuro: sigue el tema del sistema operativo por defecto,
  pero con un selector propio en Ajustes de tres estados (Sistema /
  Claro / Oscuro) para forzar uno manualmente si el usuario lo prefiere.
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

El "Modo Papel vintage" (textura, sonido, inclinación por nota,
envejecimiento) se aplaza a v1.1 — ver esa sección.

### Almacenamiento

- Local, en SQLite. Cifrado por sobre (*envelope encryption*): se
  genera una clave simétrica para la base de datos, y esa clave (no
  cada nota por separado) se protege con DPAPI ligada al
  usuario/equipo de Windows y se guarda en el archivo de configuración
  local. Sin servidor, sin cuenta, sin telemetría.
  - Límite honesto que hay que documentar en la propia app antes de
    publicarla: DPAPI-por-usuario protege frente a otra cuenta de
    Windows o frente a alguien sin tu sesión iniciada, pero **no**
    frente a otro proceso corriendo con tu misma sesión de usuario.
  - Cifrar el contenido bloquea poder hacer búsqueda por SQL/`LIKE`
    sobre el texto de las notas en el futuro (búsqueda ya está fuera de
    alcance de v1, así que no se resuelve ahora — solo se deja
    constancia de la dependencia para cuando se retome).
- **Exportar/Importar** (para mover las notas a otro ordenador o hacer
  copia de seguridad): genera un archivo propio (`.aldunebackup`, un
  zip con JSON dentro, con un campo de versión de formato desde v1 para
  poder evolucionar el formato sin romper backups antiguos) que
  contiene las notas **sin cifrar**. Es una decisión deliberada: la
  clave DPAPI está ligada a la máquina y no puede viajar con el
  archivo, así que cifrar el export exigiría pedir una contraseña al
  usuario — se descarta por fricción. En su lugar, se sigue el mismo
  patrón que usa Firefox para exportar marcadores (JSON sin cifrar por
  defecto, cifrado solo como extensión opcional de terceros): el
  archivo de export es responsabilidad del usuario mientras exista (no
  dejarlo en una carpeta compartida, moverlo a mano).
  - **Semántica de import**: fusiona con lo existente, nunca reemplaza
    ni borra notas que ya estuvieran ahí. Si un id del archivo coincide
    con uno ya existente en destino, se genera un id nuevo para la nota
    importada en vez de sobrescribir. Se incluyen notas archivadas y en
    papelera, no solo las activas (fidelidad completa del backup).
  - El import valida el formato (incluida la versión) antes de tocar la
    base de datos real; si no es válido, se rechaza con un mensaje
    claro sin modificar nada de lo existente. Al importar, las notas se
    cifran con la clave del equipo de destino.

### Integración con Windows

- Icono en la bandeja del sistema, con menú: abrir todas las notas,
  ajustes, salir.
- Atajo de teclado global configurable para crear una nota nueva desde
  cualquier sitio.
- Inicio automático con Windows (activable/desactivable en Ajustes).
- **Instancia única**: al arrancar, la app comprueba (con un mutex con
  nombre) si ya hay una instancia corriendo, y si la hay, no arranca
  una segunda — evita atajo de teclado roto, docks duplicados y dos
  procesos escribiendo a la vez en la misma base de datos. Relevante
  porque la combinación de inicio automático + bandeja + atajo global
  hace fácil lanzar la app dos veces sin querer.

### Explícitamente fuera de v1 (sin plan de añadir pronto)

Búsqueda entre notas, etiquetas/carpetas, sincronización en la nube,
recordatorios o notas con fecha/notificación, editor de temas libre
(colores arbitrarios elegidos por el usuario), export cifrado con
contraseña. Se dejan fuera a propósito para no convertir un post-it
simple en un gestor de tareas — si con el uso real hace falta alguna,
se añade en una versión posterior sin que el diseño actual la bloquee
(salvo búsqueda, que sí requeriría revisar el cifrado del contenido
cuando se retome, como se anota en Almacenamiento).

### Aplazado a v1.1 (decidido tras la revisión de diseño, no descartado)

Reducen el riesgo del primer hito de implementación porque son
ortogonales al mecanismo difícil de la app (ventana/hover/multi-monitor)
y no cuesta nada dejarlos fuera del primer build:

- **Modo "Papel vintage"** (opcional, desactivado por defecto cuando
  llegue, con un sub-interruptor para separar visual de sonido):
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
- **Posición de borde independiente por pantalla**: v1 solo ofrece una
  posición global para todas las pantallas activas; la variante "cada
  pantalla su propio borde, con una casilla para volver al modo global"
  se añade en v1.1 sobre la misma base de configuración.

## Stack tecnológico

**C# + WPF (.NET), Windows-only.**

Justificación (no por familiaridad con otros lenguajes del autor, sino
por criterio técnico puro):

- La dificultad real de esta app no está en la lógica de negocio, está
  en lograr que Windows deje a una ventana comportarse de forma no
  estándar: sin bordes, redimensionable en vivo entre pill y abanico,
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
  Aldune tiene muchísimo menos precedente en el ecosistema Rust en
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
  (`MouseEnter`/`MouseLeave`) sobre la propia ventana anclada, que
  cambia de tamaño real entre estado pill y estado expandido (ver
  Núcleo) — no hace falta ningún hook global de ratón (`WH_MOUSE_LL`)
  para la detección de hover en sí, solo un temporizador de respaldo
  para el colapso.
- **Capa de persistencia**: SQLite + cifrado por sobre vía DPAPI,
  papelera/archivado, serialización a/desde el formato de backup
  portable versionado.
- **Capa de integración con Windows**: icono de bandeja (interop, WPF no
  tiene control nativo de bandeja), atajo global (`RegisterHotKey`),
  inicio automático (entrada de Registro o acceso directo en la carpeta
  de Inicio), mutex de instancia única.
- **Ajustes**: archivo de configuración local aparte de la base de
  datos de notas (pantallas activas, posición de borde global, atajo de
  teclado, inicio automático, clave DPAPI-protegida de la base de
  datos).

### Componentes

1. `EdgeDockWindow` — pill + abanico por monitor.
2. `NoteWindow` — nota expandida (texto, checkboxes, color,
   archivar/borrar/restaurar).
3. `NotesRepository` — acceso a SQLite, cifrado, lógica de
   papelera/archivado.
4. `ImportExportService` — genera/lee el archivo de backup portable
   versionado, con la semántica de fusión descrita en Almacenamiento.
5. `TrayIconService` — icono de bandeja + menú.
6. `HotkeyService` — registro del atajo global.
7. `SettingsService` — lee/escribe el archivo de ajustes.
8. `MonitorService` — detecta pantallas conectadas/desconectadas (por
   id estable de dispositivo) y cambios de DPI, posiciona cada
   `EdgeDockWindow`.

Cada componente tiene una responsabilidad única y es comprensible/
testeable sin conocer los demás — `NotesRepository` no sabe nada de
ventanas, `EdgeDockWindow` no sabe nada de SQLite.

## Modelo de datos

Tabla `Note`:

- `Id`
- `Text` (contenido libre; los checkboxes viven como texto `[ ]`/`[x]`
  dentro de este campo, renderizados como controles clicables en modo
  visualización)
- `Color`
- `ScreenOrigin` (id estable de dispositivo de la pantalla de origen;
  no se reescribe por una desconexión temporal del monitor)
- `CreatedAt`
- `UpdatedAt`
- `State` (`Active` | `Archived` | `Trashed`)

No hay tablas de etiquetas, carpetas ni recordatorios — quedaron fuera
de alcance.

## Manejo de errores

- **Base de datos corrupta/ilegible al arrancar**: se copia el archivo
  dañado antes de tocar nada, y se ofrece crear una base nueva vacía en
  vez de fallar sin más.
- **Clave DPAPI no disponible**: cubre dos causas distintas y hay que
  distinguirlas en el mensaje. (a) Perfil de Windows dañado — puede ser
  recuperable. (b) La contraseña de la cuenta de Windows fue reseteada
  por un administrador — esto **destruye permanentemente** la clave
  DPAPI y con ella el acceso a todas las notas cifradas; no hay
  recuperación posible salvo restaurar desde un `.aldunebackup`
  exportado previamente. El mensaje debe decir esto explícitamente,
  porque es la causa más probable en la práctica y la única sin ninguna
  solución técnica.
- **Import de un `.aldunebackup` corrupto o de versión de formato no
  reconocida**: se valida el formato (incluida la versión) antes de
  tocar la base de datos real; si falla, se rechaza con un mensaje
  claro sin modificar nada de lo existente.
- **Atajo de teclado global ya usado por otra app**: se detecta el
  fallo de registro y se muestra en Ajustes, en vez de fallar en
  silencio.
- **Monitor desconectado con su dock abierto**: las notas de esa
  pantalla se muestran temporalmente en la pantalla principal sin
  perder su `ScreenOrigin` real (ver Núcleo).
- **Segunda instancia lanzada**: se detecta vía el mutex de instancia
  única y no se abre una segunda copia (opcionalmente, se podría
  enfocar/mostrar la instancia ya corriendo — detalle a decidir en el
  plan de implementación, no bloqueante para la spec).

## Testing

- `NotesRepository`, `ImportExportService` y `SettingsService` son
  testeables de forma aislada (sin levantar ventanas): pruebas
  unitarias sobre CRUD, papelera/archivado, cifrado/descifrado, y el
  ciclo export→import (incluida la fusión con ids en conflicto).
- La parte de ventanas/hover/multi-monitor depende del compositor de
  Windows y es la más difícil de automatizar; se valida con una lista
  de escenarios manuales dirigidos: monitor único, dos monitores con
  DPI distinto, conectar/desconectar un monitor con el dock abierto,
  borde coincidiendo con la barra de tareas, arrancar una segunda
  instancia.

## Orden de implementación sugerido

Para minimizar riesgo en el primer hito, dado que el autor es nuevo en
C#/WPF/Win32:

1. Mecánica de ventana en un solo monitor (pill, hover, expansión,
   nota abierta) — el núcleo técnicamente más incierto, aislado primero.
2. CRUD de notas + almacenamiento (SQLite, cifrado, papelera/archivado).
3. Multi-monitor + DPI.
4. Bandeja, atajo global, inicio automático, instancia única.
5. Import/export.

El Modo Papel vintage y la posición de borde por pantalla quedan fuera
de este orden — son v1.1, después de que v1 funcione de punta a punta.

## Nombre

**Aldune** (fan + note) — sin colisiones encontradas en búsquedas de
apps/software existentes en el momento de escribir esta spec. Se
descartaron "EdgeNotes" (app de notas de borde de pantalla ya existente
en Microsoft Store, mismo concepto), "Perch" (tomado por Perch AI) y
"Margin" (usado por varias apps de notas ya establecidas). Se evita
cualquier nombre que incluya "Post-it", marca registrada de 3M.
