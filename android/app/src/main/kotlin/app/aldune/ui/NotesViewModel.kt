package app.aldune.ui

import android.app.Application
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import app.aldune.AldunaApp
import app.aldune.AppContainer
import app.aldune.capture.CaptureRequest
import app.aldune.data.AppSettings
import app.aldune.data.SettingsStore
import app.aldune.domain.LostReason
import app.aldune.domain.NewNoteColorSettings
import app.aldune.domain.NoteListItem
import app.aldune.domain.NoteResult
import app.aldune.domain.NoteService
import app.aldune.domain.SharedNote
import app.aldune.domain.SharedNoteText
import app.aldune.domain.NoteState
import app.aldune.syncclient.SyncEvent
import app.aldune.syncing.SyncController
import app.aldune.text.NoteTone
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
    ) : UiState
}

/**
 * Pega el dominio con la interfaz. Todo el trabajo con la base va en el hilo de E/S y de uno en uno (un cerrojo): las operaciones del
 * dominio ya son atómicas, pero el orden en que se aplican y se refresca la lista tiene que ser el que pidió el usuario.
 */
private const val AUTOSAVE_MS = 600L

class NotesViewModel(application: Application) : AndroidViewModel(application) {
    private val container = (application as AldunaApp).container
    private val io = Mutex()
    private val settingsStore = SettingsStore(application)
    private var service: NoteService? = null
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
                created?.let { SharedOutcome.Created(it.value.id) } ?: SharedOutcome.Empty
            }
        }
    }

    private val _state = MutableStateFlow<UiState>(UiState.Loading)
    val state: StateFlow<UiState> = _state.asStateFlow()

    private val _settings = MutableStateFlow(settingsStore.load())
    val settings: StateFlow<AppSettings> = _settings.asStateFlow()

    fun updateSettings(transform: (AppSettings) -> AppSettings) {
        val next = transform(_settings.value)
        _settings.value = next
        settingsStore.save(next)
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
        _sync.value = opened.sync.also { it.onNotesChanged = { launchIo { reconcileDrafts(); refreshLocked() } } }
        // Como Windows: la papelera se purga al abrir, con los días de los ajustes (30 por defecto).
        notes.purgeTrashed(_settings.value.trashDays)
        _state.value = UiState.Ready()
        refresh()
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
            text
        }
    }

    /** Se pidió una nota nueva (el «+»): se guarda lo que hubiera y se olvida el borrador anterior de nota nueva. */
    fun startNewNote() {
        flushDraft("")
        drafts.remove("")
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
        }
    }

    /** Color y etiquetas de una nota, para su menú. */
    class NoteOptions(val noteId: String, val color: String, val tags: List<String>)

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
            val id = drafts[key]?.noteId ?: return@withLock null
            service?.get(id)?.let { NoteOptions(id, it.color, it.tags) }
        }
    }

    fun moveNote(id: String, before: String?, after: String?) = change { it.move(id, before, after) }

    fun setNoteColor(noteId: String, color: String) = change { it.setColor(noteId, color) }

    fun setNoteTags(noteId: String, tags: List<String>) = change { it.setTags(noteId, tags) }

    fun archive(id: String) = change { it.archive(id) }

    fun trash(id: String) = change { it.trash(id) }

    fun restore(id: String) = change { it.restore(id) }

    fun deleteForever(id: String) = change { it.deleteForever(id) }

    private fun newNoteColors() = NewNoteColorSettings(tone = if (_settings.value.darkNotes) NoteTone.Dark else NoteTone.Light)

    private fun change(op: (NoteService) -> Any?) = launchIo {
        service?.let(op)
        refresh()
        syncEvent(SyncEvent.LocalChange)
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
        val items = notes.list(current.filter, current.query, current.tag).map { NoteListItem.from(it, placeholder, protectedTitle) }
        val counts = NoteState.entries.associateWith { notes.list(it).size }
        val tags = notes.list(current.filter).flatMap { it.tags }.distinctBy { it.lowercase() }.sortedBy { it.lowercase() }
        _state.update { (it as? UiState.Ready)?.copy(items = items, allTags = tags, counts = counts) ?: it }
    }
}
