# Prompt de nube: motor de sync Kotlin (`android/sync`) contra los vectores de escenario

> Para pegar en una sesión de Claude Code en la nube (Sonnet) sobre el repo público `NachoPola16/aldune`.
> **Requisito previo:** F0 hecha (2026-10-17): `tests/vectors/sync-scenarios.json` (34 escenarios) y su sección en `tests/vectors/README.md`
> tienen que estar en `main` (commitearlos antes de lanzar la sesión).
> Presupuesto objetivo: 10-12 $. El trabajo va en tareas pequeñas con commit e informe tras cada una, para que si se corta a
> mitad quede algo usable. Referencia de tamaño: la sesión de `android/core` (8 tareas, 82 tests).

---

```
Eres una sesión de trabajo sobre el repositorio aldune (app de notas para Windows con un cliente Android en Kotlin en camino).
Antes de nada lee: AGENTS.md, docs/superpowers/specs/2026-10-17-aldune-android-design.md (secciones 1, 3, 3.1 y 6),
tests/vectors/README.md, docs/INFORME-ANDROID-CORE.md y el código de android/core. La referencia de comportamiento es
src/Aldune.Core/SyncService.cs (método SynchronizeCore) y src/Aldune.Core/SyncScopeFilter.cs; léelos, pero NO los modifiques.

IDENTIDAD GIT (obligatorio, antes del primer commit):
  git config user.name "NachoPola16"
  git config user.email "nachopolac@gmail.com"
Ningún commit puede llevar Co-Authored-By, "Generated with" ni mención a Claude/Anthropic. Antes de terminar comprueba
  git log --format='%an <%ae> | %cn <%ce>' origin/main..HEAD
y que solo aparece NachoPola16 <nachopolac@gmail.com>. Si aparece otra identidad, corrígelo antes del push.

RAMA: crea nube/android-sync desde main y trabaja solo ahí. Haz push de la rama al acabar cada tarea. No abras PR ni toques main.

COMPROBACIÓN PREVIA: si no existe tests/vectors/sync-scenarios.json, no inventes los vectores: escribe en el informe
"falta F0" y para. Los vectores los genera Core en Windows; nunca los edites a mano ni los regeneres.

ENTORNO: JDK 21 y Gradle del sistema (no hay wrapper en el repo; no añadas binarios). Ejecuta con
  cd android/sync && gradle test
android/core se prueba igual (cd android/core && gradle test). Su test KotlinOutputTest reescribe tests/vectors/kotlin-output.json en
cada ejecución: NO lo commitees (git checkout -- tests/vectors/kotlin-output.json antes de cada commit).

QUÉ CONSTRUIR: el módulo android/sync, Kotlin/JVM puro (sin Android), con el mismo jvmTarget 11 / --release 11 que android/core.
Es su propia compilación: android/sync/settings.gradle.kts con includeBuild("../core") y dependencia "app.aldune:aldune-core".
No modifiques nada dentro de android/core, src/ ni tests/Aldune.*. Comentarios en español, explicando el porqué.

DISEÑO OBLIGATORIO (spec §3.1): separar decisión y efectos.
- SyncInput: notas locales (con autor), borrados locales, bases por nota, ids excluidos, objetos del almacén (SyncEnvelope de
  android/core), alcance (todas / elegidas / etiqueta), deviceId.
- planSync(input, key): SyncPlan — función pura. Acciones por nota + bases nuevas, o error.
- execute(plan, LocalStore, SyncTransport) — interfaces propias de este módulo, con dobles en memoria para los tests.
- Replica Core incluidas sus rarezas; si un vector contradice tu lectura de SyncService.cs, manda el vector: anótalo en el informe.
- Formato de los vectores: lee la sección «Vectores de escenario» de tests/vectors/README.md. En `expected`, `"<now>"` es una fecha puesta
  por el reloj (mandar a la papelera un borrado sin firma): inyecta un reloj en execute y comprueba solo que se usó. `remoteAfter` lista lo
  que queda en el almacén; un objeto que desaparece es un borrado del almacén; `unreadable` = formato desconocido, debe quedar intacto.
  Los escenarios no incluyen E14 ni E21 ni varias versiones del mismo id: esos van en tests propios.

TAREAS, por orden (commit + push + informe después de cada una; si te quedas sin margen, para tras la última completa):
0. Andamiaje Gradle de android/sync y un test que carga sync-scenarios.json y cuenta los escenarios.
1. Tipos (SyncInput, SyncPlan, acciones, LocalStore, SyncTransport) y dobles en memoria. Sin lógica aún.
2. planSync para los escenarios sin borrados ni alcance (subir, aplicar, iguales, bases, conflicto real, OnlyMigrated, empate por
   deviceId, protegidas). Test parametrizado: un caso por escenario del JSON; compara expected.actions, conflicts y basesAfter.
3. Borrados: firmados, sin firma (papelera, fecha futura → borrar del almacén, firmado del mismo id → ignorar), borrado local.
4. Alcance: etiqueta (incluida la nota que salió de la etiqueta), notas elegidas, ids excluidos.
5. Errores: ninguna nota descifra → error y cero escrituras (pero un almacén con solo borrados no aborta); el formato desconocido se
   rechaza al deserializar, no en planSync. Más dos tests propios (no salen de vectores): varias versiones del mismo id en el almacén
   (gana la mayor) y que una protegida desbloqueada sube siempre con text "".
6. execute + comprobación de localAfter y remoteAfter con los dobles. Todos los escenarios verdes.
7. (Solo si sobra margen) HttpSyncTransport con OkHttp contra MockWebServer, copiando las rutas y códigos de
   src/Aldune.Core/HttpSyncTransport.cs y src/Aldune.SyncServer/Program.cs. WebDAV NO: es otra sesión.

INFORME INCREMENTAL: docs/INFORME-ANDROID-SYNC.md, con el mismo esquema que docs/INFORME-ANDROID-CORE.md: entorno, una casilla por
tarea con su resultado (tests verdes/rojos, número), diferencias encontradas entre SyncService.cs, el README y los vectores, y lo que
queda sin hacer. Actualízalo y commitéalo al terminar CADA tarea, aunque sea con "a medias".

AL ACABAR: gradle test completo en android/sync (y android/core sin cambios en verde), comprobación de identidad git, push de la rama
y un resumen de 10 líneas: qué escenarios pasan, cuáles no y por qué, y qué harías en la siguiente sesión.
```
