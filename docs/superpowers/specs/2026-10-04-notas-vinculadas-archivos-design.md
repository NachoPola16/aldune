# Aldune — Notas vinculadas a archivos `.md` y `.txt` (design spec)

Decidido con el usuario el 2026-10-04 (fase F1 de `docs/superpowers/plans/2026-10-03-orden-de-trabajo.md`),
a partir de lo que pidió en `docs/ROADMAP.md` §11. Sale como **1.6.0**.

## Qué se pidió

- Abrir un `.md` que ya está en cualquier carpeta del equipo y que **se quede en el dock apuntando a ese
  sitio**: la nota *es* el archivo (se edita ahí y el archivo cambia), no una copia.
- **Elegir por nota si entra en la sincronización.** Ejemplo real: los apuntes de una asignatura en `Z:`
  (volumen en red), que el portátil ve en el mismo sitio.
- El usuario edita esos archivos también con otros programas, aunque no necesariamente a la vez.

## Qué no entra, y por qué

- **Ver Markdown con formato** (títulos grandes, negritas): el editor es un `TextBox` plano y tareas,
  viñetas, sangrías, mover líneas y deshacer dependen de ello. Rehacerlo es tocar el corazón de la app con
  mucho riesgo; el `.md` se edita bien como texto.
- **Fotos**: ya descartadas en `ROADMAP.md` §3. Rompen el modelo de texto plano en el que se apoyan el
  cifrado, la búsqueda, la exportación, la sync (servidor y tamaño de los sobres) y el cliente móvil.
- **Vincular la misma nota a un archivo en cada equipo** ("raíces con nombre" que viajan por la sync):
  con `Z:` no aporta nada (el archivo ya es el mismo en los dos equipos y sincronizarlo además crearía
  conflictos falsos por doble camino); con copias locales convertiría Aldune en un sincronizador de
  archivos que compite con OneDrive o Syncthing; obligaría a subir el formato de sync a 5; y daría a lo
  que llega por la sync la capacidad de escribir archivos del disco de otro equipo. El vínculo es de cada
  equipo: en el portátil se abre el archivo una vez y queda en su dock.
- **Notas protegidas vinculadas**: ver decisión 7.
- **Edición simultánea letra a letra** con otro programa (fusionar mientras se escribe en los dos): muy
  compleja; con la decisión 4 no se pierde nada.
- Formatos con otra sintaxis de listas (`.org`, `.rst`).

## Decisiones

### 1. Una nota vinculada sigue siendo una nota

Tiene su fila en `Note` (color, etiquetas, posición en el mazo, recordatorio, estado) como cualquier otra.
Solo cambia de dónde sale y adónde va su texto. `Note.Text` guarda la **traducción** del archivo al formato
de Aldune (`☐`, `☒`, `→`; ver decisión 3): es la copia del último contenido leído, y por eso el dock, el
gestor, la búsqueda, el recordatorio y "Exportar a Markdown" funcionan sin casos especiales. La base de
datos va cifrada como siempre; lo único en claro es el archivo, que ya lo estaba.

### 2. El vínculo es local y no viaja

Tabla nueva `NoteFileLink` (creada con `CREATE TABLE IF NOT EXISTS`, como las demás; una base antigua la
gana al abrirse y una versión antigua la ignora):

| Columna | Qué guarda |
|---|---|
| `NoteId` (PK) | La nota. |
| `Path` | Ruta completa del archivo. |
| `SyncEnabled` | Si la nota entra en la sync. **0 por defecto.** |
| `KnownHash` | SHA-256 del contenido del archivo la última vez que Aldune lo leyó o escribió. |
| `KnownWriteTime` | Fecha de escritura del archivo en ese momento (para la comprobación barata). |
| `KnownTextHash` | SHA-256 de `Note.Text` en ese mismo momento. |

**"Cambios sin escribir"** se define como `hash(Note.Text) != KnownTextHash`. Vive en la base de datos, así
que sobrevive a un cierre inesperado: si Aldune se cierra antes de escribir el archivo, al arrancar sigue
sabiendo que hay algo pendiente.

Nada de esto entra en el sobre de sync. **El formato de sync sigue en 4.**

- **Sync desactivada** (por defecto): `SyncScopeFilter.Includes` deja fuera la nota. Es el sitio único que
  ya comparten la sync y la señal ▂▄▆█, así que la señal dirá "fuera del alcance".
- **Sync activada**: la nota viaja como una nota normal y cifrada, y en el otro equipo es una nota normal.
  Lo que llega de otro equipo se guarda en la nota y **se escribe en el archivo** por el camino de la
  decisión 4 (con su comprobación de huella). "Sincronizar esta nota" se **suma** al alcance general: si la
  sync está limitada a una etiqueta o a notas elegidas, la nota tiene que cumplirlo también.

### 3. Traducción entre el archivo y la nota (`MarkdownLink`, Core)

Dos funciones puras:

- `ToNoteText(string fileText) → string`: `- [ ] ` → `☐ `, `- [x] `/`- [X] ` → `☒ `, `- ` → `→ `, también con
  `*` y `+`, respetando la sangría. Reutiliza lo que ya hace `TaskLists.ConvertMarkdownTasks` al pegar.
  Lo demás (encabezados `#`, enlaces, tablas, cabecera YAML, listas numeradas) se queda literal.
  **Dentro de un bloque de código** (entre vallas ```` ``` ```` o `~~~`) no se traduce nada.
- `ToFileText(string noteText, string originalFileText) → string`: reconstruye el archivo. Traduce el
  original línea a línea a formato nota, empareja esas líneas con las de `noteText` (subsecuencia común más
  larga) y **toda línea emparejada se escribe con los bytes originales exactos** (marcador `*`/`+`/`-`,
  `[X]` mayúscula, tabuladores, espacios al final). Las líneas nuevas o editadas se escriben en forma
  estándar: tarea `- [ ] `/`- [x] `; viñeta con el marcador más usado en el archivo (o `-` si no hay
  ninguno). Las líneas que en la nota no llevan glifo se escriben tal cual.
- **Garantía**: `ToFileText(ToNoteText(f), f) == f` para cualquier `f`, y editar una línea solo cambia esa
  línea del archivo.

Formato del archivo (`LinkedFileFormat`, Core): se detecta al leer y se conserva al escribir.

- UTF-8 con o sin BOM, UTF-16 LE/BE con BOM. Un archivo que no es UTF-8 válido y no lleva BOM (ANSI antiguo)
  **no se vincula**, con aviso: guardarlo estropearía sus tildes.
- Saltos de línea: se conserva el dominante (CRLF o LF); las líneas emparejadas conservan el suyo. Se
  conserva si el archivo terminaba o no en salto de línea.
- Tope de **2 MB**, como el editor. Más grande: no se vincula, con aviso.
- Extensiones: `.md`, `.markdown`, `.txt`. La misma traducción para las tres (una tarea nueva en un `.txt` se
  escribe `- [ ]`, que se lee bien en cualquier editor).

### 4. Leer, guardar y cambios hechos por fuera (`LinkedFileDecision`, Core)

Una función pura decide, con `KnownHash`, la huella actual del disco (o "no existe/no accesible") y si la
nota tiene cambios sin escribir en el archivo:

| Disco | Cambios sin escribir en Aldune | Qué se hace |
|---|---|---|
| igual a `KnownHash` | sí | **Escribir** el archivo. |
| igual a `KnownHash` | no | Nada. |
| distinto | no | **Recargar**: el archivo manda; la nota se actualiza. |
| distinto | sí | **Conflicto**. |
| no accesible | cualquiera | **No disponible** (decisión 5). |

- **Escribir**: con el mismo autoguardado de las notas. Se escribe en un temporal oculto de la misma carpeta
  (`.~aldune-<guid>.tmp`) y se sustituye con `File.Replace`, de modo que un corte no deja el archivo a
  medias; si la unidad no lo admite, se escribe en el sitio (el texto sigue a salvo en la base de datos).
  Justo antes se vuelve a leer y comparar la huella: **nunca se escribe sobre un archivo cuya huella no es
  la conocida**. Tras escribir se guardan la huella y la fecha nuevas. Un temporal huérfano de Aldune se
  borra en el siguiente guardado de esa nota (es lo único que Aldune borra en una carpeta del usuario, y
  solo si su nombre sigue el patrón exacto).
- **Recargar**: la nota toma el texto traducido, con `UpdatedAt` = ahora (si la sync está activada, se
  publica). Con la ventana abierta, el cursor se queda en la misma línea si existe.
- **Conflicto**: el archivo **no se toca**. El texto de Aldune pasa a una **nota normal nueva** ("⚠
  Conflicto: \<título\>", mismo color, sin vínculo), la nota vinculada recarga el disco y se avisa. No se
  crean archivos en las carpetas del usuario.
- **Vigilancia** (WPF): un `FileSystemWatcher` por carpeta con archivos vinculados, más una comprobación
  periódica de la fecha de escritura (cada 3 s con la nota abierta, cada 30 s si no; en unidades de red el
  vigilante falla). Tras un aviso se espera a que el archivo deje de cambiar (dos lecturas iguales
  separadas 300 ms) antes de decidir: los editores guardan en varios pasos. Las escrituras de la propia
  Aldune se reconocen por la huella y no hacen nada. Las notas archivadas o en la papelera no se vigilan;
  al restaurarlas se vuelve a decidir.
- **Cambios automáticos**: en una nota vinculada **no** se aplican "ocultar tareas hechas" ni "mover las hechas
  al final" de forma automática (reescribirían líneas del archivo sin que el usuario haga nada). Lo que el
  usuario hace a mano (marcar, borrar hechas, mover líneas) sí.

### 5. Archivo no disponible

Archivo borrado, carpeta movida, `Z:` desconectado o sin permisos:

- La nota pasa a **solo lectura** y muestra una franja: "Archivo no disponible: Z:\apuntes\tema3.md", con
  **Reintentar**, **Buscar…** (vincular de nuevo a otra ruta: la huella conocida se descarta, así que
  se decide como "disco distinto": sin cambios pendientes manda el archivo; con ellos, conflicto) y **Convertir en nota normal**.
- La comprobación periódica sigue: cuando el archivo vuelve, la franja desaparece sola y se decide como en
  la decisión 4.
- Lo que se edita justo antes de que desaparezca, o lo que llega por la sync mientras no está, se queda en
  la nota como cambio pendiente y se escribe cuando vuelve (o da conflicto si el archivo cambió entretanto).
- Un renombrado dentro de la misma carpeta (evento `Renamed` del vigilante) actualiza la ruta solo.

### 6. Interfaz

Abrir (las tres primeras admiten varios archivos):

1. **"Abrir archivo…"** en el menú del dock y en el de la bandeja (filtro `.md`, `.markdown`, `.txt`).
2. **Arrastrar** archivos desde el Explorador al dock.
3. **"Abrir con → Aldune"** en el Explorador: el instalador registra Aldune en `OpenWithProgids` de las tres
   extensiones, **sin hacerse el programa predeterminado**. `aldune.exe "<ruta>"` abre el archivo; si ya hay
   una Aldune en marcha, la nueva le pasa las rutas por una tubería con nombre (`SingleInstance` hoy solo
   avisa para mostrarse) y sale. La versión portable no registra nada, pero la línea de órdenes funciona.
4. **"Guardar como archivo vinculado…"** en el "⋯" de una nota normal no protegida: crea el archivo con
   `ToFileText(texto, "")` (UTF-8 sin BOM, CRLF) y vincula la nota.

Abrir un archivo ya vinculado (misma ruta completa, sin distinguir mayúsculas) **muestra la nota existente**.
Uno nuevo entra al final del mazo, con el color que toque por las reglas de asignación, y se abre.

En la nota vinculada:

- **Marca en la pestaña** (icono de página de Segoe Fluent Icons) y la ruta en el tooltip; igual en el gestor.
- **Título**: la primera línea sin los `#` de encabezado de Markdown (`# Tema 3` → "Tema 3"). Se aplica a todas
  las notas en `NoteTitleHelper`. Si la primera línea está vacía, el nombre del archivo sin extensión.
- **Menú "⋯"**: "Sincronizar esta nota" (casilla), "Abrir en su editor" (el programa predeterminado de
  Windows para la extensión), "Mostrar en el Explorador", "Convertir en nota normal" (quita el vínculo,
  conserva el texto y avisa de que el archivo sigue en el disco) y "Proteger" desactivado con su
  explicación.
- "Exportar a Markdown" sigue disponible y crea una copia aparte.

Textos con `Strings.T` en los cinco idiomas; colores por `DynamicResource`.

### 7. Notas protegidas: no se vinculan

"La nota es tu archivo" y el archivo es texto en claro. Proteger solo en Aldune sería una falsa seguridad
(cualquiera abre el `.md` con el Bloc de notas); cifrar el archivo lo haría ilegible para los demás
programas, que es para lo que se vincula. Así que en una nota vinculada "Proteger" está desactivado, y para
protegerla primero se convierte en nota normal (con el aviso de que el archivo sigue en claro en el disco).
"Guardar como archivo vinculado…" no se ofrece en una nota protegida.

### 8. Archivar, papelera y borrar

- Archivar conserva el vínculo y deja de vigilar; restaurar vuelve a leer.
- En la papelera se conserva el vínculo, para que restaurar funcione.
- Vaciar la papelera (o su plazo) borra la nota y la fila de `NoteFileLink`. **Aldune nunca borra un archivo
  vinculado**, esté donde esté: borrarlo no se puede deshacer y en una unidad de red ni siquiera pasa por la
  Papelera de Windows. Para borrarlo, "Mostrar en el Explorador".

## Piezas

Core (TDD):

- `MarkdownLink`: `ToNoteText`, `ToFileText`.
- `LinkedFileFormat`: lectura de bytes → texto + formato (codificación, BOM, salto dominante, salto final);
  escritura texto + formato → bytes; rechazos (codificación, tamaño).
- `LinkedFileDecision`: la tabla de la decisión 4.
- `NotesRepository`: tabla `NoteFileLink` y sus operaciones; `SyncScopeFilter` con la casilla por nota;
  `NoteTitleHelper` sin `#`.

WPF:

- `LinkedFileWatcher`: vigilantes por carpeta, comprobación periódica, espera a que el archivo se asiente.
- `AppCoordinator`: abrir/vincular, aplicar decisiones, conflicto, convertir en nota normal.
- `NoteWindow`: guardado por el camino del archivo, franja de no disponible y solo lectura, menús, no aplicar
  los cambios automáticos.
- `EdgeDockWindow`: soltar archivos; marca en la pestaña. Gestor: marca y tooltip.
- `SingleInstance`: tubería con nombre para pasar rutas; `App.OnStartup` lee los argumentos.
- Instalador (`Aldune.iss`): `OpenWithProgids` para `.md`, `.markdown`, `.txt`, que se quita al desinstalar.

## Pruebas

- **Ida y vuelta byte a byte** (`ToFileText(ToNoteText(f), f) == f`) con una colección de archivos difíciles:
  bloques de código con `- [ ]` dentro (con ```` ``` ```` y `~~~`), listas anidadas y numeradas, `* [X]`, `+`,
  tabuladores, espacios al final, CRLF, LF y mezcla, con y sin BOM, UTF-16, cabecera YAML, archivo vacío,
  sin salto final, solo saltos.
- **Ediciones localizadas**: marcar una tarea, añadir una línea, borrar otra, mover una con Alt+↑: solo
  cambian esas líneas del archivo.
- `LinkedFileDecision`: cada fila de la tabla; `LinkedFileFormat`: cada codificación y cada rechazo.
- `SyncScopeFilter`: nota vinculada sin sync fuera; con sync, sujeta al alcance general.
- **Sondas** (fuera del repo, datos temporales): cambio por fuera que se recarga; conflicto (nota "⚠
  Conflicto" y archivo intacto); carpeta renombrada (no disponible, solo lectura, vuelve sola); arrastrar
  dos archivos al dock; segunda instancia con una ruta; "Guardar como archivo vinculado…". Las que mueven
  ratón o teclado, con aviso previo.
- Smoke test en verde antes de publicar.

## Orden de trabajo

Un plan, en dos tandas:

1. Core completo; abrir ("Abrir archivo…" y arrastrar), guardar, vigilar, no disponible (con "Convertir en
   nota normal"), conflicto, archivar/papelera, marca y título. Con esto ya se usan los apuntes de `Z:`.
2. "Abrir con" (instalador y tubería), "Guardar como archivo vinculado…", "Sincronizar esta nota",
   "Abrir en su editor" y "Mostrar en el Explorador".
