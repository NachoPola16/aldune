package app.aldune.ui

import android.app.Activity
import android.content.Context
import android.content.ContextWrapper
import android.widget.Toast
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
import androidx.compose.foundation.layout.safeDrawingPadding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.text.BasicTextField
import androidx.compose.foundation.text.input.InputTransformation
import androidx.compose.foundation.text.input.OutputTransformation
import androidx.compose.foundation.text.input.delete
import androidx.compose.material3.TextButton
import app.aldune.data.CompletedTasksStore
import app.aldune.text.HiddenTasks
import androidx.compose.foundation.text.input.TextFieldState
import androidx.compose.runtime.snapshotFlow
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.MoreVert
import androidx.compose.material3.Button
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
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.withFrameNanos
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.SolidColor
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
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalSoftwareKeyboardController
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.platform.LocalViewConfiguration
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.TextLayoutResult
import androidx.compose.ui.text.TextRange
import androidx.compose.ui.text.coerceIn
import androidx.compose.ui.unit.dp
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.compose.LifecycleEventEffect
import androidx.lifecycle.compose.collectAsStateWithLifecycle
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
import app.aldune.text.TaskLines
import app.aldune.text.TaskLists
import app.aldune.text.TextAndCaret
import app.aldune.text.TextSelection
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch

/** Espera antes de pedir el teclado en una nota nueva (ver el efecto de foco de [NoteEditorPane]). */
private const val KEYBOARD_DELAY_MS = 250L

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
    if (old.text == typed.text) return@InputTransformation
    val fixed = ListKeys.resolve(old, typed) ?: return@InputTransformation
    replace(0, length, fixed.text)
    selection = TextRange(fixed.caret)
}

@Composable
fun NoteEditorPane(key: String, viewModel: NotesViewModel, onBack: () -> Unit, showBack: Boolean) {
    val state = remember(key) { TextFieldState() }
    var loaded by remember(key) { mutableStateOf(false) }
    // Una protegida solo se lee y se edita con su contraseña: hasta entonces se ve un aviso con el botón de abrirla. `unlocked` dice que hay una
    // sesión abierta en el ViewModel; el texto en claro está solo en este campo y en esa sesión (nunca en un borrador ni en estado guardado).
    var protectedNote by remember(key) { mutableStateOf(false) }
    var unlocked by remember(key) { mutableStateOf(false) }
    var passwordMode by remember(key) { mutableStateOf<PasswordMode?>(null) }
    val context = LocalContext.current
    var options by remember(key) { mutableStateOf<NotesViewModel.NoteOptions?>(null) }
    val scope = rememberCoroutineScope()
    var layout by remember(key) { mutableStateOf<(() -> TextLayoutResult?)?>(null) }
    // Toque pendiente sobre el texto. No se aplica en el mismo evento: el campo, al recibir ese mismo toque, emite el texto
    // anterior con el cursor nuevo y pisaría el cambio; se espera al siguiente fotograma (ver LaunchedEffect de abajo).
    var tap by remember(key) { mutableStateOf<Offset?>(null) }

    // Carga la nota (otra vez, tras resolver un conflicto que le cambió el texto o la protección). Mientras dura, el texto no se guarda.
    suspend fun load() {
        loaded = false
        val text = viewModel.openDraft(key)
        protectedNote = viewModel.isProtected(key)
        if (!protectedNote) {
            state.edit {
                replace(0, length, text)
                selection = TextRange(0)
            }
        } else {
            // Al rotar, la sesión sigue abierta en el ViewModel: se recarga sin volver a pedir la contraseña.
            val plain = viewModel.protectedText(key)
            if (plain != null) {
                state.edit {
                    replace(0, length, plain)
                    selection = TextRange(0)
                }
                unlocked = true
            } else {
                passwordMode = PasswordMode.Unlock
            }
        }
        loaded = true
    }
    LaunchedEffect(key) { load() }
    // Una pasada de sync cambió el texto de esta nota mientras estaba abierta (y no se había tocado): se recarga, o la siguiente tecla guardaría
    // el texto viejo encima del recién llegado.
    LaunchedEffect(key) {
        viewModel.externalEdits.collect { changed ->
            if (changed != key) return@collect
            val fresh = viewModel.draftText(key)
            if (fresh == state.text.toString()) return@collect
            state.edit {
                replace(0, length, fresh)
                selection = TextRange(selection.min.coerceAtMost(fresh.length))
            }
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
        snapshotFlow { state.text.toString() }.collect {
            if (!loaded) return@collect
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
                state.edit { replace(0, length, "") }
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
            viewModel.flushDraft(key)
            if (context.findActivity()?.isChangingConfigurations != true) viewModel.lockProtected(key)
        }
    }
    LifecycleEventEffect(Lifecycle.Event.ON_STOP) { viewModel.flushAll() }

    fun text() = state.text.toString()

    fun commit(text: String, selection: TextRange) {
        state.edit {
            replace(0, length, text)
            this.selection = selection.coerceIn(0, text.length)
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

    val settings by viewModel.settings.collectAsStateWithLifecycle()
    val settleChecked = settings.reorderOnCheck

    // «Ocultar tareas hechas»: el texto real no cambia; el campo dibuja el texto sin esas líneas (OutputTransformation) y los toques se
    // traducen de vuelta al texto real. Una protegida no apunta nada: no se guarda ni un rastro de su contenido fuera de ella.
    val hideActive = settings.hideDoneTasks && !protectedNote
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
    val hideTransformation = remember(hideActive, showDone, completed, hideDelayMs) {
        if (!hideActive || showDone) null else OutputTransformation {
            val spans = HiddenTasks.hiddenSpans(asCharSequence().toString(), completed, System.currentTimeMillis(), hideDelayMs)
            for (span in spans.asReversed()) delete(span.start, span.end)
        }
    }

    /**
     * Marca o desmarca la casilla de [glyph] y, con el ajuste «mover abajo las tareas hechas», la recoloca en su lista. Al tocar, el cursor
     * sigue a su línea; con Ctrl+Enter se queda en el mismo sitio de la pantalla, como en Windows, para poder marcar una lista seguida.
     */
    fun toggleTask(glyph: Int, keepCaretPosition: Boolean) {
        val toggled = TaskLines.toggleCheckboxAt(text(), glyph) ?: return
        val sel = state.selection
        if (!settleChecked) return commit(toggled, sel)
        val edit = TaskLists.settleToggled(toggled, glyph)
        val moved = if (keepCaretPosition) sel else TextRange(edit.mapIndex(sel.start), edit.mapIndex(sel.end))
        commit(edit.text, moved)
    }

    // Una nota nueva (el «+», el acceso directo, el mosaico, la notificación) es para escribir ya: se pide el foco y el teclado. Sin esto,
    // el teclado salía o no según una carrera con el foco de la ventana (en frío no, con la app ya abierta sí). Una nota existente no lo abre:
    // al leerla no hay que tapar la mitad del texto.
    val focus = remember { FocusRequester() }
    val keyboard = LocalSoftwareKeyboardController.current
    LaunchedEffect(key, loaded, protectedNote) {
        if (key.isEmpty() && loaded && !protectedNote) {
            focus.requestFocus()
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

    Column(Modifier.fillMaxSize().safeDrawingPadding().imePadding()) {
        Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
            if (showBack) {
                IconButton(onClick = onBack) { Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = stringResource(R.string.back)) }
            }
            Spacer(Modifier.weight(1f))
            IconButton(onClick = { scope.launch { options = viewModel.noteOptions(key) } }, enabled = loaded) {
                Icon(Icons.Default.MoreVert, contentDescription = stringResource(R.string.note_options))
            }
        }
        options?.let { current ->
            NoteOptionsDialog(
                current, viewModel, onClose = { options = null },
                // Lo que se ve ahora en el editor (como en Windows). Una protegida cerrada no tiene texto que exportar.
                exportText = if (!current.isProtected || unlocked) text() else null,
                onProtection = {
                    options = null
                    passwordMode = if (current.isProtected) PasswordMode.Remove else PasswordMode.Protect
                },
            )
        }
        passwordMode?.let { mode ->
            PasswordDialog(mode, onDismiss = { passwordMode = null }) { password, confirmation ->
                when (mode) {
                    PasswordMode.Unlock -> when (val outcome = viewModel.unlockProtected(key, password)) {
                        is NotesViewModel.UnlockOutcome.Opened -> {
                            state.edit {
                                replace(0, length, outcome.text)
                                selection = TextRange(0)
                            }
                            unlocked = true
                            null
                        }
                        NotesViewModel.UnlockOutcome.WrongPassword -> PasswordFailure.Wrong
                        NotesViewModel.UnlockOutcome.Unreadable -> PasswordFailure.Unreadable
                        NotesViewModel.UnlockOutcome.Unsupported -> PasswordFailure.Unsupported
                        NotesViewModel.UnlockOutcome.Gone -> PasswordFailure.Changed
                    }
                    PasswordMode.Protect -> when (val result = viewModel.protectNote(key, password, confirmation)) {
                        is ProtectionResult.Ok -> {
                            // Queda abierta, como en Windows: se abre la sesión con la misma contraseña (derivar otra vez).
                            when (val outcome = viewModel.unlockProtected(key, password)) {
                                is NotesViewModel.UnlockOutcome.Opened -> {
                                    state.edit {
                                        replace(0, length, outcome.text)
                                        selection = TextRange(0)
                                    }
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
                            state.edit {
                                replace(0, length, plain)
                                selection = TextRange(0)
                            }
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
            PasswordDialog(PasswordMode.Unlock, onDismiss = { unlockOther = false }) { password, _ ->
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
                Text(stringResource(R.string.protected_note_body), color = MaterialTheme.colorScheme.onSurfaceVariant)
                Button(onClick = { passwordMode = PasswordMode.Unlock }, Modifier.padding(top = 16.dp)) {
                    Text(stringResource(R.string.password_unlock_action))
                }
            }
            return@Column
        }
        BasicTextField(
            state = state,
            inputTransformation = ListKeysTransformation,
            outputTransformation = hideTransformation,
            onTextLayout = { getResult -> layout = getResult },
            modifier = Modifier
                .weight(1f)
                .fillMaxWidth()
                .padding(horizontal = 16.dp, vertical = 8.dp)
                .focusRequester(focus)
                // Tocar una casilla la marca. Se mira en la pasada Initial y sin consumir nada: el campo sigue recibiendo
                // el toque (cursor, teclado) como siempre.
                .pointerInput(key) {
                    awaitEachGesture {
                        val down = awaitFirstDown(requireUnconsumed = false, pass = PointerEventPass.Initial)
                        val up = waitForUpOrCancellation(PointerEventPass.Initial) ?: return@awaitEachGesture
                        if ((up.position - down.position).getDistance() > touchSlop) return@awaitEachGesture
                        if (up.uptimeMillis - down.uptimeMillis >= longPress) return@awaitEachGesture
                        tap = up.position
                    }
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
            textStyle = MaterialTheme.typography.bodyLarge.copy(color = MaterialTheme.colorScheme.onBackground),
            cursorBrush = SolidColor(MaterialTheme.colorScheme.primary),
        )
        if (hiddenCandidates.isNotEmpty()) {
            val count = HiddenTasks.hiddenCount(text(), completed, now, hideDelayMs)
            TextButton(onClick = { showDone = !showDone }, Modifier.padding(horizontal = 8.dp)) {
                Text(if (showDone) stringResource(R.string.hidden_tasks_hide) else stringResource(R.string.hidden_tasks_show, count))
            }
        }
        HorizontalDivider()
        EditBar(
            text = state.text.toString(),
            onCheckbox = { commit(TaskLines.toggleTaskLineAt(text(), caret())) },
            onBullet = { commit(BulletLines.toggleBulletLineAt(text(), caret())) },
            onOutdent = ::outdent,
            onIndent = ::indent,
            onMoveUp = { move(LineDirection.Up) },
            onMoveDown = { move(LineDirection.Down) },
            onUncheckAll = { commit(TaskLists.uncheckAll(text()).text, state.selection) },
            onRemoveChecked = {
                val edit = TaskLists.removeChecked(text())
                commit(edit.text, TextRange(edit.mapIndex(state.selection.start), edit.mapIndex(state.selection.end)))
            },
        )
    }
}

private tailrec fun Context.findActivity(): Activity? = when (this) {
    is Activity -> this
    is ContextWrapper -> baseContext.findActivity()
    else -> null
}

/**
 * La posición de la casilla tocada, o `null` si el toque no cayó sobre una. `getOffsetForPosition` da el límite de carácter más
 * cercano, así que se prueban el carácter anterior y el siguiente; y se exige que el toque esté a menos de [slack] del glifo, para que
 * tocar el texto de la tarea no la marque.
 */
private fun toggleCheckboxAtTap(text: String, layout: TextLayoutResult?, position: Offset, slack: Float): Int? {
    layout ?: return null
    if (text.isEmpty()) return null
    val offset = layout.getOffsetForPosition(position)
    for (candidate in intArrayOf(offset, offset - 1)) {
        if (candidate !in text.indices) continue
        if (TaskLines.toggleCheckboxAt(text, candidate) == null) continue
        val box = layout.getBoundingBox(candidate)
        if (position.y in box.top..box.bottom && position.x in (box.left - slack)..(box.right + slack)) return candidate
    }
    return null
}

/** Barra sobre el teclado: casilla, viñeta, sangría, mover línea y el menú «⋯» de la lista de tareas. */
@Composable
private fun EditBar(
    text: String,
    onCheckbox: () -> Unit,
    onBullet: () -> Unit,
    onOutdent: () -> Unit,
    onIndent: () -> Unit,
    onMoveUp: () -> Unit,
    onMoveDown: () -> Unit,
    onUncheckAll: () -> Unit,
    onRemoveChecked: () -> Unit,
) {
    var menuOpen by remember { mutableStateOf(false) }
    Row(Modifier.fillMaxWidth().horizontalScroll(rememberScrollState())) {
        BarButton("☐", stringResource(R.string.edit_checkbox), onCheckbox)
        BarButton("→", stringResource(R.string.edit_bullet), onBullet)
        BarButton("⇤", stringResource(R.string.edit_outdent), onOutdent)
        BarButton("⇥", stringResource(R.string.edit_indent), onIndent)
        BarButton("↑", stringResource(R.string.edit_move_up), onMoveUp)
        BarButton("↓", stringResource(R.string.edit_move_down), onMoveDown)
        Box {
            BarButton("⋯", stringResource(R.string.edit_more)) { menuOpen = true }
            DropdownMenu(expanded = menuOpen, onDismissRequest = { menuOpen = false }) {
                DropdownMenuItem(
                    text = { Text(stringResource(R.string.edit_uncheck_all)) },
                    enabled = TaskLists.hasChecked(text),
                    onClick = { menuOpen = false; onUncheckAll() },
                )
                DropdownMenuItem(
                    text = { Text(stringResource(R.string.edit_remove_checked)) },
                    enabled = TaskLists.hasChecked(text),
                    onClick = { menuOpen = false; onRemoveChecked() },
                )
            }
        }
    }
}

@Composable
private fun BarButton(label: String, description: String, onClick: () -> Unit) {
    IconButton(onClick = onClick, modifier = Modifier.semantics { contentDescription = description }) {
        Text(label, style = MaterialTheme.typography.titleLarge, color = MaterialTheme.colorScheme.onBackground)
    }
}
