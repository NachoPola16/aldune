# Informe de la sesión de nube — F5 (parte Kotlin/JVM pura)

> 2026-10-18. Especificación: [`2026-10-18-aldune-android-f5-design.md`](../../superpowers/specs/2026-10-18-aldune-android-f5-design.md) §3 y §4 enteros, más el
> extremo a extremo de §6.2. Nada de §5 (`app`). **Los 10 pasos están hechos**; `core`, `text` y `tests/vectors/` intactos, y `android/sync` sin tocar
> (la excepción tipada de §4.6 no hizo falta).

## Resultado

`./gradlew :core:test :sync:test :text:test :domain:test :syncclient:test --rerun-tasks` en verde.

| Módulo | Antes | Después |
|---|---|---|
| core | 82 | 82 |
| sync | 119 | 119 |
| text | 100 | 100 |
| domain | 113 | **121** (+8: `NoteServiceProtectedTest`) |
| syncclient | — | **165** |

`syncclient` por fichero de pruebas: `SyncMappingTest` 26, `InMemoryLocalSyncStoreTest` 26 (la batería del contrato), `SyncFailureTest` 20,
`SyncRunnerTest` 15, `SyncTriggerPolicyTest` 27, `SyncStatusTest` 11, `ProfileInputTest` 25, `EndToEndSyncTest` 15.

Commits (uno por paso, autor y committer `NachoPola16`, sin `Co-Authored-By`): domain · módulo y `settings` · mapeo · contrato · clasificación de fallos ·
`runSync` · política de disparadores · estado · perfil y primera conexión · extremo a extremo (+ este informe).

## Qué se hizo

1. **`domain`**: `Note.isProtected` y `Note.protectedContentJson`; `NoteError.Protected`; `NoteService.edit` rechaza una protegida antes de tocar nada (ni
   siquiera anota `editedAt`); el resto de operaciones ya conservaban los campos con `copy` y ahora lo fijan los tests; `list` con búsqueda no vacía
   (según `NoteSearch`: lo que queda tras recortar) nunca devuelve una protegida, ni por etiquetas. El filtro por etiqueta **sin** búsqueda sí la deja
   (es navegación, no mira el texto).
2. **`android/syncclient`** (`app.aldune.syncclient`, Java 11, JUnit 5, `java-test-fixtures`; `api` a `:domain`, `:sync`, `:core`).
3. `buildSyncInput` / `toDomainNote` / `DeviceIds`. 4. `LocalSyncStore`, `LocalSnapshot`, `LocalCommit` (+ `ExpectedVersion`, `LocalAction`), `CommitReport`,
   `InMemoryLocalSyncStore`, `LocalSyncStoreContract` en `testFixtures` y su subclase en `src/test`. 5. `runSync` → `SyncOutcome`. 6. `SyncTriggerPolicy`.
7. `classifyFailure`, `classifyPlanError`, `formatStatus`, `relativeTime`. 8. `parseProfileInput`, `validateCredentials`, `firstConnectDecision`,
   `completeFirstConnect`. 9. `EndToEndSyncTest` con todos los casos de §6.2 (y dos más: dos teléfonos que convergen y un borrado firmado que viaja entre ellos).

La batería del contrato **detecta** el fallo de concurrencia: con la comparación de versiones desactivada, 7 de sus pruebas fallan.

## Decisiones donde la spec no decía nada (revisar)

- **`settings.gradle.kts` y raíz de Gradle.** Sin SDK de Android no se puede ni configurar la raíz: `build.gradle.kts` declara AGP, KSP y Room (todos en Google
  Maven, que desde la nube da 403). No bastaba con quitar `:app`. Ahora `settings` calcula `hasAndroidSdk` (`ANDROID_HOME`/`ANDROID_SDK_ROOT` o `sdk.dir` en
  `local.properties`); **con SDK nada cambia** (incluye `:app` y usa `build.gradle.kts` de siempre); **sin SDK** excluye `:app`, usa
  `build.jvm.gradle.kts` (solo los plugins Kotlin) y avisa por consola. Riesgo: en un PC con SDK pero sin `ANDROID_HOME` ni `local.properties` `:app`
  desaparecería (con el aviso). No he podido compilar `:app` aquí: **sin verificar** con SDK.
- **Orden de pasos.** El paso 7 (`classifyFailure`) se hizo antes que el 5 (`runSync`), porque `runSync` devuelve un `SyncFailure`. `formatStatus` quedó en su sitio.
- **Clasificación sin excepción tipada.** Basta tipo de excepción + `answered NNN` / `(401)` del mensaje de los transportes reales (probados con MockWebServer: si
  cambia un mensaje, esas pruebas fallan). `OFFLINE` = cualquier `SocketException`, `InterruptedIOException` o `UnknownHostException` en la cadena de causas.
  **403, 400, 429 → `UNKNOWN`** (la spec solo da 401; con httpStatus guardado para decidirlo después). Un `SyncFormatException` de formato *más viejo* (0) no se
  presenta como «versión nueva»: `UNKNOWN`. Un fallo del almacén local en el commit también sale como `UNKNOWN`.
- **`runSync`**: los datos locales que no cuadran con el formato (id que no es UUID **canónico**, JSON protegido ilegible, `deviceId` mal formado) lanzan
  excepción, no `Failure`. Las acciones de una nota «omitida» (estado desconocido) se descartan **enteras** (también su conflicto) y su base no se fija. Un plan sin
  acciones locales ni bases no abre transacción. Los contadores de `Success` son lo realmente aplicado (`CommitReport`), no lo planeado.
- **Contrato**: las acciones locales están en tipos del dominio (`LocalAction`, con `Note` ya convertida): la conversión y la omisión las hace `runSync`, no cada
  almacén. `ExpectedVersion` = `Absent` | `Existing(updatedAt, autor)` | `Tombstoned`. Una acción sin versión esperada es un fallo de programa (`require` en
  `LocalCommit`). `TrashNote` de una nota inexistente no hace nada. Para ello se añadieron a `InMemoryNoteStore` (no a la interfaz `NoteStore`) `putTombstone`,
  `removeTombstone`, `removeWithoutTombstone` y `clear`; Room los hará con SQL. La «transacción» en memoria es el monitor del propio `InMemoryNoteStore`.
- **`testFixtures`** usa `@Test`/`@BeforeEach` de JUnit 5 directamente: las anotaciones de `kotlin-test` solo se resuelven en el source set `test`. Si `app` las
  hereda en `androidTest` hace falta JUnit 5 allí o portar la batería a JUnit 4 (riesgo 1 de §8, sin resolver; el plan B es `:syncclient-contract`).
- **`SyncTriggerPolicy`**: API `onEvent(SyncEvent): SyncDecision` + `setProfileConfigured`. Añadí `TimerFired` (para que la app avise al vencer un `RunAt`; un cambio
  posterior retrasa la hora y el temporizador viejo recibe un `RunAt` nuevo) y `SyncFinished(outcome)` (obligatorio, también en fallo). `RunNow` da la pasada por
  en curso. «Hace más de 30 s» es estricto (a los 30 s justos no) y se cuenta desde el inicio del último intento, fallido o no. Tras un fallo: las automáticas
  (`AppOpened`, `Foreground`, `Periodic`) dentro de los 60 s devuelven `Nothing`; un `LocalChange` se retrasa a `max(cambio+5 s, fallo+60 s)`; las manuales y
  la repetición manual pendiente no esperan; las automáticas que esperaban durante una pasada fallida se descartan (silencioso); un éxito levanta la espera.
  Con notas saltadas por edición concurrente: `RunAt(ahora+5 s)`.
- **Estado**: `formatStatus(SyncStatus?, now)`; `null` = sin perfil → `NONE`. Un perfil que nunca sincronizó lleva el icono `OK` (no hay nada roto; los iconos de
  §4.5 no incluyen uno neutro). `SYNCING` manda sobre un fallo anterior. Todo fallo conserva «última sync hace…» si la hay y lleva el `SyncFailure` entero
  (con el detalle técnico para Ajustes). `RelativeTime`: `JustNow`/`Minutes`/`Hours`/`Days`/`Date`, siempre redondeando hacia abajo; una hora por delante del
  reloj es `JustNow`.
- **Código de perfil**: `parseProfileInput` quita espacios (también duros), saltos, tabuladores y caracteres de ancho cero **en cualquier sitio**, y comillas
  rectas/tipográficas, comillas invertidas o `<>` de los extremos. «Mi código es: aldune-profile-…» → `NOT_A_CODE` (no se adivina dónde acaba). Cualquier fallo de
  `ProfileCodes.decode` → `INVALID`. `validateCredentials(url, ProfileCredentials.Token | Basic)` devuelve `Valid` (valores ya limpios) o `Invalid(errores)` con
  todos los fallos a la vez; la contraseña **no** se recorta (solo se exige no vacía). La URL se devuelve recortada **sin** forzar «/» final: igual que Windows,
  `https://host/aldune` sustituye `aldune` al resolver las rutas; `app` debería avisar de que una subcarpeta termina en «/».
- **Primera conexión**: `firstConnectDecision` → `Proceed(dropLocalTombstones)` | `AskUser(noteCount)`; añadí `completeFirstConnect(store, decision, choice)` que usa
  `forgetSyncState` / `discardAllLocal` (el caso «lápidas sin notas» es `discardAllLocal`, que no crea lápidas, así que no hizo falta un método nuevo en el contrato).
  «Subir» conserva notas **y lápidas** (la spec solo manda descartar las lápidas cuando no hay notas).

## Cosas que ya eran así y conviene saber

- Una nota con saltos de línea **mezclados** (`\n` y `\r\n`) pierde la mezcla al pasar por `Note` (se decide por el primer salto y `normalize` convierte todos): al
  volver a subirla sale toda con uno solo y habría una versión nueva. Es el diseño de `LineEnding` de F4; no lo toqué.
- Un conflicto cuya ganadora es la versión local no cambia la nota local: solo guarda el conflicto y sube (comportamiento del motor).

## Qué quedó sin hacer / sin verificar

- Nada de la lista quedó sin hacer. Sin verificar por falta de SDK: que `:app` siga configurando con el nuevo `settings`, y `LocalSyncStoreContract` ejecutado desde
  `androidTest` con Room (tarea 3 de §7).
- Dependencias: solo `kotlinx-serialization-json` (ya en el catálogo) para leer/escribir el JSON del contenido protegido con el formato de `core`; ninguna nueva.
- `kotlin-output.json` se descartó antes de cada commit.
