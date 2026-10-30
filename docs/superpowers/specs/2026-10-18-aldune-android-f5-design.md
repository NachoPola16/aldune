# Aldune para Android — F5: la sync dentro de la app

> 2026-10-18. Detalla la fila F5 de [`2026-10-17-aldune-android-design.md`](2026-10-17-aldune-android-design.md) §5. Manda esa spec (sobre todo §2.6,
> §3 y §3.1) en lo que no se diga aquí. Estado de partida: F0-F4 hechas y verificadas en el Pixel 10a y la tableta Lenovo TB350FU
> (`INFORME-ANDROID-F4.md`); el motor (`planSync`/`execute`) y los transportes HTTP y WebDAV existen en `android/sync`
> con 119 pruebas. Las decisiones marcadas **(usuario)** las tomó el usuario el 18-10.

## 0. Objetivo y límite

Que el móvil y la tableta **sincronicen con el mismo almacén que el PC** (servidor de Aldune o WebDAV) sin que el usuario tenga que pensar en ello, sin
perder nunca una edición y sin cambiar el formato 4. Al acabar F5: una nota creada en Android aparece en el PC y al revés; un conflicto provocado se guarda;
un borrado firmado borra y uno sin firma manda a la papelera; todo contra el servidor real del CT 103 con un perfil de prueba.

Fuera de F5: abrir/proteger/desproteger notas protegidas y resolver conflictos (F7; en F5 se **guardan y se señalan**, §5), más de un perfil, carpeta compartida,
iniciar una rotación de clave, QR, captura y widget (F6).

## 1. Decisiones del usuario (18-10)

| Tema | Decisión |
|---|---|
| Servidor que no responde | **Silencioso** **(usuario)**: la app sigue en local; el indicador pasa a «Sin conexión · última sync hace X»; se reintenta en el siguiente disparador y con WorkManager cuando hay red. Ni avisos emergentes ni reintento agresivo. |
| Qué se muestra | **Icono + detalle** **(usuario)**: icono discreto en la barra de la lista (sin configurar no se dibuja; ok / sincronizando / sin conexión / error). Al tocarlo: «Última sync hace 3 min» o el error en texto claro y «Sincronizar ahora». En Ajustes → Sincronización, lo mismo más el último error completo. No hay aviso tras cada sync. |
| Alcance | **El del código** **(usuario)**: todas / elegidas / etiqueta, como en Windows. Se muestra en Ajustes y no se edita en el móvil. |
| Primera conexión con notas locales | **Preguntar** **(usuario)**: «Subir estas N notas al perfil» o «Descartarlas de este dispositivo y quedarme con las del perfil». Sin notas locales, se conecta sin preguntar. |
| Protegidas que llegan antes de F7 | **Candado, solo lectura** **(usuario)**: se guardan enteras (`protectedContent` intacto), salen con candado y «Nota protegida» y, al abrirlas, «Se podrá abrir en una próxima versión». Se pueden archivar, mandar a la papelera, colorear y mover (eso viaja); el texto no se toca. |
| Conflictos antes de F7 | **Guardar + aviso** **(usuario)**: tabla `sync_conflict` (migración 1→2 de Room) e icono en la fila de la nota. Resolverlos llega en F7. La ganadora ya está aplicada: no se pierde nada. |

## 2. Reparto: un módulo puro nuevo, `android/syncclient`

```
android/
  core/ sync/ text/   sin cambios (salvo la excepción tipada de §4.6, solo si hace falta)
  domain/             + campos de protegida en Note y sus reglas (§3)
  syncclient/         NUEVO. Kotlin/JVM puro. Depende de :domain, :sync, :core. Puente entre las notas del dominio y el motor:
                      foto local → SyncInput, ejecución en dos fases, contrato LocalSyncStore (+ su batería de pruebas reutilizable),
                      disparadores, estado para la interfaz, validación del código de perfil, primera conexión.
  app/                Room (implementa LocalSyncStore), secretos en el Keystore, pantallas, WorkManager. Casi sin reglas propias.
```

**Por qué un módulo y no `domain`:** `domain` se queda sin OkHttp ni formato de sync, y el trabajo de la nube va a una carpeta nueva sin riesgo para los 414
tests actuales. Paquete `app.aldune.syncclient`. Mismas opciones de compilación que los otros módulos puros (Java 11, JUnit 5).

**Dónde se hace:** todo `syncclient` y el cambio de `domain` en una **sesión de nube** con TDD (resultado en `INFORME-NUBE-F5.md`). `app` se hace luego aquí, en local, con los dispositivos (§7).

## 3. Cambios en `domain`

- `Note` gana `isProtected: Boolean = false` y `protectedContentJson: String? = null`: el JSON de `ProtectedNoteContent` **tal cual llegó**, opaco para `domain`
  (que no depende de `core`). Una protegida tiene `text == ""`. Room ya tiene las columnas `isProtected` y `protectedContent` (F4 las reservó).
- `NoteService`:
  - `edit` sobre una protegida devuelve un error (`NoteResult.Err`, código nuevo «protegida») y no escribe.
  - `setColor`, `setTags`, `archive`, `trash`, `restore`, `move`, `deleteForever`, `purgeTrashed`: igual que con una nota normal, y **conservan** `isProtected` y
    `protectedContentJson` sin tocarlos.
  - `list(query = no vacío)`: las protegidas no salen (su texto no se conoce); con la búsqueda vacía sí salen.
  - Tampoco se encuentran por sus etiquetas: una búsqueda no vacía nunca devuelve una protegida (más simple y más prudente).
- `NoteTitleHelper`/vista previa de una protegida: lo decide `app` (título de relleno de `strings.tsv`), no `domain`.

## 4. `syncclient`

### 4.1 La foto local y la construcción del `SyncInput`

`LocalSnapshot` (leída en **una** transacción, §4.2): notas (`domain.Note`), lápidas (`domain.Tombstone`), bases (`noteId` → `SyncBase`).

`buildSyncInput(snapshot, settings, deviceId, now): SyncInput`:

- `Note` → `LocalNote(SyncNote, authorDeviceId = note.syncAuthorDeviceId)`.
- Ids `String` → `UUID` (Core los escribe en minúsculas con guiones; `RandomIdFactory` también). Un id que no es UUID es un fallo de programa: excepción, no se salta.
- `Instant` → `OffsetDateTime` en **UTC** y **truncado a 100 ns** (`createdAt`, `updatedAt`, `deletedAt`, `now`). Así la cabecera y el contenido cifrado coinciden
  (comentario de `toTicks` en `SyncPlanner.kt`) y la misma nota da el mismo sobre en dos pasadas seguidas.
- `text` = `note.wireText` (devuelve el CRLF de una nota de Windows; una nota de Android va en LF). Protegida: `text = ""` aunque llegue otra cosa (el motor
  también lo fuerza en `forSync`; aquí se fija con test desde el lado de la entrada), `isProtected = true`, `protectedContent` = el JSON parseado.
  `protectedContentJson` ilegible = fallo de programa (lo escribió la propia app al aplicar).
- `state` = `NoteState.code`; `tags`, `color`, `dockPosition`, `screenOrigin` **tal cual** (sin normalizar: E28).
- Lápidas: `LocalTombstone(noteId, deletedAt, deviceId)` conservando el marcador `"local"` (el motor lo sustituye por el `deviceId` al firmar).
- `excludedIds` = vacío siempre (Android no tiene notas vinculadas a archivos).
- `scope`: del perfil guardado (`SyncScope.All` / `SelectedNotes` / `Tag`).
- `deviceId`: 32 hex en minúsculas (`UUID` sin guiones, como `Guid.ToString("N")` de Core), generado **una vez por instalación** y nunca cambiado (ni al
  desconectar). Lo guarda `app`; `syncclient` da la función que lo genera y valida.

Y al revés, `toDomainNote(SyncNote, authorDeviceId): Note?` para aplicar: `LineEnding.detect` + `normalize` sobre el texto, `Instant` desde el instante,
etiquetas, color, posición y origen tal cual, `syncAuthorDeviceId = author`, protegida con su JSON (serializado con `core`, en el mismo orden de claves que
Core para poder compararlo). **Estado desconocido** (`NoteState.fromCode == null`, p. ej. un formato futuro): devuelve `null`; la acción se salta, su base no
se fija y se cuenta como «omitida» en el resultado (no se inventa un estado, porque al volver a subir se perdería).

### 4.2 Contrato `LocalSyncStore` (transaccional)

```kotlin
interface LocalSyncStore {
    /** Notas, lápidas y bases leídas en una sola transacción. */
    fun snapshot(): LocalSnapshot
    /** Aplica todo lo local de una pasada en UNA transacción; devuelve qué se aplicó y qué se saltó. */
    fun commit(commit: LocalCommit): CommitReport
    fun conflictedNoteIds(): Set<String>
    /** Primera conexión → «descartar»: borra notas, lápidas, bases y conflictos SIN crear lápidas. */
    fun discardAllLocal()
    /** Desconectar: borra bases y conflictos; las notas y las lápidas se quedan. */
    fun forgetSyncState()
}
```

`LocalCommit` lleva las acciones locales del plan (`ApplyNote`, `ApplyTombstone`, `TrashNote`, `SaveConflict`), las bases nuevas y, por nota, la
**versión esperada** que se vio en la foto (`updatedAt` + autor, o «no existía», o «había lápida»).

Semántica de cada acción, igual que Core:
- `ApplyNote`: inserta o reemplaza la nota con el autor remoto y **quita la lápida local** de ese id si la hay (`NotesRepository.cs`, `DELETE FROM SyncTombstone`
  en `ApplySyncNote`).
- `ApplyTombstone`: borra la nota y guarda la lápida con el `deviceId` del autor remoto (para no volver a firmarla como propia).
- `TrashNote`: estado papelera, `updatedAt = at`, autor `null` («este equipo», como `SetState`).
- `SaveConflict`: guarda el conflicto (versión perdedora entera); si ya había uno para esa nota, lo reemplaza.

**Concurrencia optimista** (la regla que evita perder ediciones): la sync tarda segundos en la red y el usuario puede editar mientras tanto. Dentro de la
transacción, para cada nota con acción local se compara el estado actual con la versión esperada. Si **no coincide**, todas las acciones locales de esa nota
se saltan y **su base no se fija**. Con la base vieja, la pasada siguiente ve los dos lados cambiados y guarda un conflicto real si lo hay, en vez de pisar la
edición en silencio. Las bases de notas **sin** acciones locales (solo subida, o contenido igual) se fijan siempre: lo que dicen es verdad en el almacén.
`CommitReport` dice cuántas se saltaron; si hay alguna, el disparador pide otra pasada pronto (§4.4).

**Batería de pruebas del contrato**: una clase abstracta (`LocalSyncStoreContract`) con todas las pruebas de esta sección, en un *source set* de
`testFixtures` (plugin `java-test-fixtures`) para que `app` la herede en sus pruebas instrumentadas con la implementación de Room. `syncclient` la pasa con
`InMemoryLocalSyncStore` (sobre `InMemoryNoteStore` de `domain`, que es también el `NoteStore` de `NoteService`: un solo almacén para las dos cosas, como
hará Room). Si `testFixtures` no funciona con AGP en `androidTest`, alternativa: la batería en un módulo `:syncclient-contract` con JUnit en `implementation`.

### 4.3 Ejecución en dos fases: `runSync`

```
foto  →  buildSyncInput  →  transporte.readAll  →  planSync  →  FASE 1: transporte (Upload, DeleteRemote, en el orden del plan)
                                                              →  FASE 2: commit local (una transacción, con las bases)
```

- **Las bases nunca se guardan antes de que el almacén tenga lo que dicen.** Si se escribieran antes y la subida fallara, la pasada siguiente vería la nota
  local «igual que la base» y aplicaría encima la versión vieja del almacén: se perdería la edición. Por eso la fase 2 va después de la 1.
- Si la fase 1 falla a medias: **no se escribe nada local**. Las subidas ya hechas son idempotentes; la pasada siguiente converge.
- **Nunca hay red dentro de la transacción de Room** (bloquearía las escrituras del editor hasta 20 s).
- El plan con error (`NOTHING_READABLE_ERROR`) no escribe nada en ninguna fase.
- `runSync` no usa `execute()` de `android/sync` (que intercala y no es transaccional); `execute` se queda para los vectores y sus pruebas.
- Devuelve `SyncOutcome`: contadores (subidas, recibidas, borrados aplicados, conflictos, omitidas, saltadas por edición concurrente) o un `SyncFailure` (§4.5).
- Una sola pasada a la vez: el que llama lo garantiza (§4.4); `runSync` no es reentrante y no lo intenta ser.

### 4.4 Cuándo se sincroniza: `SyncTriggerPolicy`

Máquina de estados pura, con reloj inyectado; `app` le pasa eventos y obedece sus decisiones (`RunNow`, `RunAt(instante)`, `Nothing`).

| Evento | Decisión |
|---|---|
| Sin perfil configurado | `Nothing` siempre |
| `AppOpened` | `RunNow` |
| `Foreground` (volver a primer plano) | `RunNow` si la última pasada **intentada** fue hace más de 30 s |
| `LocalChange` (cada guardado del editor o cambio de estado/color/orden) | `RunAt(último cambio + 5 s)`: cada cambio nuevo lo retrasa |
| `PullToRefresh`, `ManualSync` | `RunNow`, sin esperas ni límites |
| `Periodic` (WorkManager, ≥15 min, con red) | `RunNow` |
| `SyncFinished(outcome)` | si durante la pasada llegó algún evento, una repetición; si hubo acciones saltadas por edición concurrente, `RunAt(+5 s)` |

- **Una sola pasada a la vez**: un evento con una pasada en curso no lanza otra; marca «repetir al acabar» (una sola repetición, por muchos eventos que lleguen).
- **Tras un fallo** (cualquiera): las automáticas (`AppOpened`, `Foreground`, `LocalChange`, `Periodic`) esperan al menos **60 s** desde el fallo. Las manuales
  nunca esperan. No hay reintento creciente **(usuario: silencioso)**.
- La política no conoce Android ni WorkManager: `app` traduce `RunAt` a un `Handler`/corrutina mientras está en primer plano, y `Periodic` lo pide WorkManager.

### 4.5 Errores y estado para la interfaz

`classifyFailure(Throwable | planError): SyncFailure`, con `kind`:

| `kind` | Cuándo | Lo que verá el usuario (texto en `strings.tsv`) |
|---|---|---|
| `OFFLINE` | `UnknownHostException`, `ConnectException`, `SocketTimeoutException`, `InterruptedIOException` de `callTimeout`, sin ruta | «Sin conexión» |
| `UNAUTHORIZED` | 401 (servidor: token; WebDAV: usuario o contraseña) | «El servidor rechazó el token» / «Usuario o contraseña de WebDAV incorrectos» |
| `KEY_CHANGED` | `NOTHING_READABLE_ERROR` | «La clave de este perfil cambió: pega el código nuevo» |
| `SERVER_ERROR` | 5xx, 404 de la ruta base, respuesta que no es JSON/XML | «El servidor respondió con un error» |
| `TOO_LARGE` | 413 | «Una nota es demasiado grande para el servidor» |
| `UNSUPPORTED_FORMAT` | `SyncFormatException` de formato desconocido | «El almacén tiene notas de una versión más nueva de Aldune: actualiza la app» |
| `UNKNOWN` | lo demás | «No se pudo sincronizar» + detalle técnico solo en Ajustes |

- Se clasifica **por tipo de excepción y, si hace falta, por el código HTTP del mensaje**, con pruebas que usan los transportes reales contra MockWebServer
  (no mensajes escritos a mano en el test). §4.6 si el mensaje no basta.
- `SyncStatus` (lo que guarda `app`): `lastSuccessAt`, `lastAttemptAt`, `lastFailure`, `running`. `formatStatus(status, now): StatusLine` devuelve **claves y
  números**, no texto: `icon` (`NONE`, `OK`, `SYNCING`, `OFFLINE`, `ERROR`), `message` (enum que `app` traduce a `R.string`) y un `RelativeTime` para «hace…»:
  `JUST_NOW` (< 1 min), minutos (< 60), horas (< 24), días (< 7) y, a partir de ahí, la fecha (que formatea `app` con el `Locale`). `OFFLINE` lleva el icono
  de sin conexión, no el de error (no es culpa del usuario). Sin perfil: `NONE` (no se dibuja). Nunca sincronizado: «Aún no se ha sincronizado».
- El detalle técnico del último error (el mensaje en inglés del transporte) solo se ve en Ajustes y en el diagnóstico, **sin contenido de notas**.

### 4.6 Excepción tipada en `android/sync` (solo si hace falta)

Hoy el 401 de HTTP es `IllegalStateException` y el resto `IOException` con el código en el mensaje. Si clasificar por mensaje resulta frágil (p. ej. WebDAV no
lo distingue), se permite **un** cambio en `android/sync`: `SyncTransportException(val status: Int?, message)` que herede del tipo que ya se lanza en cada caso,
**con el mismo mensaje**, y sin tocar nada más. Los 119 tests de `sync` y los vectores tienen que seguir verdes.

### 4.7 Validar el código de perfil pegado

`parseProfileInput(pasted: String): ProfileParse`:
- Limpia lo que añade copiar de un chat o un correo: espacios, saltos de línea y tabuladores **en cualquier sitio** (el base64url no los lleva), comillas o
  `<>` alrededor.
- `ProfileCodes.decode`. Resultado:
  - `Server(url, scope, tag, noteIds, profileName, key)`: pide el token.
  - `WebDav(url, …)`: pide usuario y contraseña de aplicación.
  - `NeedsTransport(…)` si el código no trae `Transport` (códigos antiguos): la interfaz pide elegir servidor o WebDAV y escribir la URL.
  - Errores: `EMPTY`, `NOT_A_CODE` (no empieza por un prefijo conocido), `INVALID` (base64/JSON roto, versión no soportada, clave mala),
    `FOLDER_NOT_SUPPORTED` («Este código es de una carpeta compartida; en el móvil usa servidor o WebDAV»).
- `validateCredentials(transport, url, token | usuario+contraseña)`: URL absoluta `http`/`https` (se admite `http`: el servidor de casa va por LAN), token o
  usuario no vacíos tras recortar; para el token, si se pegó la línea entera `ALDUNE_SYNC_TOKEN=…`, se queda con lo de después del `=` (es el error típico que
  ya avisa el mensaje del 401).
- La clave nunca se registra ni se muestra.
- «Probar conexión» (en `app`) = `transport.readAll()` + `classifyFailure`; no escribe nada.

### 4.8 Primera conexión y desconexión

- `firstConnectDecision(snapshot): Proceed | AskUser(noteCount)`: pregunta si hay **alguna nota** en cualquier estado (activa, archivada o papelera).
  Lápidas sin notas: no preguntan; se descartan en silencio con `forgetSyncState` + borrado de lápidas (eran borrados de notas que el perfil no conoce).
- «Subir»: se guarda el perfil y se lanza una pasada normal (el motor sube lo local sin base: no hay conflicto porque el almacén no tiene esos ids).
- «Descartar»: `discardAllLocal()` **sin crear lápidas** (si se crearan, subirían borrados firmados al perfil) y luego la pasada.
- «Desconectar»: borra clave, token y contraseña (en `app`) y llama a `forgetSyncState()`: las bases son de ese perfil y no valen para otro. Las notas se quedan.
  Al volver a conectar, con notas locales, vuelve a preguntar.

## 5. `app` (después, en local)

1. Room v2: tabla `sync_conflict` (noteId, detectedAt, versión ganadora: fecha + dispositivo + tombstone; perdedora: cifrada con `NoteCipher`, mismo modelo
   que el texto) y migración 1→2 con su prueba. `RoomLocalSyncStore` (sobre la misma base que `RoomNoteStore`) que pasa `LocalSyncStoreContract` en el
   dispositivo.
2. Secretos: clave de sync, token y contraseña de WebDAV envueltos por el Keystore en `noBackupFilesDir` (spec madre §4.2); `deviceId` y perfil (sin secretos) en
   `SettingsStore`.
3. Ajustes → Sincronización: pegar código, token o usuario+contraseña, «Probar conexión», alcance (solo lectura), última sync, último error, «Sincronizar ahora»,
   «Desconectar» (con confirmación). Diálogo de primera conexión. Aviso de que la copia del sistema no guarda las notas: la recuperación es volver a pegar el código.
4. Lista: icono de estado en la barra con su detalle; tirar para sincronizar; candado y conflicto en la fila; protegida al abrirse → aviso de solo lectura.
5. Disparadores: `ProcessLifecycleOwner` (abrir, primer plano), guardados del `NotesViewModel` (`LocalChange`), WorkManager periódico de 15 min con
   `NetworkType.CONNECTED` que llama a la misma función y respeta la pasada única (un `Mutex` del proceso).
6. Textos nuevos en `android/strings.tsv` con los cinco idiomas y `python android/scripts/gen_strings.py` (+ `--check`).
7. Probar el editor con Gboard **y** un teclado de terceros tras los cambios (el guardado dispara la sync: no puede notarse al escribir).

## 6. Verificación (puertas)

1. `./gradlew test` verde: los 414 de antes (core 82, sync 119, text 100, domain 113) **sin perder ninguno** + los nuevos de `domain` y `syncclient`.
   Los vectores (`tests/vectors/`) no se regeneran ni se tocan; `kotlin-output.json` cambia con cada ejecución: descartarlo antes de commitear.
2. `syncclient` (JVM): pruebas de §4.1-§4.8, incluido un **extremo a extremo** con `InMemoryLocalSyncStore` + `NoteService` + el doble en memoria del transporte
   de `android/sync`, que demuestre: nota creada aquí sube; nota del «PC» se aplica con su autor y **no se resube** en la pasada siguiente; abrir y cerrar sin
   editar no genera subida (E28); CRLF vuelve igual; protegida sube con `text ""` aunque el `Note` traiga texto; gana la versión mayor con dos del mismo id;
   borrado sin firma → papelera y nunca borra; edición concurrente durante la fase 1 → no se pisa y la siguiente pasada guarda conflicto; fallo de red en la
   fase 1 → nada local escrito; dos pasadas seguidas sin cambios → la segunda no hace nada.
3. `app` (instrumentadas): `LocalSyncStoreContract` con Room, migración 1→2.
4. **Sondas** contra el servidor del CT 103 con un **perfil de prueba** (nunca el perfil real del usuario; la app de depuración tiene su propia base): nota
   creada en Android aparece en el PC (con una instalación de Windows de pruebas o una base temporal, nunca `%LOCALAPPDATA%\Aldune`) y al revés; conflicto
   provocado (editar en los dos con la red cortada); borrado firmado; borrado sin firma (sobre escrito a mano en el almacén de prueba); servidor apagado →
   «Sin conexión» sin avisos; token malo → mensaje claro; modo avión y vuelta; en el Pixel y en la tableta.

## 7. Tareas

| # | Tarea | Dónde |
|---|---|---|
| 1 | `domain`: campos y reglas de protegida (§3) | **Nube** |
| 2 | `syncclient`: §4.1-§4.8 con TDD y `LocalSyncStoreContract` | **Nube** |
| 3 | Room v2 + `RoomLocalSyncStore` + contrato en el dispositivo | Local |
| 4 | Secretos, `SettingsStore` del perfil, Ajustes → Sincronización, primera conexión | Local |
| 5 | Indicador, tirar para sincronizar, candado y conflicto en la fila, disparadores, WorkManager | Local |
| 6 | Sondas contra el CT 103 en Pixel y tableta; `INFORME-ANDROID-F5.md` | Local |

## 8. Riesgos

1. **`testFixtures` en `androidTest`**: si AGP no lo consume bien, módulo `:syncclient-contract` (§4.2). Se descubre en la tarea 3.
2. **Reloj del móvil desajustado**: las versiones se ordenan por fecha. Un móvil adelantado gana siempre; atrasado, pierde. Es igual que en Windows y no se
   corrige en F5; si aparece, se anota.
3. **WorkManager en la tableta Lenovo**: algunos fabricantes retrasan o matan trabajos en segundo plano. No se promete periodicidad (spec madre §2.6); se mira en
   la sonda y se anota.
4. **LF en notas creadas en Android** (F4 §1.2/§6.3): la sonda 4 lo comprueba de verdad. Si Windows reescribe CRLF y crea versiones nuevas, se arregla en Windows.
5. **Coste de la nube**: tope ≈10 $ (saldo ≈22 $ el 17-10, caducan el 5-11-2026). Si se acaba, lo que falte de la tarea 2 se termina aquí.
