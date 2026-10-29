package app.aldune.ui

import android.app.Application
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import app.aldune.AldunaApp
import app.aldune.AppContainer
import app.aldune.capture.CaptureRequest
import app.aldune.data.AppSettings
import app.aldune.data.SettingsStore
import app.aldune.core.ProtectedNotes
import app.aldune.core.UnlockedProtectedNote
import app.aldune.domain.LineEnding
import app.aldune.domain.LostReason
import app.aldune.domain.Note
import app.aldune.domain.NewNoteColorSettings
import app.aldune.domain.NoteListItem
import app.aldune.domain.NoteListOrder
import app.aldune.domain.NoteListing
import app.aldune.text.NoteColorAssigner
import app.aldune.text.NoteColorAssignment
import app.aldune.domain.NoteResult
import app.aldune.domain.NoteService
import app.aldune.domain.SharedNote
import app.aldune.domain.SharedNoteText
import app.aldune.domain.NoteState
import app.aldune.syncclient.ConflictKind
import app.aldune.syncclient.ProtectionError
import app.aldune.text.ProtectedTitleRules
import app.aldune.syncclient.ProtectionResult
import app.aldune.syncclient.ProtectionService
import app.aldune.syncclient.SyncEvent
import app.aldune.syncing.SyncController
import app.aldune.text.ConflictDiff
import app.aldune.text.DiffLine
import app.aldune.text.NoteThemes
import app.aldune.text.NoteTitleHelper
import app.aldune.text.NoteTone
import app.aldune.widget.WidgetUpdater
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.NonCancellable
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharedFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import kotlinx.coroutines.withContext
import java.time.Instant

/** Una nota que ganó un borrado de otro equipo mientras se editaba aquí: ya no existe y su versión editada se puede recuperar. */
data class OrphanConflict(val noteId: String, val title: String, val isProtected: Boolean)

sealed interface UiState {
    data object Loading : UiState

    /** La clave de las notas no se puede recuperar; no se ha borrado nada. La pantalla explica y ofrece «empezar de cero». */
    data class KeyLost(val reason: LostReason) : UiState

    data class Ready(
        val filter: NoteState = NoteState.ACTIVE,
        val query: String = "",
        val tag: String? = null,
        val items: List<NoteListItem> = emptyList(),
        val allTags: List<String> = emptyList(),
        /** Cuántas notas hay en cada estado, para las pestañas. */
        val counts: Map<NoteState, Int> = emptyMap(),
        /** Los conflictos sin nota (ganó un borrado): la lista «N conflictos». */
        val orphans: List<OrphanConflict> = emptyList(),
        /** La hora de los recordatorios puestos, por id de nota: la campana de la fila. */
        val reminders: Map<String, Instant> = emptyMap(),
    ) : UiState
}

/**
 * Pega el dominio con la interfaz. Todo el trabajo con la base va en el hilo de E/S y de uno en uno (un cerrojo): las operaciones del
 * dominio ya son atómicas, pero el orden en que se aplican y se refresca la lista tiene que ser el que pidió el usuario.
 */
private const val AUTOSAVE_MS = 600L
private const val HIGHLIGHT_MS = 3_000L

class NotesViewModel(application: Application) : AndroidViewModel(application) {
    private val container = (application as AldunaApp).container
    private val io = Mutex()
    private val settingsStore = SettingsStore(application)
    private var service: NoteService? = null
    private var protection: ProtectionService? = null
    private var ready: AppContainer.Opened.Ready? = null
    private var placeholder: String = "New note"
    private var protectedTitle: String = "Protected note"

    private val _sync = MutableStateFlow<SyncController?>(null)

    /** La sync, cuando la base ya está abierta (hasta entonces `null`). La pantalla de Ajustes y la lista leen su estado de aquí. */
    val sync: StateFlow<SyncController?> = _sync.asStateFlow()

    private val _externalEdits = MutableSharedFlow<String>(extraBufferCapacity = 16)

    /** La sync cambió el texto de una nota abierta en el editor (clave del borrador): el editor tiene que recargarlo, o lo pisaría al escribir. */
    val externalEdits: SharedFlow<String> = _externalEdits
    private val _deletedNotes = MutableSharedFlow<String>(extraBufferCapacity = 16)

    /** La sync borró (por un borrado firmado) la nota abierta en el editor de la clave dada: el editor tiene que cerrarse. */
    val deletedNotes: SharedFlow<String> = _deletedNotes
    private var started = false

    private val _capture = MutableStateFlow<CaptureRequest?>(null)

    /** Lo que pidió una puerta de captura y la interfaz aún no ha atendido. Se queda ahí hasta que haya pantalla (la base tarda en abrirse). */
    val capture: StateFlow<CaptureRequest?> = _capture.asStateFlow()

    fun requestCapture(request: CaptureRequest) {
        _capture.value = request
    }

    fun captureHandled() {
        _capture.value = null
    }

    /** Pide repintar el widget (agrupado: ver [WidgetUpdater]). Se llama tras todo lo que cambia notas o ajustes que el widget pinta. */
    private fun widgetChanged() = WidgetUpdater.request(getApplication())

    /** Qué hacer al tocar una tira del widget. */
    enum class OpenOutcome { OPEN, GONE }

    /** La nota puede haber cambiado desde que el widget se pintó: ya no existe, o ya no está activa (GONE). Una protegida se abre: el editor pide su contraseña. */
    suspend fun resolveOpen(id: String): OpenOutcome = withContext(Dispatchers.IO) {
        io.withLock {
            val note = service?.get(id)
            when {
                note == null || note.state != NoteState.ACTIVE -> OpenOutcome.GONE
                else -> OpenOutcome.OPEN
            }
        }
    }

    private val _highlight = MutableStateFlow<String?>(null)

    /** La fila que la lista señala un momento (tocar una protegida en el widget: abrirla es de F7, aquí solo se ve dónde está). */
    val highlight: StateFlow<String?> = _highlight.asStateFlow()

    fun highlightNote(id: String) {
        // Con una búsqueda o una etiqueta puesta la fila podría no verse.
        update { it.copy(filter = NoteState.ACTIVE, query = "", tag = null) }
        _highlight.value = id
        viewModelScope.launch {
            delay(HIGHLIGHT_MS)
            if (_highlight.value == id) _highlight.value = null
        }
    }

    /** Qué pasó al guardar un texto compartido. */
    sealed interface SharedOutcome {
        /** Se creó la nota [id]: hay que abrirla en el editor. */
        data class Created(val id: String) : SharedOutcome
        data object Empty : SharedOutcome
        data object TooLong : SharedOutcome
    }

    /**
     * Guarda lo que otra app compartió. La nota se crea ya, sin esperar al editor: el texto no lo ha escrito el usuario aquí, y si no se
     * crea ahora, un cierre antes de tocar nada lo perdería. (Una nota nueva vacía no existe hasta el primer carácter; esta ya tiene texto.)
     */
    suspend fun createShared(text: String?, subject: String?): SharedOutcome = withContext(Dispatchers.IO + NonCancellable) {
        when (val composed = SharedNoteText.compose(text, subject)) {
            SharedNote.Empty -> SharedOutcome.Empty
            SharedNote.TooLong -> SharedOutcome.TooLong
            is SharedNote.Text -> io.withLock {
                val created = service?.create(composed.value, settings = newNoteColors()) as? NoteResult.Ok
                refreshLocked()
                syncEvent(SyncEvent.LocalChange)
                widgetChanged()
                created?.let { SharedOutcome.Created(it.value.id) } ?: SharedOutcome.Empty
            }
        }
    }

    private val _state = MutableStateFlow<UiState>(UiState.Loading)
    val state: StateFlow<UiState> = _state.asStateFlow()

    private val _settings = MutableStateFlow(settingsStore.load())
    val settings: StateFlow<AppSettings> = _settings.asStateFlow()

    fun updateSettings(transform: (AppSettings) -> AppSettings) {
        val previous = _settings.value
        val next = transform(previous)
        _settings.value = next
        settingsStore.save(next)
        // El orden de la lista y el trabajo de segundo plano dependen de estos ajustes: se aplican al momento.
        if (next.listOrder != previous.listOrder) launchIo { refreshLocked() }
        if (next.syncInBackground != previous.syncInBackground) _sync.value?.refreshSchedule()
        // El aspecto, el idioma y la vista previa cambian lo que pinta el widget.
        widgetChanged()
    }

    init {
        launchIo {
            when (val opened = container.open()) {
                is AppContainer.Opened.KeyLost -> _state.value = UiState.KeyLost(opened.reason)
                is AppContainer.Opened.Ready -> start(opened.notes, opened)
            }
        }
    }

    /** El título de una nota sin texto, en el idioma de la interfaz (lo pone la pantalla al arrancar). */
    fun setPlaceholder(text: String) {
        placeholder = text
    }

    /** El título de una nota protegida en la lista, en el idioma de la interfaz. */
    fun setProtectedTitle(text: String) {
        protectedTitle = text
    }

    fun startOver() = launchIo { container.startOver().let { start(it.notes, it) } }

    override fun onCleared() {
        // La sync sigue viva (es del proceso); solo se suelta la referencia a este ViewModel, que ya no existe.
        _sync.value?.onNotesChanged = null
    }

    private fun start(notes: NoteService, opened: AppContainer.Opened.Ready) {
        service = notes
        protection = opened.protection
        ready = opened
        _sync.value = opened.sync.also { it.onNotesChanged = { launchIo { reconcileDrafts(); refreshLocked(); widgetChanged() } } }
        // Como Windows: la papelera se purga al abrir, con los días de los ajustes (30 por defecto).
        notes.purgeTrashed(_settings.value.trashDays)
        // Al abrir: avisa de los vencidos con la app muerta, limpia los de notas que ya no están y programa los demás.
        opened.reminders.reconcile()
        // Un receptor (la alarma, los botones de la notificación) puede cambiar recordatorios con la lista abierta: la campana se repinta.
        viewModelScope.launch { opened.reminders.changes.collect { launchIo { refreshLocked() } } }
        _state.value = UiState.Ready()
        refresh()
        widgetChanged()
        started = true
        syncEvent(SyncEvent.AppOpened)
    }

    /** Volvió a primer plano (la actividad pasó por `onStart`). Antes de que la base esté abierta no hace nada: ya vendrá `AppOpened`. */
    fun onForeground() {
        if (started) syncEvent(SyncEvent.Foreground)
    }

    fun syncEvent(event: SyncEvent) {
        _sync.value?.request(event)
    }

    /**
     * Tras una pasada que cambió notas: si una nota abierta cambió de texto en la base y el usuario no ha escrito nada nuevo, el editor se
     * recarga. Si no, su siguiente tecla guardaría el texto viejo encima del que acaba de llegar y esa edición se perdería sin conflicto.
     * (Si el usuario había escrito a la vez, gana lo suyo: sube como una edición más reciente.)
     */
    private fun reconcileDrafts() {
        val notes = service ?: return
        for ((key, draft) in drafts.entries.toList()) {
            val id = draft.noteId ?: continue
            val note = notes.get(id)
            if (note == null) {
                drafts.remove(key)
                _deletedNotes.tryEmit(key)
                continue
            }
            val stored = note.text
            if (stored != draft.lastSaved && draft.latest == draft.lastSaved) {
                draft.lastSaved = stored
                draft.latest = stored
                _externalEdits.tryEmit(key)
            }
        }
    }

    /** ¿La nota de [key] es protegida? (Una nota nueva, "", no lo es.) */
    suspend fun isProtected(key: String): Boolean = withContext(Dispatchers.IO) {
        io.withLock { key.isNotEmpty() && service?.get(key)?.isProtected == true }
    }

    /** El texto que tiene ahora el borrador de [key] (para recargar el editor tras [externalEdits]). */
    fun draftText(key: String): String = drafts[key]?.latest ?: ""

    fun setFilter(filter: NoteState) = update { it.copy(filter = filter, tag = null) }

    fun setQuery(query: String) = update { it.copy(query = query) }

    fun setTag(tag: String?) = update { it.copy(tag = tag) }

    // ───────────── Borradores: lo que se está escribiendo en el editor ─────────────
    // Viven aquí y no en el panel del editor: el panel se destruye al volver a la lista o al rotar, y un guardado a medias no puede
    // cancelarse con él (duplicaría la nota nueva). Una nota nueva no existe hasta que se escribe su primer carácter.

    private class Draft(var noteId: String?, var lastSaved: String, var latest: String) {
        var pending: Job? = null
    }

    private val drafts = HashMap<String, Draft>()

    /** Abre el borrador de [key] (el id de una nota, o "" para una nota nueva) y devuelve su texto actual. */
    suspend fun openDraft(key: String): String = withContext(Dispatchers.IO) {
        io.withLock {
            // Al rotar, el panel de una nota nueva se recrea con la misma clave vacía: se sigue con el borrador que ya hay (con su id, si ya se
            // creó la nota) en vez de abrir uno en blanco. Para empezar otra nota nueva de verdad está [startNewNote].
            if (key.isEmpty()) drafts[key]?.let { return@withLock it.latest }
            val text = if (key.isEmpty()) "" else service?.get(key)?.text ?: ""
            drafts[key] = Draft(key.ifEmpty { null }, text, text)
            aliases.remove(key)
            text
        }
    }

    /** Se pidió una nota nueva (el «+»): se guarda lo que hubiera y se olvida el borrador anterior de nota nueva. */
    fun startNewNote() {
        flushDraft("")
        drafts.remove("")
        aliases.remove("")
    }

    /** El texto cambió: se guarda tras una pausa en la escritura. */
    fun updateDraft(key: String, text: String) {
        val draft = drafts[key] ?: return
        draft.latest = text
        draft.pending?.cancel()
        draft.pending = viewModelScope.launch(Dispatchers.IO) {
            delay(AUTOSAVE_MS)
            persist(draft)
        }
    }

    /** Guarda ya (al salir de la nota o de la app). Sobrevive a que el panel del editor se destruya. */
    fun flushDraft(key: String) {
        val draft = drafts[key] ?: return
        draft.pending?.cancel()
        viewModelScope.launch(Dispatchers.IO) { persist(draft) }
    }

    fun flushAll() = drafts.keys.toList().forEach(::flushDraft)

    private suspend fun persist(draft: Draft) = withContext(NonCancellable) {
        io.withLock {
            val text = draft.latest
            if (text == draft.lastSaved) return@withLock
            val id = draft.noteId
            if (id == null) {
                if (text.isNotEmpty()) (service?.create(text, settings = newNoteColors()) as? NoteResult.Ok)?.let { draft.noteId = it.value.id; draft.lastSaved = text }
            } else if (service?.edit(id, text) is NoteResult.Ok) {
                draft.lastSaved = text
            }
            refreshLocked()
            syncEvent(SyncEvent.LocalChange)
            widgetChanged()
        }
    }

    // ───────────── Notas protegidas (F7) ─────────────
    // El texto de una protegida abierta vive **solo en memoria**: en la sesión de aquí (con la clave ya derivada) y en el campo del editor. Nunca
    // pasa por los borradores (`drafts`), ni por el estado guardado de la actividad, ni por la base en claro. Se cierra al salir de la nota
    // y al pasar la app a segundo plano. Derivar la clave tarda (PBKDF2): no se hace con el cerrojo `io` cogido, o la lista y el
    // autoguardado de las demás notas esperarían.

    /** Qué pasó al abrir una protegida. */
    sealed interface UnlockOutcome {
        /** [text] ya va con `\n` (los saltos de Windows se normalizan; `save` los devuelve). */
        data class Opened(val text: String) : UnlockOutcome

        data object WrongPassword : UnlockOutcome

        /** El contenido cifrado está dañado. */
        data object Unreadable : UnlockOutcome

        /** Lo protegió una versión más nueva de Aldune. */
        data object Unsupported : UnlockOutcome

        /** La nota ya no existe o ya no es protegida. */
        data object Gone : UnlockOutcome
    }

    private class ProtectedSession(val unlocked: UnlockedProtectedNote, var lastSaved: String) {
        @Volatile var latest: String = lastSaved
        var pending: Job? = null
    }

    private val sessions = HashMap<String, ProtectedSession>()
    private val _unlockedIds = MutableStateFlow<Set<String>>(emptySet())

    /** Las notas protegidas abiertas ahora. El editor se bloquea (y borra su texto) en cuanto la suya sale de aquí; la actividad lo usa para ocultar Recientes. */
    val unlockedIds: StateFlow<Set<String>> = _unlockedIds.asStateFlow()

    private val _protectionLost = MutableSharedFlow<String>(extraBufferCapacity = 16)

    /** Una protegida abierta se cerró porque no se pudo guardar (cambió en otro equipo): el editor lo explica. */
    val protectionLost: SharedFlow<String> = _protectionLost

    /** El texto de la sesión de [id] (para recargar el editor tras rotar), o `null` si no está abierta. */
    /**
     * El id real de la nota que edita el editor con la clave [key]: una nota creada en esta sesión del editor tiene clave "" hasta que se
     * reabre, pero ya tiene id (el de su borrador). Sin esto, proteger una nota recién escrita buscaba la nota "" y fallaba siempre.
     */
    private fun idOf(key: String): String = drafts[key]?.noteId ?: aliases[key] ?: key

    /** Clave del editor → id, para una nota nueva que se protegió sin salir del editor (su borrador ya se olvidó). Se vacía al empezar otra nueva. */
    private val aliases = HashMap<String, String>()

    fun protectedText(key: String): String? = synchronized(sessions) { sessions[idOf(key)]?.latest }

    suspend fun unlockProtected(key: String, password: String): UnlockOutcome = withContext(Dispatchers.Default) {
        val id = idOf(key)
        val service = protection ?: return@withContext UnlockOutcome.Gone
        when (val result = service.unlock(id, password)) {
            is ProtectionResult.Ok -> {
                val text = LineEnding.normalize(result.value.text)
                synchronized(sessions) {
                    sessions.put(id, ProtectedSession(result.value, text))?.let { it.pending?.cancel(); it.unlocked.close() }
                    _unlockedIds.value = sessions.keys.toSet()
                }
                UnlockOutcome.Opened(text)
            }
            is ProtectionResult.Err -> when (result.error) {
                ProtectionError.WrongPassword -> UnlockOutcome.WrongPassword
                is ProtectionError.UnreadableContent -> UnlockOutcome.Unreadable
                is ProtectionError.UnsupportedVersion -> UnlockOutcome.Unsupported
                else -> UnlockOutcome.Gone
            }
        }
    }

    /** El texto del editor de una protegida abierta cambió: se guarda tras una pausa, reutilizando la clave de la sesión. */
    fun updateProtectedText(key: String, text: String) {
        val id = idOf(key)
        val session = synchronized(sessions) { sessions[id] } ?: return
        session.latest = text
        session.pending?.cancel()
        session.pending = viewModelScope.launch(Dispatchers.IO) {
            delay(AUTOSAVE_MS)
            persistProtected(id, session)
        }
    }

    private suspend fun persistProtected(id: String, session: ProtectedSession) = withContext(NonCancellable) {
        io.withLock {
            val text = session.latest
            if (text == session.lastSaved) return@withLock
            val service = protection ?: return@withLock
            val result = try {
                service.save(id, session.unlocked, text)
            } catch (e: IllegalStateException) {
                return@withLock // la sesión se cerró mientras tanto: no hay nada que guardar
            }
            when (result) {
                is ProtectionResult.Ok -> {
                    session.lastSaved = text
                    refreshLocked()
                    syncEvent(SyncEvent.LocalChange)
                    widgetChanged()
                }
                // La nota ya no es la que se abrió (otra contraseña llegó por sync, o desapareció): no se pisa; se cierra y se avisa.
                is ProtectionResult.Err -> {
                    closeSession(id, session)
                    _protectionLost.tryEmit(id)
                }
            }
        }
    }

    // Solo cierra la sesión que se pidió: si el usuario reabrió la nota mientras se guardaba la anterior, la nueva no se toca.
    private fun closeSession(id: String, expected: ProtectedSession) = synchronized(sessions) {
        if (sessions[id] === expected) {
            sessions.remove(id)
            expected.pending?.cancel()
            expected.unlocked.close()
            _unlockedIds.value = sessions.keys.toSet()
        }
    }

    /** Guarda lo pendiente y cierra la sesión de [id] (salir de la nota). */
    fun lockProtected(key: String) {
        val id = idOf(key)
        val session = synchronized(sessions) { sessions[id] } ?: return
        session.pending?.cancel()
        viewModelScope.launch(Dispatchers.IO) {
            persistProtected(id, session)
            closeSession(id, session)
        }
    }

    /** La app pasó a segundo plano: todas las protegidas se vuelven a bloquear. */
    fun lockAllProtected() = synchronized(sessions) { sessions.keys.toList() }.forEach(::lockProtected)

    /** Quita de la memoria la sesión de [id] sin guardar nada (tras quitar la protección: el texto ya está en la nota). */
    fun dropProtected(key: String) {
        val id = idOf(key)
        val session = synchronized(sessions) { sessions[id] } ?: return
        closeSession(id, session)
    }

    /**
     * Protege la nota con la contraseña. Antes se guarda lo escrito (el texto que se cifra es el último guardado) y se olvida su borrador:
     * a partir de aquí el texto no pasa por los borradores.
     */
    suspend fun protectNote(key: String, password: String, confirmation: String, visibleName: String? = null): ProtectionResult<Note> = withContext(Dispatchers.Default) {
        val id = idOf(key)
        val service = protection ?: return@withContext ProtectionResult.Err(ProtectionError.NotFound(id))
        drafts[key]?.let { it.pending?.cancel(); persist(it) }
        val result = service.protect(id, password, confirmation, visibleName)
        if (result is ProtectionResult.Ok) {
            drafts.remove(key)
            if (key != id) aliases[key] = id
            io.withLock {
                refreshLocked()
                syncEvent(SyncEvent.LocalChange)
                widgetChanged()
            }
        }
        result
    }

    /**
     * Quita la protección. La sesión abierta (si la hay) se guarda antes y **no se cierra aquí**: si la contraseña es la equivocada el editor sigue
     * abierto. Tras un éxito, quien llama recarga el texto con [openDraft] y suelta la sesión con [dropProtected].
     */
    suspend fun removeProtection(key: String, password: String): ProtectionResult<Note> = withContext(Dispatchers.Default) {
        val id = idOf(key)
        val service = protection ?: return@withContext ProtectionResult.Err(ProtectionError.NotFound(id))
        synchronized(sessions) { sessions[id] }?.let { it.pending?.cancel(); persistProtected(id, it) }
        val result = service.removeProtection(id, password)
        if (result is ProtectionResult.Ok) {
            io.withLock {
                refreshLocked()
                syncEvent(SyncEvent.LocalChange)
                widgetChanged()
            }
        }
        result
    }

    // ───────────── Conflictos (F7) ─────────────

    /** Lo que se enseña de la otra versión de un conflicto. */
    sealed interface OtherVersion {
        /** La otra versión es un borrado hecho en otro equipo. */
        data object Deleted : OtherVersion

        /** La otra versión está protegida (con su propia contraseña, que puede ser otra): se ve al desbloquearla. */
        data object Protected : OtherVersion

        data class Text(val lines: List<DiffLine>) : OtherVersion
    }

    class ConflictView(val noteId: String, val kind: ConflictKind, val other: OtherVersion)

    /** Las líneas de la otra versión; marcadas las que cambian si se conoce el texto de aquí (una protegida bloqueada no lo da). */
    private fun diffLines(here: String?, other: String): List<DiffLine> =
        if (here != null) ConflictDiff.lineDiff(here, other) else other.split('\n').mapIndexed { i, line -> DiffLine(i + 1, line, false) }

    /** El conflicto de la nota [noteId] tal como lo enseña el editor, o `null` si no hay. */
    suspend fun conflictFor(noteId: String): ConflictView? = withContext(Dispatchers.IO) {
        io.withLock {
            val current = ready ?: return@withLock null
            val conflict = current.conflictStore.get(noteId) ?: return@withLock null
            val losing = conflict.losing
            val note = losing.note
            val other = when {
                losing.tombstone -> OtherVersion.Deleted
                note == null -> return@withLock null
                note.isProtected -> OtherVersion.Protected
                else -> OtherVersion.Text(diffLines(service?.get(noteId)?.takeUnless { it.isProtected }?.text, LineEnding.normalize(note.text)))
            }
            ConflictView(noteId, current.conflicts.kind(conflict), other)
        }
    }

    /** Abre la otra versión de un conflicto si está protegida. `null` si la contraseña no es la suya. Deriva la clave: fuera del cerrojo. */
    suspend fun unlockOtherVersion(noteId: String, password: String): List<DiffLine>? = withContext(Dispatchers.Default) {
        val (conflict, here) = io.withLock {
            Pair(ready?.conflictStore?.get(noteId), service?.get(noteId)?.takeUnless { it.isProtected }?.text)
        }
        val content = conflict?.losing?.note?.protectedContent ?: return@withContext null
        val plain = ProtectedNotes.tryUnprotect(content, password) ?: return@withContext null
        diffLines(here, LineEnding.normalize(plain))
    }

    /**
     * Resuelve el conflicto de [noteId]: [useOther] aplica la otra versión (como `RestoreConflict` de Windows); si no, se descarta el conflicto y
     * se queda lo de aquí. `false` si ya no existía o no se pudo aplicar (no se escribe nada).
     */
    suspend fun resolveConflict(noteId: String, useOther: Boolean): Boolean = withContext(Dispatchers.IO + NonCancellable) {
        io.withLock {
            val current = ready ?: return@withLock false
            // La nota, la lápida, el conflicto y el recordatorio cambian juntos o no cambian.
            val result = if (useOther) {
                current.database.runInTransaction<NoteResult<Unit>> { current.conflicts.useOther(noteId) }
            } else {
                current.conflicts.keepCurrent(noteId)
            }
            current.reminders.reconcile()
            refreshLocked()
            current.sync.refreshConflicts()
            syncEvent(SyncEvent.LocalChange)
            widgetChanged()
            result is NoteResult.Ok
        }
    }

    /** Color y etiquetas de una nota, para su menú. */
    class NoteOptions(val noteId: String, val color: String, val tags: List<String>, val isProtected: Boolean, val protectedTitle: String? = null)

    /**
     * Las opciones de la nota que se edita en [key], o `null` si todavía no existe (una nota nueva sin texto). Antes se guarda lo escrito,
     * para que una nota recién empezada ya tenga id al abrir el menú.
     */
    suspend fun noteOptions(key: String): NoteOptions? = withContext(Dispatchers.IO) {
        drafts[key]?.let { draft ->
            draft.pending?.cancel()
            persist(draft)
        }
        io.withLock {
            val id = idOf(key).ifEmpty { return@withLock null }
            service?.get(id)?.let { NoteOptions(id, it.color, it.tags, it.isProtected, it.protectedTitle) }
        }
    }

    /**
     * Pone (o quita, si queda vacío) el nombre visible de una nota protegida: se ve en la lista y el widget aunque la nota esté cerrada.
     * No pide contraseña (es público por diseño) y cuenta como edición, así que viaja por la sync.
     */
    fun setVisibleName(id: String, title: String?) = launchIo {
        protection?.setProtectedTitle(id, title)
        refresh()
        syncEvent(SyncEvent.LocalChange)
        widgetChanged()
    }

    fun moveNote(id: String, before: String?, after: String?) = change { it.move(id, before, after) }

    /**
     * Escribe en [out] el zip con todas las notas activas y archivadas (ver `NotesExport`). Las protegidas no salen: su texto no está aquí en
     * claro. `null` si la base aún no está lista.
     */
    suspend fun exportAll(out: java.io.OutputStream, placeholder: String, omittedNote: (Int) -> String): app.aldune.domain.NotesExport.Result? =
        withContext(Dispatchers.IO) {
            val notes = io.withLock { service?.let { it.list(NoteState.ACTIVE) + it.list(NoteState.ARCHIVED) } } ?: return@withContext null
            app.aldune.domain.NotesExport.write(out, notes, placeholder, omittedNote)
        }

    fun setNoteColor(noteId: String, color: String) = change { it.setColor(noteId, color) }

    /**
     * Vuelve a colorear todas las notas activas con el tema, el tono y la regla actuales, en el orden del mazo y como si se crearan una tras
     * otra (`ApplyThemeToActiveNotes` de Windows): así «rotar sin repetir el vecino» no deja dos vecinas iguales. Archivadas y papelera no se
     * tocan. Cada cambio es un `setColor` normal y se sincroniza.
     */
    fun applyColorRuleToActive() = change { service ->
        val colors = newNoteColors()
        val assigned = ArrayList<String>()
        for (note in service.list(NoteState.ACTIVE)) {
            val color = NoteColorAssigner.assign(colors.theme, colors.tone, colors.rule, colors.fixedColor, assigned)
            assigned += color
            if (!color.equals(note.color, ignoreCase = true)) service.setColor(note.id, color)
        }
    }

    fun setNoteTags(noteId: String, tags: List<String>) = change { it.setTags(noteId, tags) }

    fun archive(id: String) = change { it.archive(id) }

    fun trash(id: String) = change { it.trash(id) }

    fun restore(id: String) = change { it.restore(id) }

    fun deleteForever(id: String) = change { it.deleteForever(id) }

    private fun newNoteColors() = NewNoteColorSettings(
        theme = NoteThemes.resolve(_settings.value.noteThemeId, null),
        tone = if (_settings.value.darkNotes) NoteTone.Dark else NoteTone.Light,
        rule = NoteColorAssignment.entries[_settings.value.colorRule],
        fixedColor = _settings.value.fixedNoteColor,
    )

    // ───────────── Recordatorios (F7) ─────────────

    /** El recordatorio de la nota, o `null`. */
    suspend fun reminderFor(noteId: String): Instant? = withContext(Dispatchers.IO) { ready?.reminders?.get(noteId)?.dueAt }

    fun setReminder(noteId: String, dueAt: Instant) = launchIo { ready?.reminders?.set(noteId, dueAt) }

    fun clearReminder(noteId: String) = launchIo { ready?.reminders?.clear(noteId) }

    /** Rehace las alarmas (por ejemplo, al cambiar «Hora exacta»). */
    fun reconcileReminders() = launchIo { ready?.reminders?.reconcile() }

    // Archivar, la papelera, borrar o restaurar cambian qué notas avisan: se reconcilian las alarmas (no en cada autoguardado: el texto no cuenta).
    private fun change(op: (NoteService) -> Any?) = launchIo {
        service?.let(op)
        ready?.reminders?.reconcile()
        refresh()
        syncEvent(SyncEvent.LocalChange)
        widgetChanged()
    }

    private fun update(transform: (UiState.Ready) -> UiState.Ready) {
        _state.update { (it as? UiState.Ready)?.let(transform) ?: it }
        launchIo { refresh() }
    }

    private fun launchIo(block: suspend () -> Unit) {
        viewModelScope.launch(Dispatchers.IO) { io.withLock { block() } }
    }

    private fun refresh() = refreshLocked()

    private fun refreshLocked() {
        val notes = service ?: return
        val current = _state.value as? UiState.Ready ?: return
        val order = NoteListOrder.fromCode(_settings.value.listOrder)
        val items = NoteListing.sort(notes.list(current.filter, current.query, current.tag), order).map { NoteListItem.from(it, placeholder, protectedTitle) }
        val counts = NoteState.entries.associateWith { notes.list(it).size }
        val tags = notes.list(current.filter).flatMap { it.tags }.distinctBy { it.lowercase() }.sortedBy { it.lowercase() }
        val orphans = ready?.conflicts?.orphans().orEmpty().mapNotNull { conflict ->
            val note = conflict.losing.note ?: return@mapNotNull null
            val title = if (note.isProtected) ProtectedTitleRules.visible(true, note.protectedTitle) ?: protectedTitle else NoteTitleHelper.getTitle(LineEnding.normalize(note.text), placeholder)
            OrphanConflict(conflict.noteId.toString(), title, note.isProtected)
        }
        val reminders = ready?.reminders?.all().orEmpty().associate { it.noteId to it.dueAt }
        _state.update { (it as? UiState.Ready)?.copy(items = items, allTags = tags, counts = counts, orphans = orphans, reminders = reminders) ?: it }
    }
}
