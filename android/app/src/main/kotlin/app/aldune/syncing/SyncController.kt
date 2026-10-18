package app.aldune.syncing

import app.aldune.core.SyncShareCodeData
import app.aldune.data.RoomLocalSyncStore
import app.aldune.data.SyncProfile
import app.aldune.data.SyncProfileStore
import app.aldune.data.SyncTransportKind
import app.aldune.domain.Clock
import app.aldune.sync.HttpSyncTransport
import app.aldune.sync.SyncTransport
import app.aldune.sync.WebDavSyncTransport
import app.aldune.syncclient.FirstConnectChoice
import app.aldune.syncclient.FirstConnectDecision
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
import app.aldune.syncclient.classifyFailure
import app.aldune.syncclient.completeFirstConnect
import app.aldune.syncclient.firstConnectDecision
import app.aldune.syncclient.runSync
import java.time.Duration
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
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

    /** Hay notas en este dispositivo: el usuario decide si se suben o se descartan (spec F5 §4.8). Nada se ha guardado todavía. */
    data class NeedsChoice(val noteCount: Int) : ConnectResult

    data class Failed(val failure: SyncFailure) : ConnectResult
}

/**
 * Pega la sync con la app: guarda el perfil, decide cuándo sincronizar con [SyncTriggerPolicy] y lanza las pasadas ([runSync]) de una en una.
 * Es cola fina: las reglas están en `syncclient` y se prueban en la JVM; aquí solo hay Android, hilos y persistencia.
 *
 * - **Silencioso** (decisión del usuario, spec F5 §1): un fallo solo cambia el estado; la política espera 60 s antes de reintentar solo.
 * - Conectar, desconectar y las pasadas se serializan con [passLock]: desconectar no puede dejar bases de un perfil olvidado escritas por una
 *   pasada que seguía en curso.
 */
class SyncController(
    private val profiles: SyncProfileStore,
    private val vault: SecretVault,
    private val store: RoomLocalSyncStore,
    private val clock: Clock,
    private val scope: CoroutineScope,
    private val schedule: SyncSchedule,
) {
    /** Una pasada cambió notas locales (recibidas, borradas, a la papelera o conflictos): la lista y los borradores tienen que enterarse. Lo pone la interfaz. */
    @Volatile
    var onNotesChanged: (() -> Unit)? = null

    private val _profile = MutableStateFlow(profiles.load())
    val profile: StateFlow<SyncProfile?> = _profile.asStateFlow()

    private val _status = MutableStateFlow(profiles.loadStatus())
    val status: StateFlow<SyncStatus> = _status.asStateFlow()

    /** Hay perfil pero sus secretos no se pueden abrir (el Keystore perdió la clave): hay que volver a pegar el código. */
    private val _secretsUnreadable = MutableStateFlow(false)
    val secretsUnreadable: StateFlow<Boolean> = _secretsUnreadable.asStateFlow()

    private val _conflicted = MutableStateFlow<Set<String>>(emptySet())
    val conflicted: StateFlow<Set<String>> = _conflicted.asStateFlow()

    private val policy = SyncTriggerPolicy(clock, profileConfigured = _profile.value != null)
    private val policyLock = Mutex()
    private val passLock = Mutex()
    private var timer: Job? = null

    init {
        scope.launch(Dispatchers.IO) {
            if (_profile.value != null) schedule.ensure()
            if (_profile.value != null) _secretsUnreadable.value = vault.load() is VaultState.Unreadable
            refreshConflicts()
        }
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

    private suspend fun runPass(): SyncOutcome {
        val profile = _profile.value ?: return SyncOutcome.Failure(SyncFailure(SyncFailureKind.UNKNOWN, "No sync profile"))
        val secrets = (vault.load() as? VaultState.Ready)?.secrets
        if (secrets == null) {
            _secretsUnreadable.value = true
            return SyncOutcome.Failure(SyncFailure(SyncFailureKind.UNKNOWN, "The stored sync secrets can't be read"))
        }
        _secretsUnreadable.value = false
        setStatus(_status.value.copy(running = true, lastAttemptAt = clock.now()))
        val outcome = withContext(Dispatchers.IO) {
            try {
                runSync(store, transportFor(profile.transport, profile.url, secrets.credentials), secrets.key, SyncClientSettings(profile.scope), profile.deviceId, clock)
            } catch (e: Exception) {
                // Un fallo de programa (dato local que no cuadra) no debe tumbar la app: queda en el estado, con su detalle, para el diagnóstico.
                SyncOutcome.Failure(classifyFailure(e))
            } finally {
                secrets.key.fill(0)
            }
        }
        when (outcome) {
            is SyncOutcome.Success -> setStatus(_status.value.copy(running = false, lastSuccessAt = clock.now(), lastFailure = null))
            is SyncOutcome.Failure -> setStatus(_status.value.copy(running = false, lastFailure = outcome.failure))
        }
        refreshConflicts()
        if (outcome is SyncOutcome.Success && outcome.received + outcome.tombstonesApplied + outcome.trashed + outcome.conflicts > 0) onNotesChanged?.invoke()
        return outcome
    }

    private fun setStatus(status: SyncStatus) {
        _status.value = status
        if (!status.running) profiles.saveStatus(status)
    }

    private fun refreshConflicts() {
        _conflicted.value = store.conflictedNoteIds()
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
     * Conecta con el perfil de [request]. Si ya hay notas en el dispositivo y no se ha dicho qué hacer con ellas ([choice] nulo) devuelve
     * [ConnectResult.NeedsChoice] sin tocar nada. El orden importa: primero se guardan secretos y perfil y solo después se descartan notas,
     * porque descartar no se puede deshacer y un fallo al guardar no debe costar las notas.
     */
    suspend fun connect(request: ConnectRequest, choice: FirstConnectChoice? = null): ConnectResult = withContext(Dispatchers.IO) {
        testConnection(request.transport, request.url, request.credentials)?.let { return@withContext ConnectResult.Failed(it) }
        passLock.withLock {
            val decision = firstConnectDecision(store.snapshot())
            if (decision is FirstConnectDecision.AskUser && choice == null) return@withLock ConnectResult.NeedsChoice(decision.noteCount)

            vault.save(SyncSecrets(request.share.key, request.credentials))
            val profile = SyncProfile(
                request.transport, request.url,
                SyncClientSettings.scopeOf(request.share.scope, request.share.noteIds, request.share.tag),
                request.share.profileName, profiles.deviceId(),
            )
            profiles.save(profile)
            completeFirstConnect(store, decision, choice)
            _secretsUnreadable.value = false
            setStatus(SyncStatus())
            _profile.value = profile
            policyLock.withLock { policy.setProfileConfigured(true) }
            refreshConflicts()
            schedule.ensure()
            onNotesChanged?.invoke()
            ConnectResult.Connected
        }.also { if (it == ConnectResult.Connected) request(SyncEvent.ManualSync) }
    }

    /** Olvida clave, token y bases. Las notas se quedan; si se vuelve a conectar con notas, se vuelve a preguntar. */
    suspend fun disconnect() = withContext(Dispatchers.IO) {
        timer?.cancel()
        passLock.withLock {
            policyLock.withLock { policy.setProfileConfigured(false) }
            vault.clear()
            profiles.clear()
            store.forgetSyncState()
            _profile.value = null
            _secretsUnreadable.value = false
            _status.value = SyncStatus()
            schedule.cancel()
            refreshConflicts()
        }
    }

    private fun transportFor(kind: SyncTransportKind, url: String, credentials: ProfileCredentials): SyncTransport = when (kind) {
        SyncTransportKind.SERVER -> HttpSyncTransport(url, (credentials as ProfileCredentials.Token).token)
        SyncTransportKind.WEBDAV -> (credentials as ProfileCredentials.Basic).let { WebDavSyncTransport(url, it.username, it.password) }
    }
}
