package app.aldune.ui

import androidx.compose.foundation.ScrollState
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.gestures.awaitEachGesture
import androidx.compose.foundation.gestures.awaitFirstDown
import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableFloatStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberUpdatedState
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clipToBounds
import androidx.compose.ui.geometry.CornerRadius
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.graphics.drawscope.clipRect
import androidx.compose.ui.text.drawText
import androidx.compose.ui.input.pointer.PointerEventPass
import androidx.compose.ui.input.pointer.changedToUpIgnoreConsumed
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.text.TextLayoutResult
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp
import app.aldune.text.TaskLists

/**
 * Arrastrar una tarea con el dedo: el asa (tres rayitas) al final de cada línea de tarea. La lógica de líneas es de `android/text`
 * (`TaskDrag`, `LineMovement.moveTo`); aquí solo hay gesto y dibujo.
 *
 * Todo va en coordenadas del campo entero (con su relleno); el diseño del texto está desplazado por [PAD_TOP] y por el scroll del campo.
 */
internal class TaskDragState {
    /** Línea visible (desde 0) que se está arrastrando, o `null`. */
    var from by mutableStateOf<Int?>(null)

    /** Dónde está el dedo, en coordenadas del diseño del texto (ya sin relleno ni scroll). */
    var y by mutableFloatStateOf(0f)
}

internal val TASK_DRAG_PAD_TOP = 8.dp

/** El margen izquierdo del campo de texto: la copia de la tarea levantada se dibuja desde aquí, igual que el texto de verdad. */
internal val TASK_DRAG_PAD_START = 16.dp
internal val TASK_DRAG_ZONE = 48.dp

/** Línea visible (desde 0) de la tarea bajo la altura [yText] del diseño, o `null` si ahí no hay una tarea. */
private fun taskLineAt(layout: TextLayoutResult, shown: String, yText: Float): Int? {
    if (yText < 0f || yText > layout.size.height) return null
    val offset = layout.getOffsetForPosition(Offset(0f, yText))
    if (TaskLists.checkboxOnLine(shown, offset) == null) return null
    return app.aldune.text.TaskDrag.lineOf(shown, offset)
}

/** La línea visible a la que corresponde la altura [yText] (al soltar). */
private fun lineAt(layout: TextLayoutResult, shown: String, yText: Float): Int {
    val clamped = yText.coerceIn(0f, (layout.size.height - 1).coerceAtLeast(0).toFloat())
    return app.aldune.text.TaskDrag.lineOf(shown, layout.getLineStart(layout.getLineForVerticalPosition(clamped)))
}

/**
 * El gesto: un toque que empieza en la franja del asa (a la derecha) sobre una línea de tarea la arrastra. Se mira en la pasada Initial y se
 * consume, para que el campo no mueva el cursor. Fuera de esa franja no toca nada: el campo, los toques en las casillas y la selección siguen
 * como siempre.
 */
@Composable
internal fun Modifier.taskDrag(
    key: Any?,
    drag: TaskDragState,
    layout: () -> TextLayoutResult?,
    scroll: ScrollState,
    shown: () -> String,
    onStart: () -> Unit,
    onDrop: (fromShown: Int, toShown: Int) -> Unit,
): Modifier {
    val layoutNow by rememberUpdatedState(layout)
    val shownNow by rememberUpdatedState(shown)
    val onStartNow by rememberUpdatedState(onStart)
    val onDropNow by rememberUpdatedState(onDrop)
    return pointerInput(key) {
        val zone = TASK_DRAG_ZONE.toPx()
        val padTop = TASK_DRAG_PAD_TOP.toPx()
        awaitEachGesture {
            val down = awaitFirstDown(requireUnconsumed = false, pass = PointerEventPass.Initial)
            if (down.position.x < size.width - zone) return@awaitEachGesture
            val textLayout = layoutNow() ?: return@awaitEachGesture
            val text = shownNow()
            val from = taskLineAt(textLayout, text, down.position.y - padTop + scroll.value) ?: return@awaitEachGesture

            down.consume()
            var moved = false
            while (true) {
                val change = awaitPointerEvent(PointerEventPass.Initial).changes.firstOrNull { it.id == down.id } ?: break
                if (change.changedToUpIgnoreConsumed()) {
                    change.consume()
                    break
                }
                if (!moved && (change.position - down.position).getDistance() > viewConfiguration.touchSlop) {
                    moved = true
                    drag.from = from
                    onStartNow()
                }
                if (moved) drag.y = change.position.y - padTop + scroll.value
                change.consume()
            }
            val dropped = drag.from
            drag.from = null
            if (moved && dropped != null) {
                val to = lineAt(textLayout, text, drag.y)
                if (to != dropped) onDropNow(dropped, to)
            }
        }
    }
}

/** El asa en cada línea de tarea y, mientras se arrastra, la tarea bajo el dedo y la raya de dónde caerá. Solo dibuja: no recibe toques. */
@Composable
internal fun TaskDragOverlay(
    drag: TaskDragState,
    layout: () -> TextLayoutResult?,
    scroll: ScrollState,
    shown: () -> String,
    modifier: Modifier = Modifier,
    padTop: Dp = TASK_DRAG_PAD_TOP,
    ink: Color = MaterialTheme.colorScheme.onBackground,
    /** El color de la hoja: con él se atenúa la tarea de origen y se pinta el cuerpo de la tarea levantada. */
    lift: Color = MaterialTheme.colorScheme.background,
) {
    val grip = ink.copy(alpha = 0.5f)
    val accent = MaterialTheme.colorScheme.primary
    // clipToBounds: una tarea a medio ver en el borde dibujaba su asa (las tres rayas) por fuera del campo, encima de la barra de abajo.
    Canvas(modifier.clipToBounds()) {
        val textLayout = layout() ?: return@Canvas
        val text = shown()
        val top = padTop.toPx()
        val scrollY = scroll.value
        val gripX = size.width - TASK_DRAG_ZONE.toPx() / 2
        val half = 7.dp.toPx()
        val step = 4.dp.toPx()

        var start = 0
        while (start <= text.length) {
            val visual = textLayout.getLineForOffset(start.coerceAtMost(text.length))
            val lineTop = textLayout.getLineTop(visual) - scrollY + top
            val lineBottom = textLayout.getLineBottom(visual) - scrollY + top
            if (lineBottom >= 0f && lineTop <= size.height && TaskLists.checkboxOnLine(text, start) != null) {
                val cy = (lineTop + lineBottom) / 2
                for (i in -1..1) drawLine(grip, Offset(gripX - half, cy + i * step), Offset(gripX + half, cy + i * step), strokeWidth = 2.dp.toPx())
            }
            val lf = text.indexOf('\n', start)
            if (lf < 0) break
            start = lf + 1
        }

        val from = drag.from ?: return@Canvas
        val fromVisual = textLayout.getLineForOffset(app.aldune.text.TaskDrag.lineStart(text, from))
        val height = textLayout.getLineBottom(fromVisual) - textLayout.getLineTop(fromVisual)
        // La tarea «se levanta»: la de origen queda atenuada (se ve de dónde sale) y la que acompaña al dedo lleva su texto de verdad, sobre un
        // cuerpo con sombra y borde. Antes solo flotaba un marco translúcido y el texto no se movía: no se veía qué se estaba llevando.
        val bandTop = drag.y - scrollY + top - height / 2
        val fromTop = textLayout.getLineTop(fromVisual) - scrollY + top
        val corner = CornerRadius(8.dp.toPx())
        drawRect(lift.copy(alpha = 0.6f), Offset(0f, fromTop), Size(size.width, height))
        drawRoundRect(Color.Black.copy(alpha = 0.2f), Offset(2.dp.toPx(), bandTop + 4.dp.toPx()), Size(size.width - 2.dp.toPx(), height), corner)
        drawRoundRect(lift, Offset(0f, bandTop), Size(size.width, height), corner)
        clipRect(0f, bandTop, size.width, bandTop + height) {
            drawText(textLayout, topLeft = Offset(TASK_DRAG_PAD_START.toPx(), bandTop - textLayout.getLineTop(fromVisual)))
        }
        drawRoundRect(accent, Offset(0f, bandTop), Size(size.width, height), corner, style = Stroke(1.5.dp.toPx()))

        // Dónde caerá: la raya va antes de la línea de destino si se sube y después si se baja (así se ve la posición final).
        val clamped = drag.y.coerceIn(0f, (textLayout.size.height - 1).coerceAtLeast(0).toFloat())
        val targetVisual = textLayout.getLineForVerticalPosition(clamped)
        val targetLine = app.aldune.text.TaskDrag.lineOf(text, textLayout.getLineStart(targetVisual))
        val y = if (targetLine >= from) textLayout.getLineBottom(targetVisual) else textLayout.getLineTop(targetVisual)
        val lineY = y - scrollY + top
        drawLine(accent, Offset(0f, lineY), Offset(size.width, lineY), strokeWidth = 3.dp.toPx())
        // El punto del extremo, como en cualquier lista que se reordena: la raya sola se confunde con un subrayado.
        drawCircle(accent, radius = 5.dp.toPx(), center = Offset(6.dp.toPx(), lineY))
    }
}
