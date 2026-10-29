package app.aldune.ui

import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.gestures.awaitEachGesture
import androidx.compose.foundation.gestures.awaitFirstDown
import androidx.compose.foundation.gestures.drag
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ExperimentalLayoutApi
import androidx.compose.foundation.layout.FlowRow
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.input.pointer.PointerInputChange
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.heading
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.input.KeyboardCapitalization
import androidx.compose.ui.unit.dp
import app.aldune.R
import app.aldune.text.ColorHsv
import app.aldune.text.Hsv
import app.aldune.text.CustomColor
import app.aldune.text.NoteColorContrast

/**
 * Color libre de una nota (equivalente de `CustomColorWindow` de Windows): una rejilla de colores para encontrarlo a ojo con un toque,
 * un cuadro de saturación/brillo y una barra de tono para afinarlo arrastrando, y el HEX por si se conoce el valor. «Usar este color» solo
 * se activa si el HEX es válido y el texto de la nota se podrá leer encima.
 */
@OptIn(ExperimentalLayoutApi::class)
@Composable
fun CustomColorDialog(initial: String, onDismiss: () -> Unit, onApply: (String) -> Unit) {
    var color by remember { mutableStateOf(CustomColor.normalize(initial) ?: "#F5E3B3") }
    var hexText by remember { mutableStateOf(color) }
    // El tono se guarda aparte: en un gris (o en negro) el HEX no lo conserva, y al mover el cuadro el tono volvería siempre a rojo.
    var hue by remember { mutableStateOf(ColorHsv.fromHex(color)?.hue ?: 0f) }
    val readable = NoteColorContrast.isReadable(color, NoteColorContrast.foregroundFor(color))
    val hexValid = CustomColor.normalize(hexText) != null

    /** Un color elegido con el dedo; [keepHue] cuando el propio gesto ya manda sobre el tono (cuadro y barra). */
    fun pick(next: String, keepHue: Boolean = false) {
        color = next
        hexText = next
        if (!keepHue) ColorHsv.fromHex(next)?.takeIf { it.saturation > 0f }?.let { hue = it.hue }
    }

    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text(stringResource(R.string.custom_color_title), Modifier.semantics { heading() }) },
        text = {
            Column(Modifier.fillMaxWidth().verticalScroll(rememberScrollState()), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                Text(stringResource(R.string.custom_color_hint), style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                Box(
                    Modifier.fillMaxWidth().height(64.dp).clip(RoundedCornerShape(12.dp)).background(noteFace(color)).padding(12.dp),
                    contentAlignment = Alignment.CenterStart,
                ) {
                    Text(
                        stringResource(R.string.custom_color_preview) + "  " + color,
                        color = Color(android.graphics.Color.parseColor(NoteColorContrast.foregroundFor(color))),
                        style = MaterialTheme.typography.titleMedium,
                    )
                }
                Text(stringResource(R.string.custom_color_pick), style = MaterialTheme.typography.labelLarge, modifier = Modifier.padding(top = 4.dp))
                FlowRow(horizontalArrangement = Arrangement.spacedBy(8.dp), verticalArrangement = Arrangement.spacedBy(8.dp), maxItemsInEachRow = 6) {
                    for (hex in ColorHsv.swatches) {
                        val selected = hex.equals(color, ignoreCase = true)
                        Box(
                            Modifier.size(40.dp).clip(CircleShape).background(noteFace(hex))
                                .border(if (selected) 3.dp else 1.dp, if (selected) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.outline, CircleShape)
                                .clickable { pick(hex) }.semantics { contentDescription = hex },
                        )
                    }
                }
                Text(stringResource(R.string.custom_color_rgb), style = MaterialTheme.typography.labelLarge, modifier = Modifier.padding(top = 4.dp))
                val hsv = ColorHsv.fromHex(color) ?: Hsv(0f, 0f, 1f)
                SaturationValueArea(hue, hsv.saturation, hsv.value) { s, v -> pick(ColorHsv.toHex(hue, s, v), keepHue = true) }
                HueBar(hue) { h ->
                    hue = h
                    pick(ColorHsv.toHex(h, hsv.saturation, hsv.value), keepHue = true)
                }
                OutlinedTextField(
                    value = hexText,
                    // Lo tecleado no se reescribe (con «#f5e» a medias no se puede normalizar): solo se adopta cuando ya es un HEX completo.
                    onValueChange = { text ->
                        hexText = text.take(7)
                        CustomColor.normalize(hexText)?.let { valid ->
                            color = valid
                            ColorHsv.fromHex(valid)?.takeIf { it.saturation > 0f }?.let { hue = it.hue }
                        }
                    },
                    label = { Text(stringResource(R.string.custom_color_hex)) },
                    singleLine = true,
                    isError = !hexValid,
                    supportingText = { Text(stringResource(if (!hexValid || !readable) R.string.custom_color_invalid else R.string.custom_color_readable)) },
                    keyboardOptions = KeyboardOptions(capitalization = KeyboardCapitalization.Characters),
                    modifier = Modifier.fillMaxWidth(),
                )
            }
        },
        confirmButton = {
            TextButton(onClick = { onApply(color) }, enabled = hexValid && readable) { Text(stringResource(R.string.custom_color_apply)) }
        },
        dismissButton = { TextButton(onClick = onDismiss) { Text(stringResource(R.string.cancel)) } },
    )
}

/** Cuadro de saturación (a la derecha, más intenso) y brillo (arriba, más claro). Un gesto empieza donde se pone el dedo. */
@Composable
private fun SaturationValueArea(hue: Float, saturation: Float, value: Float, onChange: (Float, Float) -> Unit) {
    val description = stringResource(R.string.custom_color_area_cd)
    val ring = MaterialTheme.colorScheme.outline
    Canvas(
        Modifier.fillMaxWidth().height(150.dp).clip(RoundedCornerShape(8.dp)).semantics { contentDescription = description }
            .pointerInput(Unit) {
                awaitEachGesture {
                    val down = awaitFirstDown()
                    fun at(change: PointerInputChange) {
                        onChange((change.position.x / size.width).coerceIn(0f, 1f), 1f - (change.position.y / size.height).coerceIn(0f, 1f))
                        change.consume()
                    }
                    at(down)
                    drag(down.id) { at(it) }
                }
            },
    ) {
        drawRect(paletteColor(ColorHsv.toHex(hue, 1f, 1f)))
        drawRect(Brush.horizontalGradient(listOf(Color.White, Color.Transparent)))
        drawRect(Brush.verticalGradient(listOf(Color.Transparent, Color.Black)))
        val center = Offset(saturation * size.width, (1f - value) * size.height)
        drawCircle(ring, radius = 12.5.dp.toPx(), center = center, style = Stroke(1.dp.toPx()))
        drawCircle(Color.White, radius = 11.dp.toPx(), center = center, style = Stroke(3.dp.toPx()))
    }
}

/** La barra del arcoíris: el tono de 0 a 360 según dónde se ponga el dedo. */
@Composable
private fun HueBar(hue: Float, onChange: (Float) -> Unit) {
    val description = stringResource(R.string.custom_color_hue_cd)
    val rainbow = remember { (0..6).map { paletteColor(ColorHsv.toHex(it * 60f, 1f, 1f)) } }
    Canvas(
        Modifier.fillMaxWidth().height(32.dp).clip(RoundedCornerShape(16.dp)).semantics { contentDescription = description }
            .pointerInput(Unit) {
                awaitEachGesture {
                    val down = awaitFirstDown()
                    fun at(change: PointerInputChange) {
                        onChange((change.position.x / size.width).coerceIn(0f, 1f) * 359.99f)
                        change.consume()
                    }
                    at(down)
                    drag(down.id) { at(it) }
                }
            },
    ) {
        drawRect(Brush.horizontalGradient(rainbow))
        val radius = size.height / 2
        val x = (hue / 360f * size.width).coerceIn(radius, size.width - radius)
        drawCircle(Color.White, radius = radius - 2.dp.toPx(), center = Offset(x, radius), style = Stroke(3.dp.toPx()))
    }
}
