package app.aldune.ui

import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.imePadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.safeDrawingPadding
import androidx.compose.foundation.text.BasicTextField
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.SolidColor
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.input.TextFieldValue
import androidx.compose.ui.unit.dp
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.compose.LifecycleEventEffect
import app.aldune.R

/**
 * Editor de una nota: texto plano con guardado automático (lo hace el `ViewModel`, ver `openDraft`). [key] es el id de la nota, o ""
 * para una nota nueva. Las ayudas de lista (casillas, viñetas, sangría) llegan en la tarea siguiente, sobre este mismo campo.
 */
@Composable
fun NoteEditorPane(key: String, viewModel: NotesViewModel, onBack: () -> Unit, showBack: Boolean) {
    var value by remember(key) { mutableStateOf(TextFieldValue("")) }
    var loaded by remember(key) { mutableStateOf(false) }

    LaunchedEffect(key) {
        value = TextFieldValue(viewModel.openDraft(key))
        loaded = true
    }
    // Al salir de la nota (otra nota, atrás, el panel se destruye) y al pasar la app a segundo plano.
    DisposableEffect(key) { onDispose { viewModel.flushDraft(key) } }
    LifecycleEventEffect(Lifecycle.Event.ON_STOP) { viewModel.flushAll() }

    Column(Modifier.fillMaxSize().safeDrawingPadding().imePadding()) {
        if (showBack) {
            Row(Modifier.fillMaxWidth()) {
                IconButton(onClick = onBack) { Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = stringResource(R.string.back)) }
            }
        }
        BasicTextField(
            value = value,
            onValueChange = {
                val changed = it.text != value.text
                value = it
                if (loaded && changed) viewModel.updateDraft(key, it.text)
            },
            modifier = Modifier.fillMaxSize().padding(horizontal = 16.dp, vertical = 8.dp),
            textStyle = MaterialTheme.typography.bodyLarge.copy(color = MaterialTheme.colorScheme.onBackground),
            cursorBrush = SolidColor(MaterialTheme.colorScheme.primary),
        )
    }
}
