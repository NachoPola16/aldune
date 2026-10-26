# Android: varios perfiles de sincronización — plan de implementación

> **Para agentes:** SUB-SKILL REQUERIDA: `superpowers:executing-plans` (o `subagent-driven-development`). Los pasos usan casillas `- [ ]`.

**Objetivo:** que Android pueda estar unido a varios perfiles de sync a la vez (p. ej. «todas mis notas» + la nota de la familia).

**Arquitectura:** cada perfil tiene un `profileId`, sus secretos en un fichero propio y su estado aparte. `sync_base` y `sync_conflict` pasan a clave `(profileId, noteId)`; notas y lápidas siguen globales. `LocalSyncStore` se instancia por perfil (vista filtrada); `runSync` y el planner no cambian. `SyncController` recorre los perfiles en cada pasada.

**Tecnología:** Kotlin, Room 3 (esquema v3), Compose, WorkManager (sin cambios). Tests JVM en `android/syncclient`, instrumentados en `android/app`.

**Spec:** `docs/superpowers/specs/2026-10-26-android-varios-perfiles-design.md`

## Restricciones globales

- Formato de sync: sigue siendo el **4**. No tocar `tests/vectors/` ni `Aldune.Core`.
- Commits: autor y committer `NachoPola16 <nachopolac@gmail.com>`, sin `Co-Authored-By` ni mención a Claude. `git add` con ficheros nombrados. Comprobar identidad antes de commitear. **No hacer push, tag ni release.**
- Comentarios en español, explicando el porqué, con la densidad del código vecino. Textos de interfaz en `android/strings.tsv` (cinco idiomas: en, es, de, fr, pt-BR) y `python android/scripts/gen_strings.py` (`--check` debe pasar). Nada escrito a mano en Compose.
- Android: `JAVA_HOME=C:\Program Files\Eclipse Adoptium\jdk-21.0.12.101-hotspot`; `./gradlew` desde `android/`. `minSdk` 26.
- Lógica nueva con test primero. Nunca usar los datos reales del móvil: solo la compilación **debug** (`io.github.nachopola16.aldune.debug`, datos propios) y sin capturas de la pantalla de inicio ni de notificaciones.
- El perfil existente migra con el id fijo `"default"` (`LEGACY_PROFILE_ID`); los nuevos usan UUID. Así la migración de Room (SQL puro) y el fichero de secretos actual (`syncsecrets.bin`) no se mueven.
- Compatibilidad: una instalación con un perfil (v anterior) abre sin pasos manuales; sin perfil sigue sin perfil.

## Revisión (casos que ninguna tarea cubriría sola)

1. Conectar un **segundo perfil con notas locales**: nunca ofrece ni ejecuta «descartar»; las notas personales sobreviven (Tarea 1 y 4).
2. **Misma clave y misma URL pegadas dos veces**: se rechaza como ya conectado, no se duplica el perfil (Tarea 4).
3. **Un perfil falla** (token caducado) y el otro sincroniza bien: el segundo avanza, el primero queda marcado (Tarea 1 y 4).
4. **Borrar** una nota compartida: la lápida llega a los dos almacenes (Tarea 1).
5. **Conflicto en dos perfiles de la misma nota**: se resuelve uno y aparece el otro; no se pierde una versión en silencio (Tarea 2).

---

## Tarea 1: lógica pura de varios perfiles (JVM)

**Ficheros:**
- Crear: `android/syncclient/src/main/kotlin/app/aldune/syncclient/MultiProfile.kt`
- Modificar: `android/syncclient/src/main/kotlin/app/aldune/syncclient/ProfileInput.kt` (`firstConnectDecision`)
- Crear: `android/syncclient/src/test/kotlin/app/aldune/syncclient/MultiProfileTest.kt`

**Interfaces — produce (lo usan las tareas 3 y 4):**
- `const val LEGACY_PROFILE_ID = "default"`
- `fun vaultFileName(profileId: String): String` (`"syncsecrets.bin"` para `default`; `"syncsecrets-<id>.bin"` si no; `require` que el id sea `[A-Za-z0-9-]+`)
- `fun combineOutcomes(outcomes: List<SyncOutcome>): SyncOutcome`
- `fun aggregateStatus(statuses: Collection<SyncStatus>): SyncStatus`
- `fun firstConnectDecision(snapshot: LocalSnapshot, isFirstProfile: Boolean = true): FirstConnectDecision`

- [ ] **Paso 1: escribir los tests que fallan**

Crear `MultiProfileTest.kt`:

```kotlin
package app.aldune.syncclient

import app.aldune.domain.IdFactory
import app.aldune.domain.InMemoryNoteStore
import app.aldune.domain.NoteService
import app.aldune.sync.InMemorySyncTransport
import app.aldune.sync.SyncScope
import java.io.IOException
import java.net.UnknownHostException
import java.time.Instant
import java.util.UUID
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFailsWith
import kotlin.test.assertIs
import kotlin.test.assertNull
import kotlin.test.assertTrue

class MultiProfileTest {
    private val clock = TestClock(T0.plusSeconds(3600))
    private val notes = InMemoryNoteStore()
    private val service = NoteService(notes, clock, DEVICE, IdFactory { UUID.randomUUID().toString() })

    // Dos «perfiles»: dos vistas de sync (bases y conflictos propios) sobre las MISMAS notas, cada una con su almacén remoto.
    private val personal = InMemoryLocalSyncStore(notes)
    private val family = InMemoryLocalSyncStore(notes)
    private val personalRemote = InMemorySyncTransport()
    private val familyRemote = InMemorySyncTransport()

    private fun pass(store: InMemoryLocalSyncStore, remote: app.aldune.sync.SyncTransport, scope: SyncScope = SyncScope.All) =
        runSync(store, remote, TEST_KEY, SyncClientSettings(scope), DEVICE, clock)

    // --- vaultFileName ---

    @Test fun `el perfil heredado conserva el fichero de secretos de siempre`() {
        assertEquals("syncsecrets.bin", vaultFileName(LEGACY_PROFILE_ID))
    }

    @Test fun `un perfil nuevo tiene su propio fichero`() {
        assertEquals("syncsecrets-0b1c-22.bin", vaultFileName("0b1c-22"))
    }

    @Test fun `un id con ruta o raro no llega a ser nombre de fichero`() {
        assertFailsWith<IllegalArgumentException> { vaultFileName("../x") }
        assertFailsWith<IllegalArgumentException> { vaultFileName("") }
    }

    // --- dos perfiles sobre las mismas notas ---

    @Test fun `una nota compartida sube a los dos almacenes y las bases no se mezclan`() {
        val shared = service.create("lista de la compra").getOrThrow()
        service.create("solo mia").getOrThrow()

        assertEquals(2, assertIs<SyncOutcome.Success>(pass(personal, personalRemote)).uploaded)
        val onlyShared = SyncScope.SelectedNotes(setOf(UUID.fromString(shared.id)))
        assertEquals(1, assertIs<SyncOutcome.Success>(pass(family, familyRemote, onlyShared)).uploaded)

        assertEquals(2, personal.bases.size)
        assertEquals(setOf(shared.id), family.bases.keys)
        assertEquals(1, familyRemote.store.size)
        assertEquals(2, personalRemote.store.size)
    }

    @Test fun `si un perfil falla el otro sincroniza igual`() {
        service.create("hola").getOrThrow()
        val broken = object : app.aldune.sync.SyncTransport {
            override fun readAll() = throw UnknownHostException("sin red")
            override fun write(envelope: app.aldune.core.SyncEnvelope) = throw IOException("no")
            override fun delete(noteId: UUID) = throw IOException("no")
        }
        val outcomes = listOf(pass(personal, broken), pass(family, familyRemote))

        assertIs<SyncOutcome.Failure>(combineOutcomes(outcomes))
        assertTrue(personal.bases.isEmpty())
        assertEquals(1, family.bases.size)
    }

    @Test fun `borrar una nota compartida lleva el borrado firmado a los dos almacenes`() {
        val shared = service.create("lista").getOrThrow()
        val scope = SyncScope.SelectedNotes(setOf(UUID.fromString(shared.id)))
        pass(personal, personalRemote); pass(family, familyRemote, scope)

        clock.advanceSeconds(60)
        service.delete(shared.id).getOrThrow()
        pass(personal, personalRemote); pass(family, familyRemote, scope)

        assertTrue(personalRemote.store.getValue(UUID.fromString(shared.id)).isTombstone)
        assertTrue(familyRemote.store.getValue(UUID.fromString(shared.id)).isTombstone)
    }

    @Test fun `desconectar un perfil deja intactos el otro y las notas`() {
        service.create("hola").getOrThrow()
        pass(personal, personalRemote); pass(family, familyRemote)

        family.forgetSyncState()

        assertTrue(family.bases.isEmpty())
        assertEquals(1, personal.bases.size)
        assertEquals(1, notes.all().size)
    }

    // --- primera conexión de un perfil adicional ---

    @Test fun `un perfil adicional nunca pregunta ni descarta aunque haya notas`() {
        service.create("personal").getOrThrow()
        val decision = firstConnectDecision(family.snapshot(), isFirstProfile = false)

        assertEquals(FirstConnectDecision.Proceed(dropLocalTombstones = false), decision)
        completeFirstConnect(family, decision)
        assertEquals(1, notes.all().size)
    }

    @Test fun `un perfil adicional no descarta lapidas aunque no haya notas`() {
        val gone = service.create("x").getOrThrow()
        service.delete(gone.id).getOrThrow()

        val decision = firstConnectDecision(family.snapshot(), isFirstProfile = false)

        assertEquals(FirstConnectDecision.Proceed(dropLocalTombstones = false), decision)
        completeFirstConnect(family, decision)
        assertEquals(1, notes.tombstones().size)
    }

    @Test fun `el primer perfil sigue preguntando como siempre`() {
        service.create("personal").getOrThrow()
        assertEquals(FirstConnectDecision.AskUser(1), firstConnectDecision(personal.snapshot()))
        assertEquals(FirstConnectDecision.AskUser(1), firstConnectDecision(personal.snapshot(), isFirstProfile = true))
    }

    // --- combineOutcomes ---

    @Test fun `un solo resultado se queda igual`() {
        val one = SyncOutcome.Success(uploaded = 2, received = 1, conflicts = 3)
        assertEquals(one, combineOutcomes(listOf(one)))
    }

    @Test fun `varios exitos suman`() {
        val sum = combineOutcomes(listOf(SyncOutcome.Success(uploaded = 1, received = 2), SyncOutcome.Success(uploaded = 3, trashed = 1, skippedConcurrent = 1)))
        assertEquals(SyncOutcome.Success(uploaded = 4, received = 2, trashed = 1, skippedConcurrent = 1), sum)
    }

    @Test fun `sin perfiles es un exito vacio`() {
        assertEquals(SyncOutcome.Success(), combineOutcomes(emptyList()))
    }

    @Test fun `un fallo gana a los exitos y es el primero`() {
        val first = SyncFailure(SyncFailureKind.OFFLINE)
        val second = SyncFailure(SyncFailureKind.UNAUTHORIZED)
        val combined = combineOutcomes(listOf(SyncOutcome.Success(uploaded = 1), SyncOutcome.Failure(first), SyncOutcome.Failure(second)))
        assertEquals(SyncOutcome.Failure(first), combined)
    }

    // --- aggregateStatus ---

    private val t1 = Instant.parse("2026-03-01T10:00:00Z")
    private val t2 = Instant.parse("2026-03-01T11:00:00Z")

    @Test fun `sin perfiles el estado esta vacio`() {
        assertEquals(SyncStatus(), aggregateStatus(emptyList()))
    }

    @Test fun `la ultima sync buena es la del perfil mas atrasado`() {
        val status = aggregateStatus(listOf(SyncStatus(lastSuccessAt = t2, lastAttemptAt = t2), SyncStatus(lastSuccessAt = t1, lastAttemptAt = t1)))
        assertEquals(t1, status.lastSuccessAt)
        assertEquals(t2, status.lastAttemptAt)
    }

    @Test fun `si un perfil nunca sincronizo el conjunto tampoco`() {
        assertNull(aggregateStatus(listOf(SyncStatus(lastSuccessAt = t2), SyncStatus())).lastSuccessAt)
    }

    @Test fun `ejecutando y fallo se propagan`() {
        val failure = SyncFailure(SyncFailureKind.SERVER_ERROR)
        val status = aggregateStatus(listOf(SyncStatus(lastSuccessAt = t1), SyncStatus(lastSuccessAt = t1, lastFailure = failure, running = true)))
        assertTrue(status.running)
        assertEquals(failure, status.lastFailure)
    }
}
```

> Nota para quien ejecute: si `InMemorySyncTransport` no expone `isTombstone`/`store` con esos nombres, ajusta el acceso a lo que ya usa `EndToEndSyncTest` (`remote.store[...]`, `SyncEnvelope.deviceId`…); lee ese fichero antes. Lo importante es la comprobación, no el nombre.

- [ ] **Paso 2: ejecutar y ver que fallan**

Run (desde `android/`): `./gradlew :syncclient:test --tests "app.aldune.syncclient.MultiProfileTest"`
Esperado: no compila (`vaultFileName`, `combineOutcomes`, `aggregateStatus` y el parámetro `isFirstProfile` no existen).

- [ ] **Paso 3: implementar**

Crear `MultiProfile.kt`:

```kotlin
package app.aldune.syncclient

/**
 * Id del perfil que ya existía cuando Android solo admitía uno (y de las filas de `sync_base`/`sync_conflict` de entonces). Es fijo para que la
 * migración de Room (SQL puro) y el fichero de secretos de siempre no tengan que moverse.
 */
const val LEGACY_PROFILE_ID = "default"

private val SAFE_PROFILE_ID = Regex("[A-Za-z0-9-]+")

/** El fichero de secretos de un perfil. El id entra en un nombre de fichero, así que solo admite letras, cifras y guiones. */
fun vaultFileName(profileId: String): String {
    require(SAFE_PROFILE_ID.matches(profileId)) { "Id de perfil no válido: $profileId" }
    return if (profileId == LEGACY_PROFILE_ID) "syncsecrets.bin" else "syncsecrets-$profileId.bin"
}

/**
 * El resultado de una pasada que recorre varios perfiles. Un fallo gana (el primero): la política espera tras él para las pasadas
 * automáticas, y el perfil que sí sincronizó solo repite trabajo sin daño. Sin fallos, los contadores se suman.
 */
fun combineOutcomes(outcomes: List<SyncOutcome>): SyncOutcome {
    outcomes.filterIsInstance<SyncOutcome.Failure>().firstOrNull()?.let { return it }
    val ok = outcomes.filterIsInstance<SyncOutcome.Success>()
    return SyncOutcome.Success(
        uploaded = ok.sumOf { it.uploaded },
        received = ok.sumOf { it.received },
        tombstonesApplied = ok.sumOf { it.tombstonesApplied },
        trashed = ok.sumOf { it.trashed },
        conflicts = ok.sumOf { it.conflicts },
        omitted = ok.sumOf { it.omitted },
        skippedConcurrent = ok.sumOf { it.skippedConcurrent },
    )
}

/**
 * Un solo estado para la cabecera de la lista: «ejecutando» si alguno lo está, el último intento más reciente, el primer fallo y, como última
 * sync buena, la del perfil más atrasado (si alguno nunca sincronizó, ninguna: no se puede decir que todo esté al día).
 */
fun aggregateStatus(statuses: Collection<SyncStatus>): SyncStatus {
    if (statuses.isEmpty()) return SyncStatus()
    val successes = statuses.map { it.lastSuccessAt }
    return SyncStatus(
        lastSuccessAt = if (successes.any { it == null }) null else successes.filterNotNull().min(),
        lastAttemptAt = statuses.mapNotNull { it.lastAttemptAt }.maxOrNull(),
        lastFailure = statuses.firstNotNullOfOrNull { it.lastFailure },
        running = statuses.any { it.running },
    )
}
```

En `ProfileInput.kt`, sustituir `firstConnectDecision`:

```kotlin
/**
 * Pregunta si hay **alguna nota** local, activa, archivada o en la papelera. Las bases y los conflictos no cuentan: son de un perfil anterior.
 *
 * Un perfil **adicional** ([isFirstProfile] falso) nunca pregunta ni descarta: «descartar» borraría las notas de los demás perfiles. Solo
 * se sube lo que su alcance incluye y se baja lo que haya en su almacén. Tampoco descarta lápidas: son borrados que otros perfiles aún
 * pueden tener que firmar.
 */
fun firstConnectDecision(snapshot: LocalSnapshot, isFirstProfile: Boolean = true): FirstConnectDecision = when {
    !isFirstProfile -> FirstConnectDecision.Proceed(dropLocalTombstones = false)
    snapshot.notes.isNotEmpty() -> FirstConnectDecision.AskUser(snapshot.notes.size)
    else -> FirstConnectDecision.Proceed(dropLocalTombstones = snapshot.tombstones.isNotEmpty())
}
```

(`completeFirstConnect` no cambia: con `Proceed(false)` ya llama a `forgetSyncState`, que en un almacén por perfil solo limpia las bases de ese perfil.)

- [ ] **Paso 4: ejecutar y ver que pasan**

Run: `./gradlew :syncclient:test` — Esperado: toda la batería en verde (incluidos los tests anteriores de `ProfileInputTest`, que usan el valor por defecto `isFirstProfile = true`).

- [ ] **Paso 5: commit**

```bash
git add android/syncclient/src/main/kotlin/app/aldune/syncclient/MultiProfile.kt android/syncclient/src/main/kotlin/app/aldune/syncclient/ProfileInput.kt android/syncclient/src/test/kotlin/app/aldune/syncclient/MultiProfileTest.kt
git commit -m "Android: logica pura de varios perfiles de sync (resultados, estado, primera conexion)"
```

---

## Tarea 2: base de datos — bases y conflictos por perfil

**Ficheros:**
- Modificar: `android/app/src/main/kotlin/app/aldune/data/Entities.kt`, `SyncDao.kt`, `Migrations.kt`, `AldunaDatabase.kt`, `RoomLocalSyncStore.kt`, `RoomConflictStore.kt`
- Generado por el build: `android/app/schemas/app.aldune.data.AldunaDatabase/3.json` (se versiona)
- Modificar tests instrumentados: `android/app/src/androidTest/kotlin/app/aldune/data/MigrationTest.kt`, `RoomLocalSyncStoreTest.kt`
- Crear test: `android/app/src/androidTest/kotlin/app/aldune/data/RoomMultiProfileTest.kt`

**Interfaces — produce:**
- `SyncBaseEntity(profileId, noteId, updatedAt, deviceId)` y `SyncConflictEntity(profileId, noteId, detectedAt, payloadCipher, nonce, tag)`, clave `(profileId, noteId)`.
- `RoomLocalSyncStore(database, notes, cipher, profileId: String)`; `discardAllLocal()` sigue borrando **todas** las bases y conflictos (solo lo usa el primer perfil); `forgetSyncState()` borra solo los de su perfil.
- `RoomConflictStore` mantiene su interfaz (`all/get/delete` por nota): ante la misma nota en dos perfiles devuelve el conflicto **más reciente** y `delete` borra solo ese (decisión de la sección 7 de la spec).
- `MIGRATION_2_3`.

- [ ] **Paso 1: tests instrumentados que fallan**

En `MigrationTest.kt` añadir:

```kotlin
    @Test fun migration2To3MovesRowsToTheLegacyProfile() {
        helper.createDatabase("migration-23", 2).use { db ->
            db.execSQL("INSERT INTO sync_base (noteId, updatedAt, deviceId) VALUES ('x', '2026-10-17T10:00:00Z', 'pc')")
            db.execSQL("INSERT INTO sync_conflict (noteId, detectedAt, payloadCipher, nonce, tag) VALUES ('x', 't', x'01', x'02', x'03')")
        }
        helper.runMigrationsAndValidate("migration-23", 3, true, MIGRATION_2_3).use { db ->
            db.query("SELECT profileId, deviceId FROM sync_base WHERE noteId = 'x'").use { c ->
                assertTrue(c.moveToFirst())
                assertEquals("default", c.getString(0))
                assertEquals("pc", c.getString(1))
            }
            db.query("SELECT profileId, detectedAt FROM sync_conflict WHERE noteId = 'x'").use { c ->
                assertTrue(c.moveToFirst())
                assertEquals("default", c.getString(0))
                assertEquals("t", c.getString(1))
            }
            // La misma nota ya puede tener base en otro perfil.
            db.execSQL("INSERT INTO sync_base (profileId, noteId, updatedAt, deviceId) VALUES ('otro', 'x', 't', 'pc')")
        }
    }
```

Y cambiar `runMigrationsAndValidate("migration-12", 2, true, MIGRATION_1_2)` para que se siga validando contra la v2 (no cambia). En `RoomLocalSyncStoreTest.kt`, línea `val syncStore = RoomLocalSyncStore(database, noteStore, cipher)` → `RoomLocalSyncStore(database, noteStore, cipher, "default")`.

Crear `RoomMultiProfileTest.kt`:

```kotlin
package app.aldune.data

import android.content.Context
import androidx.room.Room
import androidx.test.core.app.ApplicationProvider
import androidx.test.ext.junit.runners.AndroidJUnit4
import app.aldune.domain.AesGcmNoteCipher
import app.aldune.domain.Note
import app.aldune.sync.SyncBase
import app.aldune.sync.SyncConflict
import app.aldune.syncclient.LocalCommit
import java.time.OffsetDateTime
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import org.junit.runner.RunWith

/** Dos perfiles sobre la misma base (EN MEMORIA, nunca la real): bases y conflictos aparte, notas compartidas. */
@RunWith(AndroidJUnit4::class)
class RoomMultiProfileTest {
    private lateinit var db: AldunaDatabase
    private lateinit var a: RoomLocalSyncStore
    private lateinit var b: RoomLocalSyncStore
    private lateinit var conflicts: RoomConflictStore

    @Before fun setUp() {
        val context = ApplicationProvider.getApplicationContext<Context>()
        db = Room.inMemoryDatabaseBuilder(context, AldunaDatabase::class.java).build()
        val cipher = AesGcmNoteCipher(ByteArray(32) { it.toByte() })
        val notes = RoomNoteStore(db, cipher)
        a = RoomLocalSyncStore(db, notes, cipher, "a")
        b = RoomLocalSyncStore(db, notes, cipher, "b")
        conflicts = RoomConflictStore(db, cipher)
    }

    @After fun tearDown() = db.close()

    private val base = SyncBase(OffsetDateTime.parse("2026-10-26T10:00:00Z"), "pc01")

    @Test fun basesDeCadaPerfilNoSeMezclan() {
        a.commit(LocalCommit(emptyList(), mapOf(ID to base), emptyMap()))
        assertEquals(setOf(ID), a.snapshot().bases.keys)
        assertTrue(b.snapshot().bases.isEmpty())
    }

    @Test fun olvidarElEstadoDeUnPerfilNoTocaElOtro() {
        a.commit(LocalCommit(emptyList(), mapOf(ID to base), emptyMap()))
        b.commit(LocalCommit(emptyList(), mapOf(ID to base), emptyMap()))
        a.forgetSyncState()
        assertTrue(a.snapshot().bases.isEmpty())
        assertEquals(setOf(ID), b.snapshot().bases.keys)
    }

    @Test fun laMismaNotaConConflictoEnDosPerfilesSeResuelveDeUnoEnUno() {
        // Sin pérdida silenciosa: resolver el que se ve deja el del otro perfil, que aparece a continuación.
        a.commit(LocalCommit(listOf(app.aldune.syncclient.LocalAction.SaveConflict(conflict("antiguo", "2026-10-26T10:00:00Z"))), emptyMap(), mapOf(ID to app.aldune.syncclient.ExpectedVersion.Absent)))
        b.commit(LocalCommit(listOf(app.aldune.syncclient.LocalAction.SaveConflict(conflict("reciente", "2026-10-26T11:00:00Z"))), emptyMap(), mapOf(ID to app.aldune.syncclient.ExpectedVersion.Absent)))

        assertEquals(setOf(ID), a.conflictedNoteIds() + b.conflictedNoteIds())
        assertEquals(1, conflicts.all().size)
        assertEquals("reciente", conflicts.get(ID)!!.losing.note!!.text)

        conflicts.delete(ID)
        assertEquals("antiguo", conflicts.get(ID)!!.losing.note!!.text)

        conflicts.delete(ID)
        assertNull(conflicts.get(ID))
    }

    // Construye un conflicto mínimo; ver SyncConflict/ConflictPayload para los campos reales y ajustar si cambian.
    private fun conflict(text: String, detectedAt: String): SyncConflict = TODO("usar el mismo constructor que LocalSyncStoreContract para SaveConflict")

    private companion object {
        const val ID = "11111111-1111-1111-1111-111111111111"
    }
}
```

> **Para quien ejecute:** `conflict(...)` y `Note` se construyen igual que en `LocalSyncStoreContract.kt` (testFixtures de syncclient), que ya crea `SyncConflict`. Copia de ahí el constructor y rellena la función (el `TODO` es solo porque el tipo `SyncConflict` tiene muchos campos que hay que leer en ese fichero; no se deja en el código final). Quita los imports no usados (`Note`).

- [ ] **Paso 2: compilar para ver el fallo**

Run: `./gradlew :app:compileDebugAndroidTestKotlin` — Esperado: no compila (`MIGRATION_2_3`, 4º parámetro de `RoomLocalSyncStore`).

- [ ] **Paso 3: implementar**

`Entities.kt` — sustituir las dos entidades:

```kotlin
/**
 * Última versión que se sabe igual en los dos lados, para distinguir conflicto real de cambio de un solo lado (F5). Una por (perfil, nota):
 * la misma nota puede estar en el alcance de dos perfiles y cada almacén remoto tiene su propia versión acordada.
 */
@Entity(tableName = "sync_base", primaryKeys = ["profileId", "noteId"])
class SyncBaseEntity(
    val profileId: String,
    val noteId: String,
    val updatedAt: String,
    val deviceId: String,
)

/**
 * Conflicto guardado por la sync (F5): la versión que perdió, para poder recuperarla (F7). Las dos versiones van en un JSON cifrado con el
 * mismo cifrador que el texto de las notas (contiene texto de notas), en tres columnas como `note`. Uno por (perfil, nota).
 */
@Entity(tableName = "sync_conflict", primaryKeys = ["profileId", "noteId"])
class SyncConflictEntity(
    val profileId: String,
    val noteId: String,
    val detectedAt: String,
    @ColumnInfo(typeAffinity = ColumnInfo.BLOB) val payloadCipher: ByteArray,
    @ColumnInfo(typeAffinity = ColumnInfo.BLOB) val nonce: ByteArray,
    @ColumnInfo(typeAffinity = ColumnInfo.BLOB) val tag: ByteArray,
)
```

`SyncDao.kt` — reemplazar el cuerpo:

```kotlin
/** Bases y conflictos de la sync, por perfil. Las notas y las lápidas están en [NoteDao]: son la misma base, y `RoomLocalSyncStore` las toca en una transacción. */
@Dao
interface SyncDao {
    @Query("SELECT * FROM sync_base WHERE profileId = :profileId")
    fun bases(profileId: String): List<SyncBaseEntity>

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    fun putBase(base: SyncBaseEntity)

    @Query("DELETE FROM sync_base WHERE profileId = :profileId")
    fun clearBases(profileId: String): Int

    @Query("DELETE FROM sync_base")
    fun clearAllBases(): Int

    @Query("SELECT * FROM sync_conflict")
    fun conflicts(): List<SyncConflictEntity>

    /** El conflicto más reciente de una nota entre todos los perfiles (el que se enseña). */
    @Query("SELECT * FROM sync_conflict WHERE noteId = :id ORDER BY detectedAt DESC, profileId LIMIT 1")
    fun latestConflict(id: String): SyncConflictEntity?

    @Query("DELETE FROM sync_conflict WHERE profileId = :profileId AND noteId = :id")
    fun deleteConflict(profileId: String, id: String)

    @Query("SELECT DISTINCT noteId FROM sync_conflict")
    fun conflictedIds(): List<String>

    @Query("SELECT noteId FROM sync_conflict WHERE profileId = :profileId")
    fun conflictedIds(profileId: String): List<String>

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    fun putConflict(conflict: SyncConflictEntity)

    @Query("DELETE FROM sync_conflict WHERE profileId = :profileId")
    fun clearConflicts(profileId: String): Int

    @Query("DELETE FROM sync_conflict")
    fun clearAllConflicts(): Int
}
```

`Migrations.kt` — añadir:

```kotlin
/**
 * v2 → v3 (varios perfiles): `sync_base` y `sync_conflict` pasan a clave (profileId, noteId). Las filas que había son del único perfil de
 * entonces, que sigue existiendo con el id fijo `default` (ver `LEGACY_PROFILE_ID` en syncclient). SQLite no cambia una clave primaria: se
 * recrea la tabla y se copian las filas.
 */
val MIGRATION_2_3 = object : Migration(2, 3) {
    override fun migrate(db: SupportSQLiteDatabase) {
        db.execSQL("CREATE TABLE `sync_base_new` (`profileId` TEXT NOT NULL, `noteId` TEXT NOT NULL, `updatedAt` TEXT NOT NULL, `deviceId` TEXT NOT NULL, PRIMARY KEY(`profileId`, `noteId`))")
        db.execSQL("INSERT INTO `sync_base_new` (`profileId`, `noteId`, `updatedAt`, `deviceId`) SELECT 'default', `noteId`, `updatedAt`, `deviceId` FROM `sync_base`")
        db.execSQL("DROP TABLE `sync_base`")
        db.execSQL("ALTER TABLE `sync_base_new` RENAME TO `sync_base`")

        db.execSQL(
            "CREATE TABLE `sync_conflict_new` (`profileId` TEXT NOT NULL, `noteId` TEXT NOT NULL, `detectedAt` TEXT NOT NULL, " +
                "`payloadCipher` BLOB NOT NULL, `nonce` BLOB NOT NULL, `tag` BLOB NOT NULL, PRIMARY KEY(`profileId`, `noteId`))",
        )
        db.execSQL(
            "INSERT INTO `sync_conflict_new` (`profileId`, `noteId`, `detectedAt`, `payloadCipher`, `nonce`, `tag`) " +
                "SELECT 'default', `noteId`, `detectedAt`, `payloadCipher`, `nonce`, `tag` FROM `sync_conflict`",
        )
        db.execSQL("DROP TABLE `sync_conflict`")
        db.execSQL("ALTER TABLE `sync_conflict_new` RENAME TO `sync_conflict`")
    }
}
```

`AldunaDatabase.kt`: `version = 3`; `.addMigrations(MIGRATION_1_2, MIGRATION_2_3)`; actualizar el comentario del KDoc («v3: bases y conflictos por perfil»).

`RoomLocalSyncStore.kt` — cambios (resto igual):

```kotlin
class RoomLocalSyncStore(
    private val database: AldunaDatabase,
    private val notes: RoomNoteStore,
    private val cipher: NoteCipher,
    /** El perfil de esta vista: bases y conflictos son suyos. Notas y lápidas son de todos. */
    private val profileId: String,
) : LocalSyncStore {
```
- `snapshot()`: `syncDao.bases(profileId).associate { ... }`.
- `commit`: `syncDao.putConflict(toEntity(action.conflict))` ya usa `toEntity` → ahí `SyncConflictEntity(profileId, id, ...)`; `syncDao.putBase(SyncBaseEntity(profileId, id, base.updatedAt.toString(), base.deviceId))`.
- `conflictedNoteIds()` = `syncDao.conflictedIds(profileId).toSet()`.
- `conflicts()` (hoy sin usos fuera; si no los tiene, borrarla).
- `discardAllLocal()`: `noteDao.clearNotes(); noteDao.clearTombstones(); syncDao.clearAllBases(); syncDao.clearAllConflicts()` (solo el primer perfil lo llama, ver Tarea 1).
- `forgetSyncState()`: `syncDao.clearBases(profileId); syncDao.clearConflicts(profileId)`.
- `toConflict(row)`: sin cambios (usa `row.noteId`).

`RoomConflictStore.kt` — reemplazar el cuerpo:

```kotlin
class RoomConflictStore(database: AldunaDatabase, private val cipher: NoteCipher) : ConflictStore {
    private val dao = database.sync()

    // «Uno por nota» (contrato de ConflictStore): si dos perfiles tienen conflicto de la misma nota se enseña el más reciente. Resolverlo
    // lo borra a él solo, y el del otro perfil aparece después: así no se pierde en silencio una versión perdedora distinta.
    override fun all(): List<SyncConflict> =
        dao.conflicts().groupBy { it.noteId }.values.map { rows -> rows.maxWith(compareBy<SyncConflictEntity> { it.detectedAt }.thenByDescending { it.profileId }) }.map(::toConflict)

    override fun get(noteId: String): SyncConflict? = dao.latestConflict(noteId)?.let(::toConflict)

    override fun delete(noteId: String) {
        dao.latestConflict(noteId)?.let { dao.deleteConflict(it.profileId, it.noteId) }
    }
    ...
```
(`toConflict` igual.) Ojo: la comparación `maxWith` debe dar el mismo ganador que `ORDER BY detectedAt DESC, profileId` de `latestConflict`: mayor `detectedAt`, y a igualdad el `profileId` menor (`thenByDescending` en un `maxWith` elige el menor id). Mantén ambos coherentes.

- [ ] **Paso 4: compilar y generar el esquema**

Run: `./gradlew :app:assembleDebug` — Esperado: éxito y fichero nuevo `app/schemas/.../3.json`. Revisa que `sync_base` y `sync_conflict` tienen `primaryKey.columnNames = ["profileId","noteId"]`.

- [ ] **Paso 5: tests instrumentados (Pixel conectado, solo debug)**

Run: `./gradlew :app:connectedDebugAndroidTest` — Esperado: `MigrationTest`, `RoomLocalSyncStoreTest` (contrato) y `RoomMultiProfileTest` en verde. Si el dispositivo no está disponible, dilo en el informe: es una verificación sin hacer, no un éxito.

- [ ] **Paso 6: commit**

```bash
git add android/app/src/main/kotlin/app/aldune/data/Entities.kt android/app/src/main/kotlin/app/aldune/data/SyncDao.kt android/app/src/main/kotlin/app/aldune/data/Migrations.kt android/app/src/main/kotlin/app/aldune/data/AldunaDatabase.kt android/app/src/main/kotlin/app/aldune/data/RoomLocalSyncStore.kt android/app/src/main/kotlin/app/aldune/data/RoomConflictStore.kt android/app/schemas/app.aldune.data.AldunaDatabase/3.json android/app/src/androidTest/kotlin/app/aldune/data/MigrationTest.kt android/app/src/androidTest/kotlin/app/aldune/data/RoomLocalSyncStoreTest.kt android/app/src/androidTest/kotlin/app/aldune/data/RoomMultiProfileTest.kt
git commit -m "Android: bases y conflictos de sync por perfil (esquema v3 con migracion)"
```

---

## Tarea 3: almacén de perfiles (varios) con migración del perfil único

**Ficheros:**
- Modificar: `android/app/src/main/kotlin/app/aldune/data/SyncProfileStore.kt`
- Crear test: `android/app/src/androidTest/kotlin/app/aldune/data/SyncProfileStoreTest.kt`

**Interfaces — produce (lo usa la Tarea 4):**
```kotlin
data class SyncProfile(val id: String, val transport: SyncTransportKind, val url: String, val scope: SyncScope, val profileName: String?, val deviceId: String)
class SyncProfileStore(context: Context) {
    fun deviceId(): String
    fun loadAll(): List<SyncProfile>          // migra el perfil único la primera vez
    fun save(profile: SyncProfile)            // alta o actualización por id
    fun remove(profileId: String)             // perfil y su estado
    fun loadStatus(profileId: String): SyncStatus
    fun saveStatus(profileId: String, status: SyncStatus)
}
```

- [ ] **Paso 1: test instrumentado que falla** (usa un contexto aislado: `getSharedPreferences("sync")` real sería el de la app debug, que está vacía; borrar al empezar y terminar)

```kotlin
package app.aldune.data

import android.content.Context
import androidx.test.core.app.ApplicationProvider
import androidx.test.ext.junit.runners.AndroidJUnit4
import app.aldune.sync.SyncScope
import app.aldune.syncclient.SyncFailure
import app.aldune.syncclient.SyncFailureKind
import app.aldune.syncclient.SyncStatus
import java.time.Instant
import java.util.UUID
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import org.junit.runner.RunWith

/** Solo toca el fichero `sync` de la app **debug** (datos de prueba). */
@RunWith(AndroidJUnit4::class)
class SyncProfileStoreTest {
    private val context: Context = ApplicationProvider.getApplicationContext()
    private val prefs get() = context.getSharedPreferences("sync", Context.MODE_PRIVATE)

    @Before @After fun clean() {
        prefs.edit().clear().commit()
    }

    private fun profile(id: String, name: String, scope: SyncScope = SyncScope.All) =
        SyncProfile(id, SyncTransportKind.SERVER, "https://$id.example.com/", scope, name, "0123456789abcdef0123456789abcdef")

    @Test fun sinNadaDevuelveListaVacia() {
        assertTrue(SyncProfileStore(context).loadAll().isEmpty())
    }

    @Test fun guardaVariosYLosRecuperaEnOrden() {
        val store = SyncProfileStore(context)
        val noteId = UUID.fromString("11111111-1111-1111-1111-111111111111")
        store.save(profile("a", "Casa"))
        store.save(profile("b", "Familia", SyncScope.SelectedNotes(setOf(noteId))))
        val loaded = SyncProfileStore(context).loadAll()
        assertEquals(listOf("a", "b"), loaded.map { it.id })
        assertEquals(SyncScope.SelectedNotes(setOf(noteId)), loaded[1].scope)
    }

    @Test fun guardarDeNuevoActualizaSinDuplicar() {
        val store = SyncProfileStore(context)
        store.save(profile("a", "Casa"))
        store.save(profile("a", "Casa 2"))
        assertEquals(listOf("Casa 2"), store.loadAll().map { it.profileName })
    }

    @Test fun quitarUnoNoTocaElOtroNiSuEstado() {
        val store = SyncProfileStore(context)
        store.save(profile("a", "Casa")); store.save(profile("b", "Familia"))
        store.saveStatus("a", SyncStatus(lastSuccessAt = Instant.ofEpochMilli(1000)))
        store.saveStatus("b", SyncStatus(lastSuccessAt = Instant.ofEpochMilli(2000), lastFailure = SyncFailure(SyncFailureKind.OFFLINE, "x")))
        store.remove("a")
        assertEquals(listOf("b"), store.loadAll().map { it.id })
        assertEquals(SyncStatus(), store.loadStatus("a"))
        assertEquals(Instant.ofEpochMilli(2000), store.loadStatus("b").lastSuccessAt)
        assertEquals(SyncFailureKind.OFFLINE, store.loadStatus("b").lastFailure!!.kind)
    }

    @Test fun elDeviceIdSobreviveAQuitarTodo() {
        val store = SyncProfileStore(context)
        val id = store.deviceId()
        store.save(profile("a", "Casa")); store.remove("a")
        assertEquals(id, SyncProfileStore(context).deviceId())
    }

    @Test fun migraElPerfilUnicoDeLasVersionesAnteriores() {
        // Así lo dejaba la versión de un solo perfil.
        prefs.edit().putString("transport", "WEBDAV").putString("url", "https://cloud.example.com/dav/").putString("profileName", "Mi nube")
            .putInt("scope", 2).putString("tag", "viaje").putLong("status.ok", 5000L).putLong("status.attempt", 6000L)
            .putString("deviceId", "0123456789abcdef0123456789abcdef").commit()

        val store = SyncProfileStore(context)
        val migrated = store.loadAll().single()

        assertEquals("default", migrated.id)
        assertEquals(SyncTransportKind.WEBDAV, migrated.transport)
        assertEquals(SyncScope.Tag("viaje"), migrated.scope)
        assertEquals("Mi nube", migrated.profileName)
        assertEquals(Instant.ofEpochMilli(5000), store.loadStatus("default").lastSuccessAt)
        assertTrue("las claves antiguas se retiran", !prefs.contains("url") && !prefs.contains("status.ok"))
        // Idempotente: otra carga no cambia nada.
        assertEquals(listOf("default"), SyncProfileStore(context).loadAll().map { it.id })
    }
}
```

- [ ] **Paso 2: ver que no compila** — `./gradlew :app:compileDebugAndroidTestKotlin` (falta `loadAll`, `id`…).

- [ ] **Paso 3: implementar `SyncProfileStore.kt`** (reemplazo completo)

```kotlin
package app.aldune.data

import android.content.Context
import app.aldune.core.ProfileCodes
import app.aldune.sync.SyncScope
import app.aldune.syncclient.DeviceIds
import app.aldune.syncclient.LEGACY_PROFILE_ID
import app.aldune.syncclient.SyncFailure
import app.aldune.syncclient.SyncFailureKind
import app.aldune.syncclient.SyncStatus
import java.time.Instant
import java.util.UUID
import org.json.JSONArray
import org.json.JSONObject

enum class SyncTransportKind { SERVER, WEBDAV }

/** Un perfil de sync **sin** secretos (esos van en un `SecretVault` por perfil). [id] es un UUID; el perfil heredado es [LEGACY_PROFILE_ID]. */
data class SyncProfile(
    val id: String,
    val transport: SyncTransportKind,
    val url: String,
    val scope: SyncScope,
    val profileName: String?,
    val deviceId: String,
)

/**
 * Lo no secreto de la sync: los perfiles conectados (JSON en SharedPreferences, como los ajustes), el `deviceId` de esta instalación y el
 * último estado de cada perfil. El `deviceId` se genera una vez y **sobrevive a desconectar**: las versiones ya subidas están firmadas con él,
 * y cambiarlo haría que esas notas parecieran de otro equipo.
 *
 * Las versiones de un solo perfil guardaban las claves sueltas (`url`, `transport`…): la primera lectura las convierte en el perfil
 * [LEGACY_PROFILE_ID] y las retira (ver [migrateLegacy]).
 */
class SyncProfileStore(context: Context) {
    private val prefs = context.applicationContext.getSharedPreferences(FILE, Context.MODE_PRIVATE)

    fun deviceId(): String {
        val existing = prefs.getString(DEVICE_ID, null)
        if (existing != null && DeviceIds.isValid(existing)) return existing
        val fresh = DeviceIds.generate()
        prefs.edit().putString(DEVICE_ID, fresh).commit()
        return fresh
    }

    @Synchronized
    fun loadAll(): List<SyncProfile> {
        migrateLegacy()
        val raw = prefs.getString(PROFILES, null) ?: return emptyList()
        val device = deviceId()
        val array = JSONArray(raw)
        return (0 until array.length()).mapNotNull { fromJson(array.getJSONObject(it), device) }
    }

    /** Alta o actualización por id; un perfil nuevo va al final. */
    @Synchronized
    fun save(profile: SyncProfile) {
        val all = loadAll().toMutableList()
        val index = all.indexOfFirst { it.id == profile.id }
        if (index >= 0) all[index] = profile else all += profile
        writeAll(all)
    }

    /** Quita el perfil y su estado. No toca el `deviceId`. */
    @Synchronized
    fun remove(profileId: String) {
        writeAll(loadAll().filter { it.id != profileId })
        prefs.edit().apply { for (suffix in STATUS_SUFFIXES) remove(statusKey(profileId, suffix)) }.commit()
    }

    fun loadStatus(profileId: String): SyncStatus {
        val kind = prefs.getString(statusKey(profileId, "kind"), null)?.let { name -> SyncFailureKind.entries.firstOrNull { it.name == name } }
        return SyncStatus(
            lastSuccessAt = prefs.getLong(statusKey(profileId, "ok"), 0L).takeIf { it > 0 }?.let(Instant::ofEpochMilli),
            lastAttemptAt = prefs.getLong(statusKey(profileId, "attempt"), 0L).takeIf { it > 0 }?.let(Instant::ofEpochMilli),
            lastFailure = kind?.let {
                SyncFailure(it, prefs.getString(statusKey(profileId, "detail"), null), prefs.getInt(statusKey(profileId, "http"), -1).takeIf { code -> code >= 0 })
            },
        )
    }

    /** Solo se guarda lo que sobrevive a cerrar la app; `running` no. */
    fun saveStatus(profileId: String, status: SyncStatus) {
        val editor = prefs.edit()
            .putLong(statusKey(profileId, "ok"), status.lastSuccessAt?.toEpochMilli() ?: 0L)
            .putLong(statusKey(profileId, "attempt"), status.lastAttemptAt?.toEpochMilli() ?: 0L)
        val failure = status.lastFailure
        if (failure == null) {
            editor.remove(statusKey(profileId, "kind")).remove(statusKey(profileId, "detail")).remove(statusKey(profileId, "http"))
        } else {
            editor.putString(statusKey(profileId, "kind"), failure.kind.name).putString(statusKey(profileId, "detail"), failure.detail)
                .putInt(statusKey(profileId, "http"), failure.httpStatus ?: -1)
        }
        editor.apply()
    }

    private fun writeAll(profiles: List<SyncProfile>) {
        val array = JSONArray()
        profiles.forEach { array.put(toJson(it)) }
        prefs.edit().putString(PROFILES, array.toString()).commit()
    }

    /** El perfil único de antes pasa a ser el perfil `default`, con su estado. Idempotente: sin claves antiguas no hace nada. */
    private fun migrateLegacy() {
        if (prefs.contains(PROFILES)) return
        val url = prefs.getString(URL, null) ?: return
        val transport = prefs.getString(TRANSPORT, null)?.let { name -> SyncTransportKind.entries.firstOrNull { it.name == name } }
        val editor = prefs.edit()
        if (transport != null) {
            val scope = when (prefs.getInt(SCOPE, ProfileCodes.SCOPE_ALL)) {
                ProfileCodes.SCOPE_CHOSEN -> SyncScope.SelectedNotes(
                    prefs.getString(NOTE_IDS, "").orEmpty().split(',').filter { it.isNotBlank() }.map(UUID::fromString).toSet(),
                )
                ProfileCodes.SCOPE_TAG -> SyncScope.Tag(prefs.getString(TAG, null))
                else -> SyncScope.All
            }
            val array = JSONArray().put(toJson(SyncProfile(LEGACY_PROFILE_ID, transport, url, scope, prefs.getString(NAME, null), "")))
            editor.putString(PROFILES, array.toString())
            for (suffix in STATUS_SUFFIXES) {
                val old = LEGACY_STATUS_PREFIX + suffix
                when (val value = prefs.all[old]) {
                    is Long -> editor.putLong(statusKey(LEGACY_PROFILE_ID, suffix), value)
                    is Int -> editor.putInt(statusKey(LEGACY_PROFILE_ID, suffix), value)
                    is String -> editor.putString(statusKey(LEGACY_PROFILE_ID, suffix), value)
                }
            }
        }
        editor.remove(TRANSPORT).remove(URL).remove(NAME).remove(SCOPE).remove(NOTE_IDS).remove(TAG)
        for (suffix in STATUS_SUFFIXES) editor.remove(LEGACY_STATUS_PREFIX + suffix)
        editor.commit()
    }

    private fun toJson(profile: SyncProfile): JSONObject = JSONObject().put("id", profile.id).put("transport", profile.transport.name)
        .put("url", profile.url).put("name", profile.profileName ?: JSONObject.NULL).apply {
            when (val scope = profile.scope) {
                SyncScope.All -> put("scope", ProfileCodes.SCOPE_ALL)
                is SyncScope.SelectedNotes -> put("scope", ProfileCodes.SCOPE_CHOSEN).put("noteIds", scope.noteIds.joinToString(","))
                is SyncScope.Tag -> put("scope", ProfileCodes.SCOPE_TAG).put("tag", scope.tag ?: JSONObject.NULL)
            }
        }

    private fun fromJson(o: JSONObject, deviceId: String): SyncProfile? {
        val transport = SyncTransportKind.entries.firstOrNull { it.name == o.optString("transport") } ?: return null
        val scope = when (o.optInt("scope", ProfileCodes.SCOPE_ALL)) {
            ProfileCodes.SCOPE_CHOSEN -> SyncScope.SelectedNotes(o.optString("noteIds", "").split(',').filter { it.isNotBlank() }.map(UUID::fromString).toSet())
            ProfileCodes.SCOPE_TAG -> SyncScope.Tag(if (o.isNull("tag")) null else o.optString("tag"))
            else -> SyncScope.All
        }
        return SyncProfile(o.getString("id"), transport, o.getString("url"), scope, if (o.isNull("name")) null else o.optString("name"), deviceId)
    }

    private fun statusKey(profileId: String, suffix: String) = "status.$profileId.$suffix"

    private companion object {
        const val FILE = "sync"
        const val DEVICE_ID = "deviceId"
        const val PROFILES = "profiles"
        const val TRANSPORT = "transport"
        const val URL = "url"
        const val NAME = "profileName"
        const val SCOPE = "scope"
        const val NOTE_IDS = "noteIds"
        const val TAG = "tag"
        const val LEGACY_STATUS_PREFIX = "status."
        val STATUS_SUFFIXES = listOf("ok", "attempt", "kind", "detail", "http")
    }
}
```

> Cuidado: un perfil con id `"ok"` o `"kind"` chocaría con las claves heredadas `status.ok`; los ids son UUID o `default`, así que no ocurre. `deviceId` del perfil heredado se rellena al leer (`fromJson` recibe el del dispositivo).

- [ ] **Paso 4: ejecutar** — `./gradlew :app:connectedDebugAndroidTest --tests "app.aldune.data.SyncProfileStoreTest"` (o el paquete completo). Esperado: verde.
- [ ] **Paso 5: commit**

```bash
git add android/app/src/main/kotlin/app/aldune/data/SyncProfileStore.kt android/app/src/androidTest/kotlin/app/aldune/data/SyncProfileStoreTest.kt
git commit -m "Android: almacen de varios perfiles de sync, con migracion del perfil unico"
```

---

## Tarea 4: `SyncController` con varios perfiles y cableado

**Ficheros:**
- Modificar: `android/app/src/main/kotlin/app/aldune/syncing/SyncController.kt`, `android/app/src/main/kotlin/app/aldune/AppContainer.kt`

**Interfaces — consume:** `SyncProfileStore` (Tarea 3), `vaultFileName`, `combineOutcomes`, `aggregateStatus`, `firstConnectDecision(…, isFirstProfile)` (Tarea 1), `RoomLocalSyncStore(…, profileId)` (Tarea 2).
**Produce (lo usa la Tarea 5):**
- `profiles: StateFlow<List<SyncProfile>>`, `statuses: StateFlow<Map<String, SyncStatus>>`, `status: StateFlow<SyncStatus>` (agregado), `unreadable: StateFlow<Set<String>>`, `conflicted: StateFlow<Set<String>>`, `bases: StateFlow<Map<String, Instant>>`
- `suspend fun connect(request: ConnectRequest, choice: FirstConnectChoice? = null): ConnectResult` con el nuevo caso `ConnectResult.AlreadyConnected(name: String)`
- `suspend fun disconnect(profileId: String)`
- `refreshSchedule()`, `request(event)`, `run(event)`, `refreshConflicts()`, `testConnection(...)` sin cambios de firma.

No hay JVM-test posible aquí (Android); la lógica con reglas ya está en la Tarea 1. Se verifica compilando y en el Pixel (Tarea 6).

- [ ] **Paso 1: cambiar la cabecera de la clase y el estado**

```kotlin
class SyncController(
    private val profileStore: SyncProfileStore,
    private val vaultFor: (String) -> SecretVault,
    /** La vista de sync de un perfil (bases y conflictos suyos, notas y lápidas de todos). */
    private val storeFor: (String) -> LocalSyncStore,
    private val clock: Clock,
    private val scope: CoroutineScope,
    private val schedule: SyncSchedule,
) {
    // ... onNotesChanged / afterNotesChanged / notesChanged() igual ...

    private val _profiles = MutableStateFlow(profileStore.loadAll())
    val profiles: StateFlow<List<SyncProfile>> = _profiles.asStateFlow()

    private val _statuses = MutableStateFlow(_profiles.value.associate { it.id to profileStore.loadStatus(it.id) })
    val statuses: StateFlow<Map<String, SyncStatus>> = _statuses.asStateFlow()

    /** El estado de toda la sync para la cabecera de la lista (ver `aggregateStatus`); el detalle por perfil está en [statuses]. */
    private val _status = MutableStateFlow(aggregateStatus(_statuses.value.values))
    val status: StateFlow<SyncStatus> = _status.asStateFlow()

    /** Perfiles cuyos secretos no se pueden abrir (el Keystore perdió la clave): hay que volver a pegar su código. */
    private val _unreadable = MutableStateFlow<Set<String>>(emptySet())
    val unreadable: StateFlow<Set<String>> = _unreadable.asStateFlow()

    private val _conflicted = MutableStateFlow<Set<String>>(emptySet())
    val conflicted: StateFlow<Set<String>> = _conflicted.asStateFlow()

    /** Por nota, la base más antigua entre los perfiles que la tienen: si algún perfil va por detrás, la fila sale como pendiente. */
    private val _bases = MutableStateFlow<Map<String, java.time.Instant>>(emptyMap())
    val bases: StateFlow<Map<String, java.time.Instant>> = _bases.asStateFlow()

    private val policy = SyncTriggerPolicy(clock, profileConfigured = _profiles.value.isNotEmpty())
```

`init`:

```kotlin
    init {
        scope.launch(Dispatchers.IO) {
            if (_profiles.value.isNotEmpty()) schedule.ensure()
            _unreadable.value = _profiles.value.filter { vaultFor(it.id).load() is VaultState.Unreadable }.map { it.id }.toSet()
            refreshConflicts()
        }
    }

    fun refreshSchedule() {
        scope.launch(Dispatchers.IO) { if (_profiles.value.isNotEmpty()) schedule.ensure() }
    }
```

- [ ] **Paso 2: la pasada recorre los perfiles**

Reemplazar `runPass()` y `setStatus`:

```kotlin
    private suspend fun runPass(): SyncOutcome {
        val list = _profiles.value
        if (list.isEmpty()) return SyncOutcome.Failure(SyncFailure(SyncFailureKind.UNKNOWN, "No sync profile"))
        var changed = false
        val outcomes = list.map { profile ->
            val outcome = runProfilePass(profile)
            if (outcome is SyncOutcome.Success && outcome.received + outcome.tombstonesApplied + outcome.trashed + outcome.conflicts > 0) changed = true
            outcome
        }
        refreshConflicts()
        if (changed) notesChanged()
        return combineOutcomes(outcomes)
    }

    /** Una pasada de un perfil. Un fallo suyo queda en su estado y no impide que los demás sincronicen. */
    private suspend fun runProfilePass(profile: SyncProfile): SyncOutcome {
        val secrets = (vaultFor(profile.id).load() as? VaultState.Ready)?.secrets
        if (secrets == null) {
            _unreadable.update { it + profile.id }
            return SyncOutcome.Failure(SyncFailure(SyncFailureKind.UNKNOWN, "The stored sync secrets can't be read"))
        }
        _unreadable.update { it - profile.id }
        val before = _statuses.value[profile.id] ?: SyncStatus()
        setStatus(profile.id, before.copy(running = true, lastAttemptAt = clock.now()))
        val outcome = withContext(Dispatchers.IO) {
            try {
                runSync(storeFor(profile.id), transportFor(profile.transport, profile.url, secrets.credentials), secrets.key, SyncClientSettings(profile.scope), profile.deviceId, clock)
            } catch (e: Exception) {
                // Un fallo de programa (dato local que no cuadra) no debe tumbar la app: queda en el estado, con su detalle, para el diagnóstico.
                SyncOutcome.Failure(classifyFailure(e))
            } finally {
                secrets.key.fill(0)
            }
        }
        val current = _statuses.value[profile.id] ?: SyncStatus()
        when (outcome) {
            is SyncOutcome.Success -> setStatus(profile.id, current.copy(running = false, lastSuccessAt = clock.now(), lastFailure = null))
            is SyncOutcome.Failure -> setStatus(profile.id, current.copy(running = false, lastFailure = outcome.failure))
        }
        return outcome
    }

    private fun setStatus(profileId: String, status: SyncStatus) {
        _statuses.update { it + (profileId to status) }
        _status.value = aggregateStatus(_statuses.value.values)
        if (!status.running) profileStore.saveStatus(profileId, status)
    }

    fun refreshConflicts() {
        val stores = _profiles.value.map { storeFor(it.id) }
        _conflicted.value = stores.flatMap { it.conflictedNoteIds() }.toSet()
        val merged = HashMap<String, java.time.Instant>()
        for (store in stores) for ((id, base) in store.snapshot().bases) {
            val at = base.updatedAt.toInstant()
            merged.merge(id, at) { old, new -> if (new < old) new else old }
        }
        _bases.value = merged
    }
```
(`import kotlinx.coroutines.flow.update`.) Nota: `before` copia el estado guardado del perfil; `current` se relee tras la pasada para no perder cambios.

- [ ] **Paso 3: conectar y desconectar**

```kotlin
sealed interface ConnectResult {
    data object Connected : ConnectResult
    data class NeedsChoice(val noteCount: Int) : ConnectResult
    /** Ya hay un perfil con esa clave y ese almacén: no se duplica. [name] es el nombre del existente, para decírselo al usuario. */
    data class AlreadyConnected(val name: String) : ConnectResult
    data class Failed(val failure: SyncFailure) : ConnectResult
}
```

```kotlin
    suspend fun connect(request: ConnectRequest, choice: FirstConnectChoice? = null): ConnectResult = withContext(Dispatchers.IO) {
        testConnection(request.transport, request.url, request.credentials)?.let { return@withContext ConnectResult.Failed(it) }
        passLock.withLock {
            val existing = _profiles.value
            // Misma clave y mismo almacén ya conectados: duplicarlo solo doblaría el trabajo y las pasadas.
            existing.firstOrNull { it.transport == request.transport && it.url == request.url && sameKey(it.id, request.share.key) }
                ?.let { return@withLock ConnectResult.AlreadyConnected(it.profileName?.takeIf { n -> n.isNotBlank() } ?: it.url) }

            val id = UUID.randomUUID().toString()
            val first = existing.isEmpty()
            val store = storeFor(id)
            val decision = firstConnectDecision(store.snapshot(), isFirstProfile = first)
            if (decision is FirstConnectDecision.AskUser && choice == null) return@withLock ConnectResult.NeedsChoice(decision.noteCount)

            // El orden importa: secretos y perfil primero y solo después descartar (en el primer perfil), porque descartar no se puede deshacer.
            vaultFor(id).save(SyncSecrets(request.share.key, request.credentials))
            val profile = SyncProfile(
                id, request.transport, request.url,
                SyncClientSettings.scopeOf(request.share.scope, request.share.noteIds, request.share.tag),
                request.share.profileName, profileStore.deviceId(),
            )
            profileStore.save(profile)
            completeFirstConnect(store, decision, choice)
            _unreadable.update { it - id }
            _profiles.value = existing + profile
            setStatus(id, SyncStatus())
            policyLock.withLock { policy.setProfileConfigured(true) }
            refreshConflicts()
            schedule.ensure()
            notesChanged()
            ConnectResult.Connected
        }.also { if (it == ConnectResult.Connected) request(SyncEvent.ManualSync) }
    }

    private fun sameKey(profileId: String, key: ByteArray): Boolean {
        val secrets = (vaultFor(profileId).load() as? VaultState.Ready)?.secrets ?: return false
        return try { secrets.key.contentEquals(key) } finally { secrets.key.fill(0) }
    }

    /** Desconecta un perfil sin tocar los demás: olvida su clave, su credencial, sus bases y sus conflictos. Las notas y las lápidas se quedan. */
    suspend fun disconnect(profileId: String) = withContext(Dispatchers.IO) {
        passLock.withLock {
            vaultFor(profileId).clear()
            profileStore.remove(profileId)
            storeFor(profileId).forgetSyncState()
            _profiles.update { list -> list.filter { it.id != profileId } }
            _statuses.update { it - profileId }
            _status.value = aggregateStatus(_statuses.value.values)
            _unreadable.update { it - profileId }
            if (_profiles.value.isEmpty()) {
                timer?.cancel()
                policyLock.withLock { policy.setProfileConfigured(false) }
                schedule.cancel()
            }
            refreshConflicts()
        }
    }
```
Importar `java.util.UUID`, `app.aldune.syncclient.LocalSyncStore`, `combineOutcomes`, `aggregateStatus`; quitar los imports que ya no se usen (`RoomLocalSyncStore`, `SyncProfileStore` sigue). Detalle: la `request` interna de `ConnectRequest` en `connect` sombrea la función `request(event)`; usa `this.request(SyncEvent.ManualSync)` o renombra el parámetro (ya compilaba así en el código previo con `request(...)` dentro de `also`; si el compilador se queja, renombra el parámetro a `req`).

`testConnection` y `transportFor` no cambian.

- [ ] **Paso 4: cableado en `AppContainer.ready`**

Sustituir la creación del vault y del controlador:

```kotlin
            // Los secretos de la sync tienen su propia clave de envoltura (otro alias): empezar de cero con las notas no los toca. Un fichero por
            // perfil; el perfil heredado conserva el de siempre (`vaultFileName`).
            val vaultFor = { profileId: String -> SecretVault(File(appContext.noBackupFilesDir, vaultFileName(profileId)), AndroidKeystoreKeyWrapper(VAULT_ALIAS)) }
            val storeFor = { profileId: String -> RoomLocalSyncStore(database, store, cipher, profileId) }
            val sync = SyncController(SyncProfileStore(appContext), vaultFor, storeFor, SystemClock, scope, SyncWork(appContext))
```
Imports: `app.aldune.syncclient.vaultFileName`; borrar el `const val VAULT_FILE` si ya no se usa. `SyncWork` no cambia.

- [ ] **Paso 5: compilar** — `./gradlew :app:assembleDebug`. Fallará en `SyncUi.kt` y `NoteListPane.kt` (siguen usando `profile`, `secretsUnreadable`, `disconnect()`): es lo esperado; se arregla en la Tarea 5. Para tener un commit que compile, haz la Tarea 5 antes de commitear, o commitea ambas juntas.

---

## Tarea 5: interfaz (lista de perfiles) y cadenas

**Ficheros:**
- Modificar: `android/app/src/main/kotlin/app/aldune/ui/SyncUi.kt`, `android/app/src/main/kotlin/app/aldune/ui/NoteListPane.kt`
- Modificar: `android/strings.tsv` (y regenerar los XML)

**Interfaces — consume:** las de la Tarea 4.

- [ ] **Paso 1: cadenas nuevas** — añadir al final del bloque de sync de `android/strings.tsv` (separadas por TAB, orden `key en es de fr pt-BR`):

```
sync_add_profile	Add another profile	Añadir otro perfil	Weiteres Profil hinzufügen	Ajouter un autre profil	Adicionar outro perfil
sync_add_intro	Paste a profile code to join it. Your other profiles keep syncing as before.	Pega un código de perfil para unirte. Tus otros perfiles siguen sincronizando igual.	Füge einen Profilcode ein, um beizutreten. Deine anderen Profile werden weiter wie bisher synchronisiert.	Colle un code de profil pour le rejoindre. Tes autres profils continuent de se synchroniser comme avant.	Cole um código de perfil para entrar. Seus outros perfis continuam sincronizando como antes.
sync_already_connected	You're already connected to “%1$s”.	Ya estás conectado a «%1$s».	Du bist bereits mit „%1$s“ verbunden.	Tu es déjà connecté à « %1$s ».	Você já está conectado a “%1$s”.
```
Luego `python android/scripts/gen_strings.py` y `python android/scripts/gen_strings.py --check` (debe pasar).

- [ ] **Paso 2: `NoteListPane.kt`** — línea 124: `val profile = controller?.profile?.collectAsStateWithLifecycle()?.value` → `val hasProfile = controller?.profiles?.collectAsStateWithLifecycle()?.value?.isNotEmpty() == true`; línea 171: `if (profile != null && status != null)` → `if (hasProfile && status != null)`; línea 261: `NoteSyncSignal.of(profile != null, …)` → `NoteSyncSignal.of(hasProfile, …)`. (`status`, `conflicted`, `bases` se quedan: el controlador los sigue exponiendo con el mismo tipo.)

- [ ] **Paso 3: `SyncUi.kt`** — cambiar `SyncSection`, `ConnectForm` y `ConnectedPanel`:

```kotlin
/** Ajustes → Sincronización: la lista de perfiles conectados, «Añadir otro perfil» y el interruptor del segundo plano. */
@Composable
fun SyncSection(viewModel: NotesViewModel) {
    val controller by viewModel.sync.collectAsStateWithLifecycle()
    val c = controller ?: return
    val profiles by c.profiles.collectAsStateWithLifecycle()
    val status by c.status.collectAsStateWithLifecycle()
    Section(R.string.sync_title)
    var adding by remember { mutableStateOf(false) }
    if (profiles.isEmpty()) {
        ConnectForm(c, additional = false, onDone = {})
    } else {
        profiles.forEach { ConnectedPanel(c, it) }
        Column(Modifier.padding(horizontal = 16.dp), verticalArrangement = Arrangement.spacedBy(6.dp)) {
            // «Sincronizar ahora» es de toda la sync, no de un perfil: una pasada recorre todos.
            Button(enabled = !status.running, onClick = { c.request(app.aldune.syncclient.SyncEvent.ManualSync) }) { Text(stringResource(R.string.sync_now)) }
            Text(stringResource(R.string.sync_backup_note), style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant, modifier = Modifier.padding(top = 8.dp))
            if (adding) ConnectForm(c, additional = true, onDone = { adding = false })
            else OutlinedButton(onClick = { adding = true }) { Text(stringResource(R.string.sync_add_profile)) }
            HorizontalDivider(Modifier.padding(top = 8.dp))
        }
        val settings by viewModel.settings.collectAsStateWithLifecycle()
        SwitchRow(stringResource(R.string.sync_in_background), stringResource(R.string.sync_in_background_hint), settings.syncInBackground) { on ->
            viewModel.updateSettings { it.copy(syncInBackground = on) }
        }
    }
}
```

`ConnectForm(controller, additional: Boolean, onDone: () -> Unit)`: 
- el texto de introducción: `Text(stringResource(if (additional) R.string.sync_add_intro else R.string.sync_intro), …)`; el `sync_howto` solo si `!additional`;
- nuevo estado `var alreadyName by remember { mutableStateOf<String?>(null) }`, a `null` en `reset()` y en `connect(...)` al empezar;
- en el `when` del resultado: `ConnectResult.Connected -> { reset(); onDone() }` y `is ConnectResult.AlreadyConnected -> alreadyName = result.name`;
- justo tras `errorText?.let { … }` del paso 2 del formulario: `alreadyName?.let { Text(stringResource(R.string.sync_already_connected, it), color = MaterialTheme.colorScheme.error) }`;
- el botón «Cancelar» (`TextButton … reset()`) en modo `additional` también llama a `onDone()`;
- en modo `additional` **no** puede salir `NeedsChoice` (el controlador nunca lo devuelve), así que los diálogos de primera conexión no se muestran; no hace falta tocarlos.

`ConnectedPanel(controller, profile)`: lee `val status = controller.statuses.collectAsStateWithLifecycle().value[profile.id] ?: SyncStatus()` y `val unreadable = profile.id in controller.unreadable.collectAsStateWithLifecycle().value`; elimina de aquí el botón «Sincronizar ahora» y la nota `sync_backup_note` (suben a `SyncSection`, ver arriba) y deja `OutlinedButton(onClick = { confirming = true }) { Text(sync_disconnect) }`; la confirmación llama a `controller.disconnect(profile.id)`. Mantén el `HorizontalDivider` solo si no es el último (basta quitarlo del panel: la sección ya pone uno al final).

- [ ] **Paso 4: compilar y pasar la batería JVM** — desde `android/`: `./gradlew :app:assembleDebug :syncclient:test :core:test :sync:test :text:test :domain:test`. Esperado: éxito.

- [ ] **Paso 5: commit (Tareas 4 y 5 juntas, porque una no compila sin la otra)**

```bash
git add android/app/src/main/kotlin/app/aldune/syncing/SyncController.kt android/app/src/main/kotlin/app/aldune/AppContainer.kt android/app/src/main/kotlin/app/aldune/ui/SyncUi.kt android/app/src/main/kotlin/app/aldune/ui/NoteListPane.kt android/strings.tsv
git add android/app/src/main/res
git commit -m "Android: la sync recorre varios perfiles y Ajustes los lista (anadir, desconectar uno)"
```
(Comprueba con `git status` que `android/app/src/main/res` solo cambia en los `strings.xml` regenerados.)

---

## Tarea 6: verificación en el móvil y documentación

**Ficheros:**
- Modificar: `docs/SYNC.md`, `docs/STATUS.md`, `docs/superpowers/specs/2026-10-26-android-varios-perfiles-design.md` (id heredado `default` en vez de UUID)
- Memoria: `C:\Users\nacho\.claude\MEMORIA.md` (Estado vivo, 1-2 líneas) y entrada en `MEMORIA-ARCHIVO.md`

- [ ] **Paso 1: instalar la compilación debug** (nunca la release de uso real): `adb install -r android/app/build/outputs/apk/debug/app-debug.apk`. Es otra aplicación (`….debug`) con datos propios.

- [ ] **Paso 2: recorrido manual con un servidor/WebDAV de prueba** (un perfil «todas» y otro con una nota elegida; se generan en una base temporal de Windows, nunca en `%LOCALAPPDATA%\Aldune`):
  1. Conectar el primer perfil; crear notas; sincroniza.
  2. «Añadir otro perfil», pegar el código del segundo y conectar: **no** pregunta «subir o descartar» y las notas locales siguen.
  3. Editar la nota compartida en el móvil: aparece en los dos almacenes.
  4. Desconectar un perfil: el otro sigue y las notas se quedan.
  5. Pegar otra vez el mismo código: «Ya estás conectado a …».
  6. (Si se puede) actualizar encima de una instalación con la versión anterior y un perfil: abre y sigue sincronizando.
  No hagas capturas de la pantalla de inicio ni de notificaciones. Desinstala la debug al terminar.

- [ ] **Paso 3: documentación.** En `SYNC.md`: Android admite varios perfiles; corregir «un código de carpeta compartida no sirve» solo si algo cambia (no cambia); añadir un apartado «Una nota compartida con la familia» (perfil con «Notas seleccionadas» en Windows, código a cada persona, «Añadir otro perfil» en Android, y que cada persona escribe su token). `STATUS.md`: bloque 1 hecho, qué queda (QR, pegar/compartir código, transparencia del segundo plano, pasada de errores/conflictos). En la spec, cambiar el `profileId` heredado a `"default"` y mencionar la decisión de `sync_base` más antigua para la señal de la fila.

- [ ] **Paso 4: memoria según el protocolo** (entrada detallada al principio de `MEMORIA-ARCHIVO.md`; en `MEMORIA.md`, 1-2 líneas en «Estado vivo» → Aldune). Sin secretos.

- [ ] **Paso 5: commit de la documentación**

```bash
git add docs/SYNC.md docs/STATUS.md docs/superpowers/specs/2026-10-26-android-varios-perfiles-design.md docs/superpowers/plans/2026-10-26-android-varios-perfiles.md
git commit -m "Docs: varios perfiles de sync en Android (guia, estado y plan)"
git log --format='%an <%ae> | %cn <%ce>' origin/main..HEAD
```
El último comando no debe mostrar a Claude, Codex, Anthropic ni OpenAI. **No hacer push.**

## Autorrevisión

- **Cobertura de la spec:** datos (T2, T3), migración del perfil único (T2 SQL + T3 prefs; el fichero de secretos no se mueve), pasadas (T4), primera conexión adicional (T1 + T4), desconectar (T4), interfaz (T5), conflictos con decisión del 26-10 (T2), pruebas y documentación (T1–T3, T6). Fuera de alcance respetado.
- **Tipos coherentes:** `SyncProfile.id` (String) en T3/T4/T5; `RoomLocalSyncStore(…, profileId)` en T2/T4; `LEGACY_PROFILE_ID = "default"` en T1/T2 (SQL)/T3.
- **Riesgos conocidos:** `LocalSyncStoreContract` y los tests de Room necesitan el Pixel; si no está, la Tarea 2 y 3 quedan sin verificar y hay que decirlo. La señal por nota con varios perfiles usa la base más antigua entre los perfiles que la tienen (puede decir «sincronizada» si un perfil donde la nota aún no tiene base va por detrás); es una simplificación documentada.
