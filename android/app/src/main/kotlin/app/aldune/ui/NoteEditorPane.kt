package app.aldune.ui

import androidx.compose.foundation.gestures.awaitEachGesture
import androidx.compose.foundation.gestures.awaitFirstDown
import androidx.compose.foundation.gestures.waitForUpOrCancellation
import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.imePadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.safeDrawingPadding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.text.BasicTextField
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
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
import androidx.compose.runtime.withFrameNanos
import androidx.compose.runtime.setValue
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
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.platform.LocalViewConfiguration
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.TextLayoutResult
import androidx.compose.ui.text.TextRange
import androidx.compose.ui.text.coerceIn
import androidx.compose.ui.text.input.TextFieldValue
import androidx.compose.ui.unit.dp
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.compose.LifecycleEventEffect
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import app.aldune.R
import app.aldune.text.BulletLines
import app.aldune.text.LineDirection
import app.aldune.text.LineMovement
import app.aldune.text.ListIndent
import app.aldune.text.ListKeys
import app.aldune.text.TaskLines
import app.aldune.text.TaskLists
import app.aldune.text.TextAndCaret
import app.aldune.text.TextSelection

/**
 * Editor de una nota: texto con ayudas de lista (casillas, viñetas, sangría) y guardado automático (lo hace el `ViewModel`, ver
 * `openDraft`). [key] es el id de la nota, o "" para una nota nueva.
 */
@Composable
fun NoteEditorPane(key: String, viewModel: NotesViewModel, onBack: () -> Unit, showBack: Boolean) {
    var value by remember(key) { mutableStateOf(TextFieldValue("")) }
    var loaded by remember(key) { mutableStateOf(false) }
    var layout by remember(key) { mutableStateOf<TextLayoutResult?>(null) }
    // Toque pendiente sobre el texto. No se aplica en el mismo evento: el campo, al recibir ese mismo toque, emite el texto
    // anterior con el cursor nuevo y pisaría el cambio; se espera al siguiente fotograma (ver LaunchedEffect de abajo).
    var tap by remember(key) { mutableStateOf<Offset?>(null) }

    LaunchedEffect(key) {
        value = TextFieldValue(viewModel.openDraft(key))
        loaded = true
    }
    // Al salir de la nota (otra nota, atrás, el panel se destruye) y al pasar la app a segundo plano.
    DisposableEffect(key) { onDispose { viewModel.flushDraft(key) } }
    LifecycleEventEffect(Lifecycle.Event.ON_STOP) { viewModel.flushAll() }

    /** Único punto por el que cambia el valor: así ningún camino (teclado, barra, atajo, toque) se salta el guardado. */
    fun commit(next: TextFieldValue) {
        val changed = next.text != value.text
        value = next
        if (loaded && changed) viewModel.updateDraft(key, next.text)
    }

    fun commit(edit: TextAndCaret) = commit(TextFieldValue(edit.text, TextRange(edit.caret)))

    fun caret() = value.selection.min

    // Con una selección, la sangría afecta a todas las líneas que toca y la selección se conserva.
    fun shift(lines: (String, Int, Int) -> app.aldune.text.SelectionEdit, single: (String, Int) -> TextAndCaret) {
        val s = value.selection
        if (s.collapsed) return commit(single(value.text, s.start))
        val e = lines(value.text, s.min, s.length)
        commit(TextFieldValue(e.text, TextRange(e.selectionStart, e.selectionStart + e.selectionLength)))
    }

    fun indent() = shift(ListIndent::indentLines, ListIndent::indent)

    fun outdent() = shift(ListIndent::outdentLines, ListIndent::outdent)

    fun move(direction: LineDirection) {
        LineMovement.move(value.text, caret(), direction)?.let { commit(it) }
    }

    val slack = with(LocalDensity.current) { 12.dp.toPx() }

    val reorderOnCheck by viewModel.settings.collectAsStateWithLifecycle()
    val settleChecked = reorderOnCheck.reorderOnCheck

    /**
     * Marca o desmarca la casilla de [glyph] y, con el ajuste «mover abajo las tareas hechas», la recoloca en su lista. Al tocar, el cursor
     * sigue a su línea; con Ctrl+Enter se queda en el mismo sitio de la pantalla, como en Windows, para poder marcar una lista seguida.
     */
    fun toggleTask(glyph: Int, keepCaretPosition: Boolean) {
        val toggled = TaskLines.toggleCheckboxAt(value.text, glyph) ?: return
        val sel = value.selection
        if (!settleChecked) return commit(TextFieldValue(toggled, sel))
        val edit = TaskLists.settleToggled(toggled, glyph)
        val moved = if (keepCaretPosition) sel else TextRange(edit.mapIndex(sel.start), edit.mapIndex(sel.end))
        commit(TextFieldValue(edit.text, moved.coerceIn(0, edit.text.length)))
    }

    LaunchedEffect(tap) {
        val position = tap ?: return@LaunchedEffect
        withFrameNanos { }
        toggleCheckboxAtTap(value.text, layout, position, slack)?.let { toggleTask(it, keepCaretPosition = false) }
        tap = null
    }

    val touchSlop = LocalViewConfiguration.current.touchSlop
    val longPress = LocalViewConfiguration.current.longPressTimeoutMillis

    Column(Modifier.fillMaxSize().safeDrawingPadding().imePadding()) {
        if (showBack) {
            Row(Modifier.fillMaxWidth()) {
                IconButton(onClick = onBack) { Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = stringResource(R.string.back)) }
            }
        }
        BasicTextField(
            value = value,
            onValueChange = { typed ->
                // Enter y Retroceso del teclado virtual llegan como texto, no como tecla: se deducen por diferencia.
                val old = value
                val fixed = if (typed.text != old.text) {
                    ListKeys.resolve(
                        TextSelection(old.text, old.selection.min, old.selection.max),
                        TextAndCaret(typed.text, typed.selection.start),
                    )
                } else null
                if (fixed != null) commit(fixed) else commit(typed)
            },
            onTextLayout = { layout = it },
            modifier = Modifier
                .weight(1f)
                .fillMaxWidth()
                .padding(horizontal = 16.dp, vertical = 8.dp)
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
                            val glyph = TaskLists.checkboxOnLine(value.text, caret())
                            if (glyph != null) toggleTask(glyph, keepCaretPosition = true)
                            glyph != null
                        }
                        else -> false
                    }
                },
            textStyle = MaterialTheme.typography.bodyLarge.copy(color = MaterialTheme.colorScheme.onBackground),
            cursorBrush = SolidColor(MaterialTheme.colorScheme.primary),
        )
        HorizontalDivider()
        EditBar(
            text = value.text,
            onCheckbox = { commit(TaskLines.toggleTaskLineAt(value.text, caret())) },
            onBullet = { commit(BulletLines.toggleBulletLineAt(value.text, caret())) },
            onOutdent = ::outdent,
            onIndent = ::indent,
            onMoveUp = { move(LineDirection.Up) },
            onMoveDown = { move(LineDirection.Down) },
            onUncheckAll = { commit(TextFieldValue(TaskLists.uncheckAll(value.text).text, value.selection)) },
            onRemoveChecked = {
                val edit = TaskLists.removeChecked(value.text)
                commit(TextFieldValue(edit.text, TextRange(edit.mapIndex(value.selection.start), edit.mapIndex(value.selection.end))))
            },
        )
    }
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
