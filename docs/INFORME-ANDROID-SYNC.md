# Informe de la sesión en la nube — android/sync

Rama: `claude/upbeat-archimedes-br02lb` (el prompt pedía `nube/android-sync`; la sesión está fijada a esta rama y no se empuja a otra).
Módulo: `android/sync` (Kotlin/JVM puro, Gradle KTS, JUnit 5, `includeBuild("../core")`).

## Entorno
- Dependencias de runtime de `android/sync`: `aldune-core`, kotlinx-serialization-json 1.8.0 y OkHttp 4.12.0; de test, JUnit 5.11.4 y MockWebServer 4.12.0.
- JDK 21, Gradle 8.14.3 del sistema; sin wrapper ni binarios en el repo: `cd android/sync && gradle test`.
- `jvmTarget` 11 y `--release 11`, igual que `android/core`.
- Maven Central devolvió 429 (límite de peticiones del egreso compartido) en las primeras resoluciones; reintentando con pausas se descargó todo
  y Gradle lo guarda en caché.

## Tareas
- [x] 0. Andamiaje Gradle + test que carga `sync-scenarios.json` y cuenta los escenarios (34). Verde.
- [x] 1. Tipos (`SyncTypes.kt`: `SyncInput`, `SyncPlan`, `SyncAction`, `LocalStore`, `SyncTransport`) y dobles (`InMemoryDoubles.kt`). Compila; sin lógica.
  - Decisión de diseño: el reloj entra como `SyncInput.now` (no en `execute`). El plan lleva ya la nota mandada a la papelera con su fecha nueva
    y la sube en la misma pasada (E12a), así que el plan necesita la hora; leerla una vez fuera mantiene `planSync` puro.
    El prompt decía «reloj en execute»; `execute` no necesita ninguno. En los tests, `<now>` se comprueba con **igualdad** con `SyncInput.now`
    (más estricto que «reciente»).
- [x] 2. `planSync` (`SyncPlanner.kt`) para los escenarios sin borrados ni alcance: E01-E10, E16, E22, E25, E28 → 14 casos verdes (plan) y verdes
  también de punta a punta (tarea 6). Se escribió el arnés antes (`ScenarioHarness.kt`, `ScenarioChecks.kt`).
- [x] 3. Borrados: E11, E12a/b/c, E13, E15, E27a/b verdes. Más el caso propio de E14 (borrado sin firma junto a uno firmado del mismo id).
- [x] 4. Alcance: E17, E18a/b, E19, E20, E26 verdes. Más E21 (id excluido) como test propio.
- [x] 5. Errores: E23a/b y E24 verdes. Tests propios: varias versiones del mismo id (gana la mayor y, en empate, el `deviceId` mayor), protegida
  desbloqueada que sube con `text ""`, base64 roto = ilegible, plan con error = cero escrituras.
  - `planSync` es **una sola función** (como `SynchronizeCore`), así que las tareas 2-5 se implementaron juntas y se verificaron por grupos de
    escenarios (`PlanScenarioTest` tiene un grupo por tarea). Por eso hay dos commits para 2-6 y no uno por tarea.
- [x] 6. `execute` (`SyncExecutor.kt`) + comprobación de `localAfter`, `localTombstonesAfter`, `remoteAfter`, `basesAfter` y conflictos con los dobles.
  **Los 34 escenarios verdes** (en `planSync` solo y de punta a punta). Test extra de idempotencia: una segunda pasada tras la primera no hace nada.
- [x] 7. `HttpSyncTransport` (`HttpSyncTransport.kt`, OkHttp 4.12.0; MockWebServer 4.12.0 solo en test): 11 tests verdes contra un MockWebServer.
  Rutas y códigos copiados de `HttpSyncTransport.cs` y `Program.cs`: `GET api/v1/objects` + `GET api/v1/objects/{id sin guiones}`, `PUT`/`DELETE` del mismo
  camino, `Authorization: Bearer`. 401 → `IllegalStateException` con el mensaje de Core; otros códigos (404, 413, 5xx) → `IOException`; sin respuesta → timeout
  (`callTimeout`, 20 s por defecto); respuesta que no es JSON → `IOException`; un objeto de formato 5 → `SyncFormatException` y **aborta la lectura entera**
  (HTTP no salta, la carpeta sí). Incluye un test de dos dispositivos sincronizándose a través de un servidor simulado con la regla «un PUT más
  antiguo no pisa lo guardado».
  - **No probado contra `Aldune.SyncServer` real** (puerta de F2) ni con el 413 de 5 MB real: el mock solo devuelve el código.
  - No se aplica ningún límite de tamaño al leer; Core tampoco.
  - Se sigue la resolución RFC 3986 de la URL base como `new Uri(base, relative)` de .NET: sin barra final se pierde el último segmento de la ruta.

## Estado final
- `cd android/sync && gradle test` → **93 tests, 0 fallos** (1 de andamiaje, 34 de plan, 35 de punta a punta, 12 propios, 11 del transporte HTTP).
- `cd android/core && gradle test` → 82 tests, 0 fallos, sin tocar (`tests/vectors/kotlin-output.json` restaurado tras ejecutarlo).
- No se ha tocado `src/`, `tests/Aldune.*` ni `android/core`.


## Diferencias encontradas (SyncService.cs / README / vectores / prompt)
1. **Los vectores no traen `expected.actions`**, que el prompt manda comparar: traen contadores (`uploaded`…), `remoteAfter`, `localAfter`,
   `localTombstonesAfter`, `basesAfter` y `conflicts`. Las pruebas del plan comparan contadores, error, conflictos y bases; el resto se comprueba
   ejecutando el plan contra los dobles y mirando el estado final.
2. **`FolderSyncTransport.Write` no pisa un objeto más reciente** (`SyncVersion.Compare(envelope, current) < 0` → vuelve sin escribir). Lo fija E12c:
   el motor cuenta `uploaded = 1` pero el almacén no cambia (`rewritten: false`). El README no lo menciona. El doble `InMemorySyncTransport`
   lo replica; el transporte HTTP/WebDAV tendrá que decidir lo suyo en F2 (el motor no lo sabe: cuenta la subida igualmente).
3. **Fechas con más de 7 decimales:** el reloj de Java puede dar más precisión que .NET (100 ns). Si esa fecha se usara tal cual para la nota mandada
   a la papelera, la cabecera y el contenido cifrado discreparían y la nota se rechazaría al leerla. `planSync` trunca `now` a 100 ns (hay test).
   Core no tiene el problema porque `DateTimeOffset` ya es de 100 ns.
4. **De la versión ganadora de un conflicto solo se compara fecha y dispositivo.** En los vectores el `winner` trae `tombstone: false` y `text: null`
   incluso cuando ganó un borrado (E27a): Core no guarda la ganadora completa («la ganadora es la nota viva»). La perdedora sí se compara entera.
5. **E24 (formato 5)** se trata como pide la spec: el objeto no llega a `planSync`, se rechaza al deserializar (`SyncFormatException`) y el arnés lo
   guarda aparte como ilegible; el doble comprueba que queda intacto. No hay diferencia con el vector.
6. **`excludedIds` (E21) no está en el JSON de los escenarios**: se prueba con un test propio.
7. Sin diferencias entre `SyncService.cs` y los vectores: ningún vector contradijo la lectura del código.

## Sin hacer
- WebDAV (otra sesión) y la prueba del transporte HTTP contra el servidor real (F2).
- Rotación de clave (`CompletePendingKeyRotation`): fuera de la v1 (Android no la inicia).
- `LocalStore`/`SyncTransport` reales (Room, OkHttp): solo hay dobles en memoria.
- No se ha ejecutado contra un `Aldune.SyncServer` real (la puerta de F2).
- Escenarios que siguen sin vector: varias versiones del mismo id (test propio), E14 (test propio), E21 (test propio), rotación de clave.
