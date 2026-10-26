package app.aldune.syncing

import app.aldune.core.SyncShareCodeData
import app.aldune.data.SyncProfile
import app.aldune.data.SyncProfileStore
import app.aldune.data.SyncTransportKind
import app.aldune.domain.Clock
import app.aldune.sync.HttpSyncTransport
import app.aldune.sync.SyncTransport
import app.aldune.sync.WebDavSyncTransport
import app.aldune.syncclient.FirstConnectChoice
import app.aldune.syncclient.FirstConnectDecision
import app.aldune.syncclient.LocalSyncStore
import app.aldune.syncclient.ProfileCredentials
import app.aldune.syncclient.SecretVault
import app.aldune.syncclient.SyncClientSettings
import app.aldune.syncclient.SyncDecision
import app.aldune.syncclient.SyncEvent
import app.aldune.syncclient.SyncFailure
import app.aldune.syncclient.SyncFailureKind
import app.aldune.syncclient.SyncOutcome
import app.aldune.syncclient.SyncSecrets
import app.aldune.syncclient.SyncStatus
import app.aldune.syncclient.SyncTriggerPolicy
import app.aldune.syncclient.VaultState
import app.aldune.syncclient.aggregateStatus
import app.aldune.syncclient.classifyFailure
import app.aldune.syncclient.combineOutcomes
import app.aldune.syncclient.completeFirstConnect
import app.aldune.syncclient.firstConnectDecision
import app.aldune.syncclient.runSync
import java.time.Duration
import java.util.UUID
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import kotlinx.coroutines.withContext

/** Programar y cancelar la sync periódica de segundo plano (WorkManager en la app). */
interface SyncSchedule {
    fun ensure()

    fun cancel()
}

/** Lo que hace falta para conectar: el código ya leído, el transporte elegido, la URL y la credencial (ya validadas). */
class ConnectRequest(val share: SyncShareCodeData, val transport: SyncTransportKind, val url: String, val credentials: ProfileCredentials)

sealed interface ConnectResult {
    data object Connected : ConnectResult

    /** Hay notas en este dispositivo: el usuario decide si se suben o se descartan (spec F5 §4.8). Nada se ha guardado todavía. Solo para el primer perfil. */
    data class NeedsChoice(val noteCount: Int) : ConnectResult

    /** Ya hay un perfil con esa clave y ese almacén: no se duplica. [name] es el del existente, para decírselo al usuario. */
    data class AlreadyConnected(val name: String) : ConnectResult

    data class Failed(val failure: SyncFailure) : ConnectResult
}

/**
 * Pega la sync con la app: guarda los perfiles, decide cuándo sincronizar con [SyncTriggerPolicy] y lanza las pasadas ([runSync]) de una en una.
 * Es cola fina: las reglas están en `syncclient` y se prueban en la JVM; aquí solo hay Android, hilos y persistencia.
 *
 * - **Varios perfiles** (spec de varios perfiles): una pasada recorre todos de uno en uno. Cada uno tiene su almacén remoto, sus secretos, sus
 *   bases y su estado; las notas y las lápidas son del dispositivo. El fallo de un perfil no impide que los demás sincronicen.
 * - **Silencioso** (decisión del usuario, spec F5 §1): un fallo solo cambia el estado; la política espera 60 s antes de reintentar solo.
 * - Conectar, desconectar y las pasadas se serializan con [passLock]: desconectar no puede dejar bases de un perfil olvidado escritas por una
 *   pasada que seguía en curso.
 */
class SyncController(
    private val profileStore: SyncProfileStore,
    private val vaultFor: (String) -> SecretVault,
    /** La vista de sync de un perfil: bases y conflictos suyos, notas y lápidas de todos. */
    private val storeFor: (String) -> LocalSyncStore,
    private val clock: Clock,
    private val scope: CoroutineScope,
    private val schedule: SyncSchedule,
) {
    /** Una pasada cambió notas locales (recibidas, borradas, a la papelera o conflictos): la lista y los borradores tienen que enterarse. Lo pone la interfaz. */
    @Volatile
    var onNotesChanged: (() -> Unit)? = null

    /** Lo mismo para el contenedor (reconciliar los recordatorios): vale también sin interfaz, en una pasada del trabajador de segundo plano. */
    @Volatile
    var afterNotesChanged: (() -> Unit)? = null

    private fun notesChanged() {
        afterNotesChanged?.invoke()
        onNotesChanged?.invoke()
    }

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
    private val policyLock = Mutex()
    private val passLock = Mutex()
    private var timer: Job? = null

    init {
        scope.launch(Dispatchers.IO) {
            if (_profiles.value.isNotEmpty()) schedule.ensure()
            _unreadable.value = _profiles.value.filter { vaultFor(it.id).load() is VaultState.Unreadable }.map { it.id }.toSet()
            refreshConflicts()
        }
    }

    /** El ajuste «sincronizar en segundo plano» cambió: programa o cancela el trabajo periódico (`SyncWork.ensure` lee el ajuste). */
    fun refreshSchedule() {
        scope.launch(Dispatchers.IO) { if (_profiles.value.isNotEmpty()) schedule.ensure() }
    }

    fun request(event: SyncEvent) {
        scope.launch(Dispatchers.IO) { run(event) }
    }

    /** Como [request], pero espera a que termine la pasada que el evento provoque: el trabajador de segundo plano no puede acabar antes. */
    suspend fun run(event: SyncEvent) {
        when (val decision = policyLock.withLock { policy.onEvent(event) }) {
            SyncDecision.RunNow -> pass()
            is SyncDecision.RunAt -> arm(decision)
            SyncDecision.Nothing -> Unit
        }
    }

    private fun arm(decision: SyncDecision.RunAt) {
        timer?.cancel()
        timer = scope.launch(Dispatchers.IO) {
            delay(Duration.between(clock.now(), decision.at).toMillis().coerceAtLeast(0))
            run(SyncEvent.TimerFired)
        }
    }

    /** Una pasada y lo que la política decida después (una repetición, un temporizador…). La política da la pasada por en curso hasta [SyncEvent.SyncFinished]. */
    private suspend fun pass() {
        val outcome = passLock.withLock { runPass() }
        run(SyncEvent.SyncFinished(outcome))
    }

    /** Recorre todos los perfiles. El resultado que ve la política es el combinado: un fallo de cualquiera cuenta como fallo. */
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

    /** Vuelve a leer qué notas tienen conflicto y las bases. La interfaz lo llama tras resolver uno (la sync lo hace sola al terminar una pasada). */
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

    // ───────────── Conectar y desconectar ─────────────

    /** Comprueba que el servidor contesta y que las credenciales valen. No escribe nada (solo lee el listado). */
    suspend fun testConnection(transport: SyncTransportKind, url: String, credentials: ProfileCredentials): SyncFailure? = withContext(Dispatchers.IO) {
        try {
            transportFor(transport, url, credentials).readAll()
            null
        } catch (e: Exception) {
            classifyFailure(e)
        }
    }

    /**
     * Conecta un perfil nuevo con [req]. El primer perfil, si ya hay notas en el dispositivo y no se ha dicho qué hacer con ellas ([choice]
     * nulo), devuelve [ConnectResult.NeedsChoice] sin tocar nada. Un perfil **adicional** nunca pregunta ni descarta (ver
     * `firstConnectDecision`). El orden importa: primero se guardan secretos y perfil y solo después se descartan notas, porque descartar no
     * se puede deshacer y un fallo al guardar no debe costar las notas.
     */
    suspend fun connect(req: ConnectRequest, choice: FirstConnectChoice? = null): ConnectResult = withContext(Dispatchers.IO) {
        testConnection(req.transport, req.url, req.credentials)?.let { return@withContext ConnectResult.Failed(it) }
        passLock.withLock {
            val existing = _profiles.value
            // Misma clave y mismo almacén ya conectados: duplicarlo solo doblaría el trabajo y las pasadas.
            existing.firstOrNull { it.transport == req.transport && it.url == req.url && sameKey(it.id, req.share.key) }
                ?.let { return@withLock ConnectResult.AlreadyConnected(it.profileName?.takeIf { n -> n.isNotBlank() } ?: it.url) }

            val id = UUID.randomUUID().toString()
            val store = storeFor(id)
            val decision = firstConnectDecision(store.snapshot(), isFirstProfile = existing.isEmpty())
            if (decision is FirstConnectDecision.AskUser && choice == null) return@withLock ConnectResult.NeedsChoice(decision.noteCount)

            vaultFor(id).save(SyncSecrets(req.share.key, req.credentials))
            val profile = SyncProfile(
                id, req.transport, req.url,
                SyncClientSettings.scopeOf(req.share.scope, req.share.noteIds, req.share.tag),
                req.share.profileName, profileStore.deviceId(),
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
        return try {
            secrets.key.contentEquals(key)
        } finally {
            secrets.key.fill(0)
        }
    }

    /**
     * Desconecta un perfil sin tocar los demás: olvida su clave, su credencial, sus bases y sus conflictos. Las notas y las lápidas se quedan.
     * Al desconectar el último, la sync vuelve a estar apagada del todo.
     */
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

    private fun transportFor(kind: SyncTransportKind, url: String, credentials: ProfileCredentials): SyncTransport = when (kind) {
        SyncTransportKind.SERVER -> HttpSyncTransport(url, (credentials as ProfileCredentials.Token).token)
        SyncTransportKind.WEBDAV -> (credentials as ProfileCredentials.Basic).let { WebDavSyncTransport(url, it.username, it.password) }
    }
}
