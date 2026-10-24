package app.aldune.ui

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.layout.Box
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Slider
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
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.input.KeyboardCapitalization
import androidx.compose.ui.unit.dp
import app.aldune.R
import app.aldune.text.CustomColor
import app.aldune.text.NoteColorContrast

/**
 * Color libre de una nota (equivalente de `CustomColorWindow` de Windows): vista previa, HEX y ajuste fino con RGB. Sin selector de espectro:
 * en una pantalla táctil los tres deslizadores hacen lo mismo con el dedo. «Usar este color» solo se activa si el HEX es válido y el
 * texto de la nota se podrá leer encima.
 */
@Composable
fun CustomColorDialog(initial: String, onDismiss: () -> Unit, onApply: (String) -> Unit) {
    var color by remember { mutableStateOf(CustomColor.normalize(initial) ?: "#F5E3B3") }
    var hexText by remember { mutableStateOf(color) }
    val (red, green, blue) = CustomColor.channels(color)!!
    val readable = NoteColorContrast.isReadable(color, NoteColorContrast.foregroundFor(color))
    val hexValid = CustomColor.normalize(hexText) != null

    fun setColor(next: String) {
        color = next
        hexText = next
    }

    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text(stringResource(R.string.custom_color_title)) },
        text = {
            Column(Modifier.fillMaxWidth(), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                Text(stringResource(R.string.custom_color_hint), style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                Box(
                    Modifier.fillMaxWidth().height(72.dp).clip(RoundedCornerShape(12.dp)).background(noteFace(color)).padding(12.dp),
                    contentAlignment = Alignment.CenterStart,
                ) {
                    Text(
                        stringResource(R.string.custom_color_preview) + "  " + color,
                        color = Color(android.graphics.Color.parseColor(NoteColorContrast.foregroundFor(color))),
                        style = MaterialTheme.typography.titleMedium,
                    )
                }
                OutlinedTextField(
                    value = hexText,
                    onValueChange = { text ->
                        hexText = text.take(7)
                        CustomColor.normalize(hexText)?.let { color = it }
                    },
                    label = { Text(stringResource(R.string.custom_color_hex)) },
                    singleLine = true,
                    isError = !hexValid,
                    supportingText = { Text(stringResource(if (!hexValid || !readable) R.string.custom_color_invalid else R.string.custom_color_readable)) },
                    keyboardOptions = KeyboardOptions(capitalization = KeyboardCapitalization.Characters),
                    modifier = Modifier.fillMaxWidth(),
                )
                Text(stringResource(R.string.custom_color_rgb), style = MaterialTheme.typography.labelLarge, modifier = Modifier.padding(top = 4.dp))
                Channel("R", red) { setColor(CustomColor.toHex(it, green, blue)) }
                Channel("G", green) { setColor(CustomColor.toHex(red, it, blue)) }
                Channel("B", blue) { setColor(CustomColor.toHex(red, green, it)) }
            }
        },
        confirmButton = {
            TextButton(onClick = { onApply(color) }, enabled = hexValid && readable) { Text(stringResource(R.string.custom_color_apply)) }
        },
        dismissButton = { TextButton(onClick = onDismiss) { Text(stringResource(R.string.cancel)) } },
    )
}

@Composable
private fun Channel(label: String, value: Int, onChange: (Int) -> Unit) {
    androidx.compose.foundation.layout.Row(verticalAlignment = Alignment.CenterVertically) {
        Text(label, Modifier.padding(end = 8.dp))
        Slider(value = value.toFloat(), onValueChange = { onChange(it.toInt()) }, valueRange = 0f..255f, modifier = Modifier.weight(1f))
        Text(value.toString(), Modifier.padding(start = 8.dp))
    }
}
