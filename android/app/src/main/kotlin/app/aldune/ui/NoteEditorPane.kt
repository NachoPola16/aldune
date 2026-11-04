package app.aldune.ui

import android.app.Activity
import android.content.Context
import android.content.ContextWrapper
import android.widget.Toast
import androidx.compose.foundation.ExperimentalFoundationApi
import androidx.compose.foundation.gestures.awaitEachGesture
import androidx.compose.foundation.gestures.awaitFirstDown
import androidx.compose.foundation.gestures.waitForUpOrCancellation
import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.imePadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.WindowInsetsSides
import androidx.compose.foundation.layout.only
import androidx.compose.foundation.layout.safeDrawing
import androidx.compose.foundation.layout.safeDrawingPadding
import androidx.compose.foundation.layout.windowInsetsPadding
import androidx.compose.ui.platform.LocalConfiguration
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.text.BasicTextField
import androidx.compose.foundation.text.input.InputTransformation
import androidx.compose.foundation.text.input.OutputTransformation
import androidx.compose.foundation.text.input.delete
import androidx.compose.material3.AssistChip
import androidx.compose.material3.AssistChipDefaults
import androidx.compose.material3.LocalContentColor
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.runtime.DisposableEffect
import androidx.compose.ui.graphics.luminance
import androidx.compose.ui.platform.LocalView
import androidx.core.view.WindowCompat
import app.aldune.text.AppPalette
import app.aldune.text.AspectSkin
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.draw.drawBehind
import androidx.compose.ui.graphics.drawscope.DrawScope
import app.aldune.text.NoteChannels
import app.aldune.text.NoteDisplayColor
import androidx.compose.foundation.background
import androidx.compose.foundation.combinedClickable
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.layout.widthIn
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import app.aldune.data.CompletedTasksStore
import app.aldune.domain.CompletedTasksPolicy
import app.aldune.domain.KeyboardOnOpen
import app.aldune.text.MarkdownLive
import app.aldune.text.MarkdownStyle
import app.aldune.text.HiddenTasks
import app.aldune.text.LinkText
import androidx.compose.foundation.text.input.TextFieldState
import androidx.compose.runtime.snapshotFlow
import app.aldune.domain.SoundEvent
import app.aldune.domain.TypingThrottle
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.MoreVert
import androidx.compose.material3.DropdownMenu
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.derivedStateOf
import androidx.compose.runtime.getValue
import androidx.compose.runtime.produceState
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.rememberUpdatedState
import androidx.compose.runtime.withFrameNanos
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.SolidColor
import androidx.compose.ui.graphics.toArgb
import androidx.compose.foundation.text.input.TextFieldLineLimits
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.input.key.Key
import androidx.compose.ui.input.key.KeyEventType
import androidx.compose.ui.input.key.isAltPressed
import androidx.compose.ui.input.key.isCtrlPressed
import androidx.compose.ui.input.key.isShiftPressed
import androidx.compose.ui.input.key.key
import androidx.compose.ui.input.key.onPreviewKeyEvent
import androidx.compose.ui.input.key.type
import androidx.compose.ui.input.pointer.PointerEventPass
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.focus.focusProperties
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.focus.onFocusChanged
import androidx.compose.ui.hapticfeedback.HapticFeedbackType
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalHapticFeedback
import androidx.compose.ui.platform.LocalSoftwareKeyboardController
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.platform.LocalViewConfiguration
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.clearAndSetSemantics
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.semantics.CustomAccessibilityAction
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.customActions
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.SpanStyle
import androidx.compose.ui.text.style.TextIndent
import androidx.compose.ui.text.TextLayoutResult
import androidx.compose.ui.text.TextRange
import androidx.compose.ui.text.coerceIn
import androidx.compose.ui.text.font.Font
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.compose.LifecycleEventEffect
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import app.aldune.syncclient.SyncEvent
import app.aldune.R
import app.aldune.core.PasswordProblem
import app.aldune.syncclient.ProtectionError
import app.aldune.syncclient.ProtectionResult
import app.aldune.text.DiffLine
import app.aldune.text.BulletLines
import app.aldune.text.LineDirection
import app.aldune.text.LineMovement
import app.aldune.text.ListIndent
import app.aldune.text.ListKeys
import app.aldune.text.TaskDrag
import app.aldune.text.TaskLines
import app.aldune.text.TaskLists
import app.aldune.text.TextAndCaret
import app.aldune.text.TextSelection
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch

/** Espera antes de pedir el teclado en una nota nueva (ver el efecto de foco de [NoteEditorPane]). */
private const val KEYBOARD_DELAY_MS = 250L

/** Lo que el campo no puede enfocarse al abrir una nota que no abre el teclado: lo que tarda Android en repartir el foco inicial. */
private const val FOCUS_SETTLE_MS = 700L
// The square of a task is centred on the capitals of its text by a baseline shift, measured on screenshots (centre of the square minus centre of
// a capital, at 41 px of text): the large square sat 3.5 px high (lowered by 0.085 em), the normal one 1 px low (raised by 0.025 em).
private val CheckboxSpan = SpanStyle(fontFamily = FontFamily(Font(R.font.aldune_check)), baselineShift = androidx.compose.ui.text.style.BaselineShift(0.025f))
// The arrow of a bullet line in Roboto is a hairline at the height of the lowercase letters, below the middle of the capitals: heavier and a little raised it reads as a bullet centred on the text.
private val BulletSpan = SpanStyle(fontWeight = androidx.compose.ui.text.font.FontWeight.Bold, baselineShift = androidx.compose.ui.text.style.BaselineShift(0.16f))
private val LinkSpan = SpanStyle(textDecoration = androidx.compose.ui.text.style.TextDecoration.Underline)

// Live Markdown: how one stretch found by MarkdownLive is drawn. The syntax characters stay in the text, only dimmed.
private fun markdownSpanStyle(style: Int): SpanStyle? {
    if (style == 0) return null
    if (style and MarkdownStyle.MARKER != 0) return SpanStyle(color = androidx.compose.ui.graphics.Color(0xC0808080))   // a mid grey that reads on light and dark faces alike
    val decorations = buildList {
        if (style and MarkdownStyle.STRIKE != 0) add(androidx.compose.ui.text.style.TextDecoration.LineThrough)
        if (style and MarkdownStyle.LINK != 0) add(androidx.compose.ui.text.style.TextDecoration.Underline)
    }
    return SpanStyle(
        fontWeight = if (style and MarkdownStyle.BOLD != 0) androidx.compose.ui.text.font.FontWeight.Bold else null,
        fontStyle = if (style and MarkdownStyle.ITALIC != 0) androidx.compose.ui.text.font.FontStyle.Italic else null,
        fontFamily = if (style and MarkdownStyle.CODE != 0) androidx.compose.ui.text.font.FontFamily.Monospace else null,
        background = if (style and MarkdownStyle.CODE != 0) androidx.compose.ui.graphics.Color(0x24808080) else androidx.compose.ui.graphics.Color.Unspecified,
        textDecoration = if (decorations.isEmpty()) null else androidx.compose.ui.text.style.TextDecoration.combine(decorations),
    )
}

private val LargeCheckboxSpan = SpanStyle(fontFamily = FontFamily(Font(R.font.aldune_check_large)), baselineShift = androidx.compose.ui.text.style.BaselineShift(-0.085f))

/**
 * Editor de una nota: texto con ayudas de lista (casillas, viñetas, sangría) y guardado automático (lo hace el `ViewModel`, ver
 * `openDraft`). [key] es el id de la nota, o "" para una nota nueva.
 */
/**
 * Enter y Retroceso del teclado virtual. Se corrigen dentro de la propia edición del teclado (no después, en `onValueChange`): así la región
 * de composición de teclados como AnySoftKeyboard queda coherente con el texto que reescribimos, y no vuelve a escribir la palabra a medias.
 * Las ediciones hechas por código (barra, atajos) no pasan por aquí.
 */
private val ListKeysTransformation = InputTransformation {
    val old = TextSelection(originalText.toString(), originalSelection.min, originalSelection.max)
    val typed = TextAndCaret(asCharSequence().toString(), selection.start)
    if (old.text != typed.text) {
        ListKeys.resolve(old, typed)?.let {
            replace(0, length, it.text)
            selection = TextRange(it.caret)
        }
        // A box always keeps its space (a sign glued to it, pasted or synced, gets one)...
        val spaced = TaskLines.ensureGlyphSpace(asCharSequence().toString(), selection.start)
        if (spaced.text != asCharSequence().toString()) {
            replace(0, length, spaced.text)
            selection = TextRange(spaced.caret)
        }
    }
    // ...and the caret cannot rest between the box and that space: it jumps over it, or in front of the box when it came from the right.
    val shown = asCharSequence().toString()
    val movingLeft = old.text == shown && selection.start < originalSelection.start
    var start = TaskLines.clampCaret(shown, selection.start, movingLeft)
    // A lone caret also never rests in front of a box: its handle would cover the box and the tap that unticks it.
    if (selection.collapsed) start = TaskLines.keepCaretOffBox(shown, start, movingLeft)
    val end = if (selection.collapsed) start else TaskLines.clampCaret(shown, selection.end, movingLeft)
    if (start != selection.start || end != selection.end) selection = TextRange(start, end)
}

@OptIn(ExperimentalFoundationApi::class)
@Composable
fun NoteEditorPane(key: String, viewModel: NotesViewModel, onBack: () -> Unit, showBack: Boolean) {
    // Como en Windows (`NoteText`): el título (la primera línea) vive en su propio campo, en la cabecera, y `state` es solo el cuerpo. La nota
    // sigue siendo un único texto que se guarda de una pieza (ver `fullText`); lo que cambia es dónde se escribe cada parte.
    val state = remember(key) { TextFieldState() }
    val title = remember(key) { TextFieldState() }
    // Si hay salto tras el título. Una nota «título\n» (cuerpo vacío) no debe perder ese salto al abrirla, o abrir y cerrar la reescribiría.
    var breakAfterTitle by remember(key) { mutableStateOf(false) }
    val titleFocus = remember { FocusRequester() }
    // Undo/redo act on the field being edited: the title has its own history, separate from the body's.
    var titleFocused by remember { mutableStateOf(false) }
    fun fullText(): String {
        val body = state.text.toString()
        return if (body.isNotEmpty() || breakAfterTitle) title.text.toString() + "\n" + body else title.text.toString()
    }
    fun setFullText(full: String, bodyCaret: Int = 0) {
        val cut = full.indexOf('\n')
        val head = if (cut < 0) full else full.substring(0, cut)
        val body = if (cut < 0) "" else full.substring(cut + 1)
        breakAfterTitle = cut >= 0
        title.edit { replace(0, length, head); selection = TextRange(head.length) }
        state.edit { replace(0, length, body); selection = TextRange(bodyCaret.coerceIn(0, body.length)) }
    }
    var loaded by remember(key) { mutableStateOf(false) }
    // Una protegida solo se lee y se edita con su contraseña: hasta entonces se ve un aviso con el botón de abrirla. `unlocked` dice que hay una
    // sesión abierta en el ViewModel; el texto en claro está solo en este campo y en esa sesión (nunca en un borrador ni en estado guardado).
    var protectedNote by remember(key) { mutableStateOf(false) }
    var unlocked by remember(key) { mutableStateOf(false) }
    var passwordMode by remember(key) { mutableStateOf<PasswordMode?>(null) }
    val context = LocalContext.current
    var options by remember(key) { mutableStateOf<NotesViewModel.NoteOptions?>(null) }
    var topMenu by remember(key) { mutableStateOf(false) }
    val scope = rememberCoroutineScope()
    var layout by remember(key) { mutableStateOf<(() -> TextLayoutResult?)?>(null) }
    // The hanging indent is one setting for the whole field (a paragraph style per task line adds a blank line at every break), so it would also
    // indent the wrapped lines of a plain paragraph. It is dropped while any plain line wraps: the first visual line does not depend on the indent,
    // so this cannot flip back and forth.
    var plainWraps by remember(key) { mutableStateOf(false) }
    // Toque pendiente sobre el texto. No se aplica en el mismo evento: el campo, al recibir ese mismo toque, emite el texto
    // anterior con el cursor nuevo y pisaría el cambio; se espera al siguiente fotograma (ver LaunchedEffect de abajo).
    var tap by remember(key) { mutableStateOf<Offset?>(null) }
    // Arrastrar tareas con el dedo (ver TaskDragUi): el campo comparte su scroll con el dibujo del asa.
    val fieldScroll = rememberScrollState()
    val drag = remember(key) { TaskDragState() }

    // Carga la nota (otra vez, tras resolver un conflicto que le cambió el texto o la protección). Mientras dura, el texto no se guarda.
    suspend fun load() {
        loaded = false
        val text = viewModel.openDraft(key)
        protectedNote = viewModel.isProtected(key)
        if (!protectedNote) {
            setFullText(text)
        } else {
            // Al rotar, la sesión sigue abierta en el ViewModel: se recarga sin volver a pedir la contraseña.
            val plain = viewModel.protectedText(key)
            if (plain != null) {
                setFullText(plain)
                unlocked = true
            } else {
                passwordMode = PasswordMode.Unlock
            }
        }
        // Lo recién cargado no se puede deshacer: el primer «deshacer» dejaría la nota vacía.
        state.undoState.clearHistory()
        title.undoState.clearHistory()
        loaded = true
        viewModel.playSound(SoundEvent.NOTE_OPENED)
    }
    LaunchedEffect(key) { load() }
    // Abrir una nota trae antes lo que se haya escrito en otro dispositivo (la política limita a una pasada cada 30 s): si llega algo y no se
    // ha tocado, `externalEdits` recarga el texto. No se bloquea la edición: sin red no se podría escribir, y los conflictos ya se guardan.
    LaunchedEffect(key, loaded) { if (loaded && key.isNotEmpty()) viewModel.syncEvent(SyncEvent.NoteOpened) }
    // Una pasada de sync cambió el texto de esta nota mientras estaba abierta (y no se había tocado): se recarga, o la siguiente tecla guardaría
    // el texto viejo encima del recién llegado.
    LaunchedEffect(key) {
        viewModel.externalEdits.collect { changed ->
            if (changed != key) return@collect
            val fresh = viewModel.draftText(key)
            if (fresh == fullText()) return@collect
            setFullText(fresh, state.selection.min)
            state.undoState.clearHistory()
            title.undoState.clearHistory()
        }
    }
    LaunchedEffect(key) {
        viewModel.deletedNotes.collect { gone -> if (gone == key) onBack() }
    }
    // Conflicto de sync de esta nota: la franja con la otra versión. Se vuelve a leer al cargar y cuando la sync lo añade o lo quita.
    var conflict by remember(key) { mutableStateOf<NotesViewModel.ConflictView?>(null) }
    var otherLines by remember(key) { mutableStateOf<List<DiffLine>?>(null) }
    var unlockOther by remember(key) { mutableStateOf(false) }
    val syncController by viewModel.sync.collectAsStateWithLifecycle()
    val syncing = syncController?.status?.collectAsStateWithLifecycle()?.value?.running == true
    val conflictedIds = syncController?.conflicted?.collectAsStateWithLifecycle()?.value ?: emptySet()
    val hasConflict = key in conflictedIds
    LaunchedEffect(key, loaded, hasConflict) {
        if (!loaded) return@LaunchedEffect
        conflict = if (hasConflict) viewModel.conflictFor(key) else null
        otherLines = null
    }

    fun resolveConflict(useOther: Boolean) {
        val current = conflict ?: return
        scope.launch {
            if (!viewModel.resolveConflict(key, useOther)) {
                Toast.makeText(context, R.string.conflict_failed, Toast.LENGTH_LONG).show()
                return@launch
            }
            conflict = null
            if (!useOther) return@launch
            // Era un borrado: la nota ya no existe.
            if (current.other is NotesViewModel.OtherVersion.Deleted) {
                onBack()
                return@launch
            }
            // La nota cambió de texto (y quizá de protección): se suelta la sesión vieja y se vuelve a cargar. `unlocked` baja antes, o el cierre
            // de la sesión se leería como un bloqueo y se pediría una contraseña que ya no hace falta.
            unlocked = false
            viewModel.dropProtected(key)
            load()
        }
    }

    // Único punto por el que el texto llega al guardado: así ningún camino (teclado, barra, atajo, toque) se lo salta.
    LaunchedEffect(key) {
        var previous = fullText()
        snapshotFlow { fullText() }.collect {
            if (!loaded) { previous = it; return@collect }
            if (TypingThrottle.isTyping(previous, it)) viewModel.playSound(SoundEvent.TYPING)
            // Una nota protegida se vacía al bloquearse: eso no es borrar, y `unlocked` ya está bajado cuando ocurre.
            else if (TypingThrottle.isDeleting(previous, it) && (!protectedNote || unlocked)) viewModel.playSound(SoundEvent.TEXT_DELETED)
            previous = it
            // El texto de una protegida abierta no pasa por los borradores: va a su sesión. Bloqueada, no se guarda nada.
            if (!protectedNote) viewModel.updateDraft(key, it) else if (unlocked) viewModel.updateProtectedText(key, it)
        }
    }
    // La sesión se cerró (segundo plano, o no se pudo guardar): el texto en claro se borra del campo ya, y se vuelve a pedir la contraseña.
    // `unlocked` se baja antes de vaciar el campo, o ese vaciado se guardaría como el texto de la nota.
    LaunchedEffect(key) {
        viewModel.unlockedIds.collect { ids ->
            if (protectedNote && unlocked && key !in ids) {
                unlocked = false
                setFullText("")
                state.undoState.clearHistory()
                title.undoState.clearHistory()
                passwordMode = PasswordMode.Unlock
            }
        }
    }
    LaunchedEffect(key) {
        viewModel.protectionLost.collect { lost ->
            if (lost == key) Toast.makeText(context, R.string.protection_stale, Toast.LENGTH_LONG).show()
        }
    }
    // Al salir de la nota (otra nota, atrás, el panel se destruye) y al pasar la app a segundo plano. Rotar no es salir: la sesión sigue
    // en el ViewModel y el editor nuevo la recoge.
    DisposableEffect(key) {
        onDispose {
            viewModel.closeNote(key)
            if (context.findActivity()?.isChangingConfigurations != true) viewModel.lockProtected(key)
        }
    }
    LifecycleEventEffect(Lifecycle.Event.ON_STOP) { viewModel.flushAll() }

    val toggleTaskLabel = stringResource(R.string.edit_toggle_task)

    fun text() = state.text.toString()

    fun commit(text: String, selection: TextRange) {
        state.edit {
            // Only the stretch that changes is replaced: replacing the whole text (as this used to) rebuilds the layout from nothing, and the
            // field scrolled to wherever it ended up, usually back up to the caret.
            val old = asCharSequence().toString()
            val max = minOf(old.length, text.length)
            var prefix = 0
            while (prefix < max && old[prefix] == text[prefix]) prefix++
            var suffix = 0
            while (suffix < max - prefix && old[old.length - 1 - suffix] == text[text.length - 1 - suffix]) suffix++
            // Never between the two halves of an emoji.
            if (prefix > 0 && old[prefix - 1].isHighSurrogate()) prefix--
            if (suffix > 0 && old[old.length - suffix].isLowSurrogate()) suffix--
            if (old != text) replace(prefix, old.length - suffix, text.substring(prefix, text.length - suffix))
            this.selection = selection.coerceIn(0, text.length)
        }
    }

    // The checkbox and bullet buttons of the bar change the line that holds the caret and nothing else: the text stays where the reader had
    // it, even if the caret is somewhere else on the note (the field scrolls to the caret after an edit, and used to take the reader there).
    fun commitKeepingScroll(edit: TextAndCaret) {
        val before = fieldScroll.value
        commit(edit.text, TextRange(edit.caret))
        scope.launch {
            withFrameNanos { }
            withFrameNanos { }
            if (fieldScroll.value != before) fieldScroll.scrollTo(before.coerceAtMost(fieldScroll.maxValue))
        }
    }

    fun commit(edit: TextAndCaret) = commit(edit.text, TextRange(edit.caret))

    fun caret() = state.selection.min

    // Con una selección, la sangría afecta a todas las líneas que toca y la selección se conserva.
    fun shift(lines: (String, Int, Int) -> app.aldune.text.SelectionEdit, single: (String, Int) -> TextAndCaret) {
        val s = state.selection
        if (s.collapsed) return commit(single(text(), s.start))
        val e = lines(text(), s.min, s.length)
        commit(e.text, TextRange(e.selectionStart, e.selectionStart + e.selectionLength))
    }

    fun indent() = shift(ListIndent::indentLines, ListIndent::indent)

    fun outdent() = shift(ListIndent::outdentLines, ListIndent::outdent)

    fun move(direction: LineDirection) {
        LineMovement.move(text(), caret(), direction)?.let { commit(it) }
    }

    val slack = with(LocalDensity.current) { 12.dp.toPx() }

    val haptic = LocalHapticFeedback.current
    val settings by viewModel.settings.collectAsStateWithLifecycle()
    // «Tareas hechas» por nota (0 según mis ajustes, 1 se quedan, 2 bajan al final): manda sobre los ajustes del dispositivo y las dos últimas nunca ocultan.
    var doneTasksPolicy by remember(key) { mutableIntStateOf(0) }
    LaunchedEffect(key, loaded) { if (loaded) doneTasksPolicy = viewModel.completedTasksPolicy(key) }
    val settleChecked = CompletedTasksPolicy.shouldMoveToEnd(doneTasksPolicy, settings.reorderOnCheck)

    // «Ocultar tareas hechas»: el texto real no cambia; el campo dibuja el texto sin esas líneas (OutputTransformation) y los toques se
    // traducen de vuelta al texto real. Una protegida no apunta nada: no se guarda ni un rastro de su contenido fuera de ella.
    val hideActive = CompletedTasksPolicy.mayHide(doneTasksPolicy, settings.hideDoneTasks) && !protectedNote
    val hideDelayMs = settings.hideDoneAfterHours * 3_600_000L
    val completedStore = remember { CompletedTasksStore(context) }
    var completed by remember(key) { mutableStateOf<Map<String, Long>>(emptyMap()) }
    var showDone by remember(key) { mutableStateOf(false) }
    LaunchedEffect(key, hideActive, loaded) {
        if (!hideActive || !loaded) return@LaunchedEffect
        snapshotFlow { state.text.toString() }.collect { current ->
            val stored = completedStore.load(key)
            val tracked = HiddenTasks.track(current, stored, System.currentTimeMillis())
            if (tracked != stored) completedStore.save(key, tracked)
            if (tracked != completed) completed = tracked
        }
    }
    val now = System.currentTimeMillis()
    val hiddenCandidates = if (hideActive) HiddenTasks.hiddenSpans(state.text.toString(), completed, now, hideDelayMs) else emptyList()
    val hiddenSpans = if (showDone) emptyList() else hiddenCandidates
    // Las casillas se dibujan con AlduneCheck (☒ con tick, sin cambiar lo guardado); el resto del texto no cambia de fuente.
    val checkboxSpan = if (settings.largeCheckboxes) LargeCheckboxSpan else CheckboxSpan
    // Hanging indent: the continuation lines of a long line start under the task text, not under the checkbox. One indent for the whole field,
    // the width of "checkbox + space" measured in the font the field draws; Android applies it to each hard line. (A paragraph style per task
    // line was tried: a line feed outside the paragraph needs a paragraph of its own and the cursor at the end of a line then lands in it.)
    val measurer = androidx.compose.ui.text.rememberTextMeasurer()
    val bodyStyle = MaterialTheme.typography.bodyLarge
    val density = LocalDensity.current
    val taskIndent = remember(checkboxSpan, bodyStyle, density, measurer) {
        val styled = androidx.compose.ui.text.buildAnnotatedString {
            append(TaskLines.PREFIX)
            addStyle(checkboxSpan, 0, 1)
        }
        with(density) { measurer.measure(styled, bodyStyle).size.width.toSp() }
    }
    val liveMarkdown = settings.liveMarkdown
    val hideTransformation = remember(hideActive, showDone, completed, hideDelayMs, checkboxSpan, liveMarkdown) {
        OutputTransformation {
            if (hideActive && !showDone) {
                val spans = HiddenTasks.hiddenSpans(asCharSequence().toString(), completed, System.currentTimeMillis(), hideDelayMs)
                for (span in spans.asReversed()) delete(span.start, span.end)
            }
            // Se recorre una copia en String, no el buffer del campo: indexar `asCharSequence()` carácter a carácter es lo que más pesaba en una
            // nota larga con una tecla repetida (borrar mantenido), porque esto corre entero en cada cambio de texto.
            val shown = asCharSequence().toString()
            for (i in shown.indices) {
                val c = shown[i]
                if (c == TaskLines.UNCHECKED || c == TaskLines.CHECKED || c == TaskLines.CHECKED_ALTERNATE) addStyle(checkboxSpan, i, i + 1)
                else if (c == BulletLines.GLYPH && i + 1 < shown.length && shown[i + 1] == ' ') {
                    // Only the arrow that opens a line (after its indent), not one written in the middle of a sentence.
                    var back = i - 1
                    while (back >= 0 && (shown[back] == ' ' || shown[back] == '\t')) back--
                    if (back < 0 || shown[back] == '\n') addStyle(BulletSpan, i, i + 1)
                }
            }
            // Web addresses are underlined (the text itself never changes); they open from the chip below, never by themselves.
            for (link in LinkText.findAll(shown)) addStyle(LinkSpan, link.start, link.end)
            // Markdown as it is typed: styles only, the text is not touched (so search, sync and export see what was typed).
            if (liveMarkdown && shown.length <= MarkdownLive.MAX_NOTE_LENGTH) {
                for (span in MarkdownLive.parse(shown)) markdownSpanStyle(span.style)?.let { addStyle(it, span.start, span.end) }
            }
        }
    }

    /**
     * Marca o desmarca la casilla de [glyph] y, con el ajuste «mover abajo las tareas hechas», la recoloca en su lista. Al tocar, el cursor
     * sigue a su línea; con Ctrl+Enter se queda en el mismo sitio de la pantalla, como en Windows, para poder marcar una lista seguida.
     */
    fun toggleTask(glyph: Int, keepCaretPosition: Boolean) {
        val toggled = TaskLines.toggleCheckboxAt(text(), glyph) ?: return
        haptic.performHapticFeedback(HapticFeedbackType.TextHandleMove)
        viewModel.playSound(SoundEvent.TASK_CHECKED)
        val sel = state.selection
        if (!settleChecked) return commit(toggled, sel)
        val edit = TaskLists.settleToggled(toggled, glyph)
        val moved = if (keepCaretPosition) sel else TextRange(edit.mapIndex(sel.start), edit.mapIndex(sel.end))
        commit(edit.text, moved)
    }

    // Una nota nueva (el «+», el acceso directo, el mosaico, la notificación) es para escribir ya: se pide el foco y el teclado. Sin esto,
    // el teclado salía o no según una carrera con el foco de la ventana (en frío no, con la app ya abierta sí). Una nota existente no lo abre:
    // al leerla no hay que tapar la mitad del texto. Ajustes → Notas → «Teclado al abrir una nota» lo cambia (siempre o nunca).
    val focus = remember { FocusRequester() }
    val keyboard = LocalSoftwareKeyboardController.current
    // Cuando no toca abrir el teclado (por defecto, «nunca»), el campo no puede recibir el foco mientras la nota arranca: Android se lo da solo
    // al aparecer, y con el foco sale el teclado. Quitárselo después lo dejaba asomar y esconderse. Pasado un instante, o en cuanto se toca el
    // texto, ya puede enfocarse (TalkBack y el teclado físico no se quedan sin poder entrar).
    val wantsKeyboard = KeyboardOnOpen.shouldShow(settings.keyboardOnOpen, isNewNote = key.isEmpty(), isProtected = protectedNote)
    var touched by remember(key) { mutableStateOf(false) }
    var settling by remember(key) { mutableStateOf(true) }
    LaunchedEffect(key) { delay(FOCUS_SETTLE_MS); settling = false }
    val canFocus = wantsKeyboard || touched || !settling
    LaunchedEffect(key, loaded, protectedNote) {
        if (loaded && KeyboardOnOpen.shouldShow(settings.keyboardOnOpen, isNewNote = key.isEmpty(), isProtected = protectedNote)) {
            (if (title.text.isEmpty() && state.text.isEmpty()) titleFocus else focus).requestFocus()
            // `stateHidden` de la ventana oculta el teclado al arrancar y se come un show() demasiado temprano: se espera a que la ventana se
            // asiente. (Con la app ya abierta no hace falta, pero no estorba.)
            delay(KEYBOARD_DELAY_MS)
            keyboard?.show()
        }
    }

    LaunchedEffect(tap) {
        val position = tap ?: return@LaunchedEffect
        withFrameNanos { }
        // El diseño es del texto que se ve; con tareas ocultas hay que buscar la casilla ahí y luego pasar su posición al texto real.
        val shown = StringBuilder(text()).also { sb -> for (span in hiddenSpans.asReversed()) sb.delete(span.start, span.end) }.toString()
        toggleCheckboxAtTap(shown, layout?.invoke(), position, slack)?.let { toggleTask(HiddenTasks.toRaw(it, hiddenSpans), keepCaretPosition = false) }
        tap = null
    }

    val touchSlop = LocalViewConfiguration.current.touchSlop
    val longPress = LocalViewConfiguration.current.longPressTimeoutMillis

    // Igual que la lista: con «color como fondo» la nota se ve del color de la nota; en Telecom, del de su canal (con la retícula en el oscuro).
    // Los colores van por la tinta y no por un tema local: así los diálogos, que llevan su propia superficie, no se desajustan.
    val look = LocalNoteLook.current
    val listState by viewModel.state.collectAsStateWithLifecycle()
    val shownItems = (listState as? UiState.Ready)?.items.orEmpty()
    val shownItem = shownItems.firstOrNull { it.id == key }
    // «Editor con los colores de la app»: the note keeps its colour in the list, but the page it is written on is the app's own.
    val plainEditor = settings.plainEditor
    val tint = look.telecom?.takeIf { !look.fillColor && shownItem != null && !plainEditor }
        ?.let { NoteChannels.tint(shownItems.indexOf(shownItem), it, look.uniformColor) }
    // A new note is not in the list until its first character is typed: until then it shows the colour it will be created with.
    var newNoteColor by remember(key) { mutableStateOf(if (key.isEmpty()) viewModel.cachedPreviewColor() else null) }
    LaunchedEffect(key) { if (key.isEmpty() && newNoteColor == null) newNoteColor = viewModel.previewNewNoteColor() }
    val faceHex = tint?.face ?: (shownItem?.color ?: newNoteColor)?.takeIf { look.fillColor && !plainEditor }?.let { NoteDisplayColor.resolve(it, look.uniformColor) }
    val themeInk = MaterialTheme.colorScheme.onBackground
    val ink = if (faceHex == null || tint != null) themeInk else noteInk(faceHex)
    val muted = if (faceHex == null) MaterialTheme.colorScheme.onSurfaceVariant else ink.copy(alpha = 0.75f)
    val gridInk = ink.copy(alpha = 0.15f)

    // Los iconos de la barra de estado y de navegación siguen la claridad de la hoja que se ve, no la del aspecto: unos iconos blancos sobre
    // una nota amarilla (o negros sobre una azul oscura) no se leen. Al salir del editor vuelven a los del aspecto.
    val view = LocalView.current
    val appLight = AppPalette.isLight(settings.appearance, !isSystemInDarkTheme())
    val faceLight = faceHex?.let { noteFace(it).luminance() > 0.5f } ?: appLight
    DisposableEffect(faceLight, appLight) {
        val window = context.findActivity()?.window
        val controller = window?.let { WindowCompat.getInsetsController(it, view) }
        controller?.isAppearanceLightStatusBars = faceLight
        controller?.isAppearanceLightNavigationBars = faceLight
        onDispose {
            controller?.isAppearanceLightStatusBars = appLight
            controller?.isAppearanceLightNavigationBars = appLight
        }
    }

    // A phone on its side: the keyboard would leave the text a sliver between a title row and a button bar.
    // Only a real landscape counts: a vertical split screen or a floating window is also short but narrow, and moving the bar into the header
    // there squashes it (the narrow case keeps the bar on its own row below the text).
    val editedUndo = if (titleFocused) title.undoState else state.undoState
    val configuration = LocalConfiguration.current
    val compactHeight = configuration.screenHeightDp < 480 && configuration.screenWidthDp > configuration.screenHeightDp
    // On its side the status bar costs a tenth of the height; it goes while a note is open and comes back with a swipe from the edge.
    DisposableEffect(compactHeight) {
        val controller = context.findActivity()?.window?.let { WindowCompat.getInsetsController(it, view) }
        if (compactHeight) {
            controller?.systemBarsBehavior = androidx.core.view.WindowInsetsControllerCompat.BEHAVIOR_SHOW_TRANSIENT_BARS_BY_SWIPE
            controller?.hide(androidx.core.view.WindowInsetsCompat.Type.statusBars())
        }
        onDispose { controller?.show(androidx.core.view.WindowInsetsCompat.Type.statusBars()) }
    }
    CompositionLocalProvider(LocalContentColor provides ink) {
    Column(
        Modifier.fillMaxSize()
            .then(if (faceHex != null) Modifier.background(noteFace(faceHex)) else Modifier)
            // Los pliegues cubren toda la hoja; los renglones van aparte, dentro del campo de texto, para alinearlos con sus líneas.
            .paperTexture(if (look.paper == AspectSkin.Paper.FOLDS) look.paper else AspectSkin.Paper.NONE, ink, faceLight)
            // Beside the list (showBack is false) this pane does not touch the screen's left edge: the camera cut-out margin belongs to the list.
            .then(
                if (showBack) Modifier.safeDrawingPadding()
                else Modifier.windowInsetsPadding(WindowInsets.safeDrawing.only(WindowInsetsSides.Top + WindowInsetsSides.End + WindowInsetsSides.Bottom)),
            )
            .imePadding(),
    ) {
        Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
            if (showBack) {
                // The arrow itself (the drawn strokes sit 4 dp inside the 24 dp icon, which is centred in the 48 dp button) starts where the text of the
                // note starts, 20 dp from the edge: the button is moved 4 dp in, and the title follows it directly.
                IconButton(onClick = { viewModel.playSound(SoundEvent.NOTE_CLOSED); onBack() }, Modifier.padding(start = 4.dp)) { Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = stringResource(R.string.back)) }
            }
            // El título, junto a la flecha, como la cabecera de la ventana de nota de Windows. Intro pasa al cuerpo. Una protegida cerrada no enseña nada.
            val titleEnabled = loaded && (!protectedNote || unlocked)
            BasicTextField(
                state = title, enabled = titleEnabled, lineLimits = TextFieldLineLimits.SingleLine,
                modifier = Modifier.weight(1f).padding(start = if (showBack) 0.dp else TASK_DRAG_PAD_START, end = 4.dp).focusProperties { this.canFocus = canFocus }.focusRequester(titleFocus).onFocusChanged { titleFocused = it.isFocused },
                textStyle = MaterialTheme.typography.titleMedium.copy(color = ink),
                cursorBrush = SolidColor(ink),
                keyboardOptions = KeyboardOptions(imeAction = ImeAction.Next),
                onKeyboardAction = { focus.requestFocus() },
                decorator = { inner ->
                    Box(contentAlignment = Alignment.CenterStart) {
                        if (title.text.isEmpty() && titleEnabled) Text(stringResource(R.string.new_note_title), style = MaterialTheme.typography.titleMedium, color = muted)
                        inner()
                    }
                },
            )
            if (syncing) {
                Text(
                    stringResource(R.string.sync_status_syncing), Modifier.padding(end = 12.dp),
                    style = MaterialTheme.typography.labelMedium, color = muted,
                )
            }
            // Cuántas tareas van hechas: se ve sin recorrer la lista. Solo si la nota tiene tareas.
            val progress = TaskLines.count(state.text.toString())
            if (progress.total > 0 && !protectedNote) {
                val label = stringResource(R.string.tasks_progress_cd, progress.done, progress.total)
                Text(
                    "${progress.done}/${progress.total}", Modifier.semantics { contentDescription = label },
                    style = MaterialTheme.typography.labelLarge, color = muted,
                )
            }
            // A short screen (a phone on its side) has no room for a bar of its own under the text, least of all with the keyboard up: the
            // editing buttons move into this row, so the text keeps the height.
            if (compactHeight) {
                EditBar(
                    onCheckbox = { commitKeepingScroll(TaskLines.toggleTaskLineAt(text(), caret())) },
                    onBullet = { commitKeepingScroll(BulletLines.toggleBulletLineAt(text(), caret())) },
                    onOutdent = ::outdent,
                    onIndent = ::indent,
                    canUndo = editedUndo.canUndo, canRedo = editedUndo.canRedo,
                    onUndo = { editedUndo.undo() }, onRedo = { editedUndo.redo() },
                    inline = true,
                )
            }
            // El único menú «más» del editor: antes había dos (este y un «⋯» abajo) y no se sabía cuál tenía qué.
            Box {
                IconButton(onClick = { topMenu = true }, enabled = loaded) {
                    Icon(Icons.Default.MoreVert, contentDescription = stringResource(R.string.edit_more))
                }
                // Offset left so the menu does not touch the screen edge (it opened flush against it, with its longest entries clipped by the margin).
                DropdownMenu(expanded = topMenu, onDismissRequest = { topMenu = false }, offset = androidx.compose.ui.unit.DpOffset((-12).dp, 0.dp)) {
                    DropdownMenuItem(
                        text = { Text(stringResource(R.string.note_options)) },
                        onClick = { topMenu = false; scope.launch { options = viewModel.noteOptions(key) } },
                    )
                    // Lo que se hace con la nota entera (antes solo desde la lista): fijarla, archivarla o tirarla sin salir a deslizar la fila.
                    // Una nota nueva sin texto aún no existe (noteOptions devuelve null): no hay nada que fijar ni archivar.
                    val pinnedNow by produceState(false, topMenu) { value = if (topMenu) viewModel.noteOptions(key)?.pinnedToTop ?: false else false }
                    DropdownMenuItem(
                        text = { Text(stringResource(if (pinnedNow) R.string.note_unpin else R.string.note_pin)) },
                        onClick = { topMenu = false; scope.launch { viewModel.noteOptions(key)?.let { viewModel.setPinned(it.noteId, !pinnedNow) } } },
                    )
                    // Un elemento que cicla, sin avisos: el propio texto dice el estado. El menú se cierra al pulsarlo, como los demás.
                    DropdownMenuItem(
                        text = { Text(stringResource(doneTasksPolicyLabel(doneTasksPolicy))) },
                        onClick = {
                            topMenu = false
                            scope.launch {
                                viewModel.noteOptions(key)?.let {
                                    val next = CompletedTasksPolicy.next(doneTasksPolicy)
                                    doneTasksPolicy = next
                                    viewModel.setCompletedTasks(it.noteId, next)
                                }
                            }
                        },
                    )
                    DropdownMenuItem(
                        text = { Text(stringResource(R.string.archive)) },
                        onClick = { topMenu = false; scope.launch { viewModel.noteOptions(key)?.let { viewModel.archive(it.noteId); onBack() } } },
                    )
                    DropdownMenuItem(
                        text = { Text(stringResource(R.string.move_to_trash)) },
                        onClick = { topMenu = false; scope.launch { viewModel.noteOptions(key)?.let { viewModel.trash(it.noteId); onBack() } } },
                    )
                    if (!protectedNote || unlocked) {
                        HorizontalDivider()
                        DropdownMenuItem(text = { Text(stringResource(R.string.edit_move_up)) }, onClick = { topMenu = false; move(LineDirection.Up) })
                        DropdownMenuItem(text = { Text(stringResource(R.string.edit_move_down)) }, onClick = { topMenu = false; move(LineDirection.Down) })
                        DropdownMenuItem(text = { Text(stringResource(R.string.edit_outdent)) }, onClick = { topMenu = false; outdent() })
                        // Solo en notas con tareas: en una nota de texto era una entrada gris que nunca se podía usar.
                        if (TaskLines.count(text()).total > 0) {
                            DropdownMenuItem(
                                text = { Text(stringResource(R.string.edit_uncheck_all)) },
                                enabled = TaskLists.hasChecked(text()),
                                onClick = { topMenu = false; commit(TaskLists.uncheckAll(text()).text, state.selection) },
                            )
                        }
                    }
                }
            }
        }
        options?.let { current ->
            NoteOptionsDialog(
                current, viewModel, onClose = { options = null },
                // Lo que se ve ahora en el editor (como en Windows). Una protegida cerrada no tiene texto que exportar.
                exportText = if (!current.isProtected || unlocked) fullText() else null,
                onRestored = { setFullText(it) },
                onProtection = {
                    options = null
                    passwordMode = if (current.isProtected) PasswordMode.Remove else PasswordMode.Protect
                },
            )
        }
        passwordMode?.let { mode ->
            PasswordDialog(mode, onDismiss = { passwordMode = null }) { password, confirmation, visibleName ->
                when (mode) {
                    PasswordMode.Unlock -> when (val outcome = viewModel.unlockProtected(key, password)) {
                        is NotesViewModel.UnlockOutcome.Opened -> {
                            setFullText(outcome.text)
                            unlocked = true
                            null
                        }
                        NotesViewModel.UnlockOutcome.WrongPassword -> PasswordFailure.Wrong
                        NotesViewModel.UnlockOutcome.Unreadable -> PasswordFailure.Unreadable
                        NotesViewModel.UnlockOutcome.Unsupported -> PasswordFailure.Unsupported
                        NotesViewModel.UnlockOutcome.Gone -> PasswordFailure.Changed
                    }
                    PasswordMode.Protect -> when (val result = viewModel.protectNote(key, password, confirmation, visibleName)) {
                        is ProtectionResult.Ok -> {
                            // Queda abierta, como en Windows: se abre la sesión con la misma contraseña (derivar otra vez).
                            when (val outcome = viewModel.unlockProtected(key, password)) {
                                is NotesViewModel.UnlockOutcome.Opened -> {
                                    setFullText(outcome.text)
                                    unlocked = true
                                    protectedNote = true
                                }
                                else -> protectedNote = true
                            }
                            null
                        }
                        is ProtectionResult.Err -> when (val error = result.error) {
                            is ProtectionError.InvalidPassword -> when (error.problem) {
                                PasswordProblem.Empty -> PasswordFailure.Empty
                                PasswordProblem.TooShort -> PasswordFailure.TooShort
                                PasswordProblem.Mismatch -> PasswordFailure.Mismatch
                                PasswordProblem.None -> PasswordFailure.Changed
                            }
                            else -> PasswordFailure.Changed
                        }
                    }
                    PasswordMode.Remove -> when (val result = viewModel.removeProtection(key, password)) {
                        is ProtectionResult.Ok -> {
                            // El texto vuelve a ser el normal: pasa a ser una nota corriente con su borrador, y la sesión se suelta.
                            protectedNote = false
                            val plain = viewModel.openDraft(key)
                            setFullText(plain)
                            viewModel.dropProtected(key)
                            null
                        }
                        is ProtectionResult.Err -> when (result.error) {
                            ProtectionError.WrongPassword -> PasswordFailure.Wrong
                            is ProtectionError.UnreadableContent -> PasswordFailure.Unreadable
                            is ProtectionError.UnsupportedVersion -> PasswordFailure.Unsupported
                            else -> PasswordFailure.Changed
                        }
                    }
                }
            }
        }
        conflict?.let { view ->
            ConflictStrip(
                view, otherLines,
                onKeep = { resolveConflict(false) }, onUseOther = { resolveConflict(true) }, onUnlockOther = { unlockOther = true },
            )
        }
        if (unlockOther) {
            PasswordDialog(PasswordMode.Unlock, onDismiss = { unlockOther = false }) { password, _, _ ->
                val lines = viewModel.unlockOtherVersion(key, password)
                if (lines == null) {
                    PasswordFailure.Wrong
                } else {
                    otherLines = lines
                    null
                }
            }
        }
        if (protectedNote && !unlocked) {
            Column(
                Modifier.weight(1f).fillMaxWidth().padding(24.dp),
                verticalArrangement = Arrangement.Center, horizontalAlignment = Alignment.CenterHorizontally,
            ) {
                Text(stringResource(R.string.protected_note_body), color = muted)
                Button(onClick = { passwordMode = PasswordMode.Unlock }, Modifier.padding(top = 16.dp)) {
                    Text(stringResource(R.string.password_unlock_action))
                }
            }
            return@Column
        }
        // El texto que se ve (sin las tareas hechas ocultas): el diseño del campo es de este texto, no del real.
        fun shownText(): String = StringBuilder(text()).also { sb -> for (span in hiddenSpans.asReversed()) sb.delete(span.start, span.end) }.toString()
        // La posición (en el texto real) de la casilla bajo [position], o null si no cae sobre ninguna.
        // The pointer position is relative to the visible field; the text layout is not scrolled, so the field's scroll is added (as the task
        // drag does). Without it, in a long scrolled list a tap resolved to a row `scroll` pixels higher up.
        fun checkboxAt(position: Offset): Int? =
            toggleCheckboxAtTap(shownText(), layout?.invoke(), position.copy(y = position.y + fieldScroll.value), slack)?.let { HiddenTasks.toRaw(it, hiddenSpans) }
        // `pointerInput(key)` conserva su primera lambda mientras `key` no cambie: sin esto leería los `hiddenSpans` de la primera composición,
        // y al ocultarse una tarea hecha el texto que se ve ya no coincidiría con el diseño (tocar mal una casilla, o un índice fuera de rango).
        val currentCheckboxAt = rememberUpdatedState<(Offset) -> Int?> { checkboxAt(it) }
        // Con tareas, se deja sitio a la derecha para el asa de arrastre.
        val hasTasks = TaskLines.count(state.text.toString()).total > 0 && !protectedNote
        val lineHeight = with(LocalDensity.current) { MaterialTheme.typography.bodyLarge.lineHeight.toDp() }
        // Con papel o retícula, un poco de aire entre la cabecera y la primera fila para que el texto no se apure arriba.
        if (look.telecomGrid || look.paper == AspectSkin.Paper.RULED || look.paper == AspectSkin.Paper.GRID) Spacer(Modifier.height(12.dp))
        Box(
            Modifier.weight(1f).fillMaxWidth().paperTexture(
                if (look.paper == AspectSkin.Paper.RULED || look.paper == AspectSkin.Paper.GRID) look.paper else AspectSkin.Paper.NONE, ink, faceLight,
                ruleStep = lineHeight, ruleOffset = TASK_DRAG_PAD_TOP + lineHeight * 0.88f, marginX = 10.dp,
            )
                // La retícula del osciloscopio (Telecom oscuro) con cuadros del alto de línea y un renglón bajo cada línea del texto, como las libretas.
                .then(if (tint != null && look.telecomGrid) Modifier.drawBehind { drawTelecomGrid(gridInk, lineHeight.toPx(), (TASK_DRAG_PAD_TOP + lineHeight * 0.88f).toPx()) } else Modifier),
        ) {
        BasicTextField(
            state = state,
            scrollState = fieldScroll,
            inputTransformation = ListKeysTransformation,
            outputTransformation = hideTransformation,
            onTextLayout = { getResult ->
                layout = getResult
                getResult()?.let { plainWraps = plainLineWraps(it) }
            },
            modifier = Modifier
                .fillMaxSize()
                .taskDrag(
                    key, drag, layout = { layout?.invoke() }, scroll = fieldScroll, shown = ::shownText,
                    onStart = { haptic.performHapticFeedback(HapticFeedbackType.LongPress) },
                    onDrop = { fromShown, toShown ->
                        val raw = text()
                        val shown = shownText()
                        val from = TaskDrag.rawLine(shown, hiddenSpans, raw, fromShown)
                        val to = TaskDrag.rawLine(shown, hiddenSpans, raw, toShown)
                        LineMovement.moveTo(raw, from, to)?.let { commit(it) }
                    },
                )
                // Tocar una casilla la marca. Se mira en la pasada Initial. Si el toque cae sobre una casilla se consume el «down» para que el
                // campo no ponga ahí el cursor ni abra el teclado (marcar una lista no debe taparla); el desplazamiento sigue funcionando
                // porque el scroll no exige un down sin consumir. Fuera de una casilla no se consume nada.
                .pointerInput(key) {
                    // This detector sits outside the padding, so the left margin counts as part of the checkbox target (a thumb that
                    // lands left of the box still ticks it). Positions are converted to the text layout's own coordinates.
                    val padStart = TASK_DRAG_PAD_START.toPx()
                    val padTop = TASK_DRAG_PAD_TOP.toPx()
                    fun contentOffset(p: Offset) = Offset(p.x - padStart, p.y - padTop)
                    awaitEachGesture {
                        val down = awaitFirstDown(requireUnconsumed = false, pass = PointerEventPass.Initial)
                        touched = true
                        val onCheckbox = currentCheckboxAt.value(contentOffset(down.position)) != null
                        if (onCheckbox) down.consume()
                        val up = waitForUpOrCancellation(PointerEventPass.Initial) ?: return@awaitEachGesture
                        if ((up.position - down.position).getDistance() > touchSlop) return@awaitEachGesture
                        if (up.uptimeMillis - down.uptimeMillis >= longPress) return@awaitEachGesture
                        // Layout coordinates, taken now: the toggle runs a frame later and the list may have scrolled by then.
                        tap = contentOffset(up.position).let { it.copy(y = it.y + fieldScroll.value) }
                        if (onCheckbox) up.consume()
                    }
                }
                .padding(start = TASK_DRAG_PAD_START, end = if (hasTasks) TASK_DRAG_ZONE else TEXT_END_PAD, top = TASK_DRAG_PAD_TOP, bottom = 8.dp)
                .focusProperties { this.canFocus = canFocus }
                .focusRequester(focus)
                // Marcar una tarea sin ver la pantalla: la casilla no es un nodo propio (todo el texto es un campo), así que TalkBack no
                // puede tocarla. Esta acción marca o desmarca la tarea de la línea del cursor, como Ctrl+Enter con teclado físico.
                .semantics {
                    customActions = listOf(CustomAccessibilityAction(toggleTaskLabel) {
                        val glyph = TaskLists.checkboxOnLine(text(), caret())
                        if (glyph != null) toggleTask(glyph, keepCaretPosition = true)
                        glyph != null
                    })
                }
                .onPreviewKeyEvent { event ->
                    if (event.type != KeyEventType.KeyDown) return@onPreviewKeyEvent false
                    when {
                        event.key == Key.Tab -> {
                            if (event.isShiftPressed) outdent() else indent()
                            true
                        }
                        event.isAltPressed && (event.key == Key.DirectionUp || event.key == Key.DirectionDown) -> {
                            move(if (event.key == Key.DirectionUp) LineDirection.Up else LineDirection.Down)
                            true
                        }
                        // Ctrl+Enter marca la tarea de la línea; en una línea que no lo es, Enter normal.
                        event.isCtrlPressed && (event.key == Key.Enter || event.key == Key.NumPadEnter) -> {
                            val glyph = TaskLists.checkboxOnLine(text(), caret())
                            if (glyph != null) toggleTask(glyph, keepCaretPosition = true)
                            glyph != null
                        }
                        else -> false
                    }
                },
            // With big boxes a task list gets a little more line height: a taller row is an easier target. Ruled paper and grids keep theirs
            // (the rules are drawn at that step).
            textStyle = bodyStyle.copy(
                color = ink,
                lineHeight = if (hasTasks && settings.largeCheckboxes && !(look.telecomGrid || look.paper == AspectSkin.Paper.RULED || look.paper == AspectSkin.Paper.GRID)) bodyStyle.lineHeight * 1.15f else bodyStyle.lineHeight,
                textIndent = if (hasTasks && !plainWraps) TextIndent(firstLine = 0.sp, restLine = taskIndent) else null,
            ).let { style ->
                // Optional: justified running text with syllable hyphenation. Only wrapped lines stretch, so lists, tasks and short lines keep their
                // shape; the hyphenation dictionary is the system's (it follows the app language).
                if (settings.justifyText) style.copy(
                    textAlign = androidx.compose.ui.text.style.TextAlign.Justify,
                    hyphens = androidx.compose.ui.text.style.Hyphens.Auto,
                    lineBreak = androidx.compose.ui.text.style.LineBreak.Paragraph,
                ) else style
            },
            // The accent when it can be seen on the page, the ink of the note when it cannot: the accent on a yellow or a pink note was lost.
            cursorBrush = SolidColor(MaterialTheme.colorScheme.primary.let { accent ->
                val page = faceHex?.let { noteFace(it) } ?: MaterialTheme.colorScheme.background
                if (androidx.core.graphics.ColorUtils.calculateContrast(accent.toArgb(), page.copy(alpha = 1f).toArgb()) >= 4.5) accent else ink
            }),
        )
        if (hasTasks) TaskDragOverlay(drag, layout = { layout?.invoke() }, scroll = fieldScroll, shown = ::shownText, modifier = Modifier.matchParentSize(), ink = ink,
            lift = faceHex?.let { noteFace(it) } ?: MaterialTheme.colorScheme.background)
        }
        // A web link under the cursor offers «Open <host>». The host is shown so the user sees where it goes; only http(s) ever qualifies
        // (LinkText), and nothing opens without this tap.
        val caretLink = remember(state.text, state.selection, liveMarkdown) {
            val raw = state.text.toString()
            val at = state.selection.start
            if (!state.selection.collapsed) null
            else {
                // A Markdown link offers its address from its text too (while Markdown is drawn); it must pass the same http(s) check.
                val written = if (liveMarkdown) (MarkdownLive.linkUrlAt(raw, at) ?: MarkdownLive.linkUrlAt(raw, at - 1))?.takeIf { LinkText.toUri(it) != null } else null
                written ?: (LinkText.findAt(raw, at) ?: LinkText.findAt(raw, at - 1))?.let { raw.substring(it.start, it.end) }
            }
        }
        if (caretLink != null) {
            val linkContext = LocalContext.current
            val linkHost = LinkText.hostOf(caretLink) ?: caretLink
            AssistChip(
                onClick = {
                    try {
                        linkContext.startActivity(android.content.Intent(android.content.Intent.ACTION_VIEW, android.net.Uri.parse(java.net.URI(caretLink).toASCIIString())))
                    } catch (_: android.content.ActivityNotFoundException) {
                        // No browser installed: nothing to open.
                    }
                },
                label = { Text(stringResource(R.string.open_link, linkHost)) },
                modifier = Modifier.padding(horizontal = 16.dp, vertical = 4.dp),
                colors = AssistChipDefaults.assistChipColors(labelColor = ink),
                border = AssistChipDefaults.assistChipBorder(true, borderColor = ink.copy(alpha = 0.4f)),
            )
        }
        if (hiddenCandidates.isNotEmpty()) {
            val count = HiddenTasks.hiddenCount(text(), completed, now, hideDelayMs)
            // Un chip y no un botón de texto: tiene contorno, se lee como algo que se puede tocar y mide 48 dp de alto.
            AssistChip(
                onClick = { showDone = !showDone },
                label = { Text(if (showDone) stringResource(R.string.hidden_tasks_hide) else stringResource(R.string.hidden_tasks_show, count)) },
                modifier = Modifier.padding(horizontal = 16.dp, vertical = 4.dp),
                colors = AssistChipDefaults.assistChipColors(labelColor = ink),
                border = AssistChipDefaults.assistChipBorder(true, borderColor = ink.copy(alpha = 0.4f)),
            )
        }
        // Quitar las marcadas, a mano: la lista de la compra se vacía al volver de la tienda. Deshacer (↶) lo recupera.
        if (!protectedNote && TaskLists.hasChecked(text())) {
            AssistChip(
                onClick = {
                    val edit = TaskLists.removeChecked(text())
                    commit(edit.text, TextRange(edit.mapIndex(state.selection.start), edit.mapIndex(state.selection.end)))
                },
                label = { Text(stringResource(R.string.edit_remove_checked)) },
                modifier = Modifier.padding(horizontal = 16.dp, vertical = 4.dp),
                colors = AssistChipDefaults.assistChipColors(labelColor = ink),
                border = AssistChipDefaults.assistChipBorder(true, borderColor = ink.copy(alpha = 0.4f)),
            )
        }
        if (!compactHeight) {
            HorizontalDivider()
            EditBar(
                onCheckbox = { commitKeepingScroll(TaskLines.toggleTaskLineAt(text(), caret())) },
                onBullet = { commitKeepingScroll(BulletLines.toggleBulletLineAt(text(), caret())) },
                onOutdent = ::outdent,
                onIndent = ::indent,
                canUndo = editedUndo.canUndo, canRedo = editedUndo.canRedo,
                onUndo = { editedUndo.undo() }, onRedo = { editedUndo.redo() },
            )
        }
    }
    }
}

/** La retícula de osciloscopio: líneas de 1 px cada 24 dp, muy translúcidas, para que se lea como fondo y no compita con el texto. */
private fun DrawScope.drawTelecomGrid(color: Color, step: Float, firstRule: Float) {
    var x = 0f
    while (x <= size.width) { drawLine(color, Offset(x, 0f), Offset(x, size.height), strokeWidth = 1f); x += step }
    var y = firstRule % step
    while (y <= size.height) { drawLine(color, Offset(0f, y), Offset(size.width, y), strokeWidth = 1f); y += step }
}

private tailrec fun Context.findActivity(): Activity? = when (this) {
    is Activity -> this
    is ContextWrapper -> baseContext.findActivity()
    else -> null
}

/**
 * The position of the tapped checkbox, or `null` if the tap did not land on one. The target is the checkbox plus the space that follows it (up
 * to where the task text begins), over the whole height of its line: a tap in that gap used to resolve to the text (the nearest character
 * boundary is the first letter), so it put the cursor there, and the cursor handle then covered the next row's checkbox. Tapping the text of
 * the task itself still does not tick it.
 */
internal fun plainLineWraps(layout: TextLayoutResult): Boolean {
    val text = layout.layoutInput.text.text
    var start = 0
    while (start <= text.length) {
        val end = text.indexOf('\n', start).let { if (it < 0) text.length else it }
        val line = text.substring(start, end).trimEnd('\r')
        if (line.isNotBlank() && !TaskLines.isTaskLine(line) && layout.getLineForOffset(start) != layout.getLineForOffset(start + line.length - 1)) return true
        start = end + 1
    }
    return false
}

internal fun toggleCheckboxAtTap(text: String, layout: TextLayoutResult?, position: Offset, slack: Float): Int? {
    layout ?: return null
    if (text.isEmpty()) return null
    val offset = layout.getOffsetForPosition(position)
    val line = layout.getLineForVerticalPosition(position.y)
    if (position.y < layout.getLineTop(line) || position.y > layout.getLineBottom(line)) return null
    for (candidate in intArrayOf(offset, offset - 1, offset - 2)) {
        if (candidate !in text.indices || candidate < layout.getLineStart(line) || candidate >= layout.getLineEnd(line)) continue
        if (TaskLines.toggleCheckboxAt(text, candidate) == null) continue
        val box = layout.getBoundingBox(candidate)
        // Up to the start of the next visible character (the task text) when it is on the same line; otherwise the usual slack.
        val textStart = if (candidate + 1 < layout.getLineEnd(line)) layout.getBoundingBox(candidate + 1).right else box.right + slack
        val right = maxOf(box.right + slack / 2, textStart)
        if (position.x <= right) return candidate
    }
    return null
}

/**
 * Barra sobre el teclado: casilla, viñeta, sangría, deshacer y rehacer, con iconos. Antes eran siete botones de texto (↶ ↷ ☐ → ⇤ ⇥ ⋯) y
 * cuatro flechas seguidas no se distinguían. Reducir la sangría va en la pulsación larga de «Aumentar sangría» (y en el menú «⋮» de arriba, para
 * quien no pueda mantener pulsado); subir, bajar y desmarcar todo viven también en ese menú, porque con el dedo se arrastra la tarea pero TalkBack
 * no puede arrastrar. En pantallas anchas la barra no pasa de [BAR_MAX_WIDTH].
 */
@Composable
private fun EditBar(
    onCheckbox: () -> Unit,
    onBullet: () -> Unit,
    onOutdent: () -> Unit,
    onIndent: () -> Unit,
    canUndo: Boolean,
    canRedo: Boolean,
    onUndo: () -> Unit,
    onRedo: () -> Unit,
    inline: Boolean = false,
) {
    // Not inline: five 48 dp buttons spread edge to edge, with the glyph of the first and the last about where the text of the note starts and ends
    // (16 dp). Evenly spaced 64 dp buttons left the outer icons about 38 dp from the edges, out of line with the text above them.
    val width = if (inline) 44.dp else 48.dp
    Row(
        if (inline) Modifier else Modifier.widthIn(max = BAR_MAX_WIDTH).fillMaxWidth().padding(horizontal = 4.dp),
        horizontalArrangement = if (inline) Arrangement.SpaceEvenly else Arrangement.SpaceBetween,
    ) {
        BarButton(EditorIcons.Checkbox, stringResource(R.string.edit_checkbox), onCheckbox, width = width)
        BarButton(EditorIcons.Bullets, stringResource(R.string.edit_bullet), onBullet, width = width)
        BarButton(EditorIcons.Indent, stringResource(R.string.edit_indent), onIndent, onLongClick = onOutdent, longClickLabel = stringResource(R.string.edit_outdent), width = width)
        BarButton(EditorIcons.Undo, stringResource(R.string.edit_undo), onUndo, canUndo, width = width)
        BarButton(EditorIcons.Redo, stringResource(R.string.edit_redo), onRedo, canRedo, width = width)
    }
}

private val BAR_MAX_WIDTH = 480.dp

@OptIn(ExperimentalFoundationApi::class)
@Composable
private fun BarButton(
    icon: androidx.compose.ui.graphics.vector.ImageVector, description: String, onClick: () -> Unit, enabled: Boolean = true,
    onLongClick: (() -> Unit)? = null, longClickLabel: String? = null, width: androidx.compose.ui.unit.Dp = 48.dp,
) {
    // 48 × 48 dp: el mínimo táctil (eran 64 de ancho, pero separaban los iconos de los bordes). Inactivo se atenúa a la mitad (con 0,38 casi desaparecía).
    Box(
        Modifier.size(width = width, height = 48.dp)
            .combinedClickable(enabled = enabled, role = Role.Button, onClickLabel = null, onLongClickLabel = longClickLabel, onLongClick = onLongClick, onClick = onClick)
            .semantics { contentDescription = description },
        contentAlignment = Alignment.Center,
    ) {
        Icon(icon, contentDescription = null, tint = LocalContentColor.current.copy(alpha = if (enabled) 1f else 0.5f))
    }
}
