package app.aldune.ui

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.unit.dp
import app.aldune.R
import app.aldune.domain.NoteVersion
import java.time.ZoneId
import java.time.format.DateTimeFormatter
import java.time.format.FormatStyle
import kotlinx.coroutines.launch

/**
 * Historial de versiones de una nota: las copias anteriores con su fecha, la vista previa de la elegida y Restaurar. Son locales (no se
 * sincronizan) y restaurar guarda antes el texto actual, así que se puede deshacer. [onRestored] recibe el texto para el editor abierto.
 */
@Composable
fun VersionHistoryDialog(noteId: String, viewModel: NotesViewModel, onDismiss: () -> Unit, onRestored: (String) -> Unit) {
    var versions by remember { mutableStateOf<List<NoteVersion>?>(null) }
    var selected by remember { mutableStateOf<NoteVersion?>(null) }
    val scope = rememberCoroutineScope()
    LaunchedEffect(noteId) {
        versions = viewModel.versions(noteId)
        selected = versions?.firstOrNull()
    }
    val format = remember { DateTimeFormatter.ofLocalizedDateTime(FormatStyle.MEDIUM, FormatStyle.SHORT).withZone(ZoneId.systemDefault()) }
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text(stringResource(R.string.history_title)) },
        text = {
            Column {
                Text(stringResource(R.string.history_hint), style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                val list = versions
                if (list != null && list.isEmpty()) {
                    Text(stringResource(R.string.history_empty), Modifier.padding(top = 16.dp), color = MaterialTheme.colorScheme.onSurfaceVariant)
                } else if (list != null) {
                    LazyColumn(Modifier.fillMaxWidth().heightIn(max = 168.dp).padding(top = 8.dp), contentPadding = PaddingValues(vertical = 2.dp)) {
                        items(list, key = { it.id }) { version ->
                            val isSelected = version.id == selected?.id
                            Text(
                                format.format(version.savedAt),
                                Modifier.fillMaxWidth().clickable { selected = version }.padding(horizontal = 4.dp, vertical = 12.dp),
                                style = MaterialTheme.typography.bodyLarge,
                                color = if (isSelected) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.onSurface,
                            )
                        }
                    }
                    Text(
                        selected?.text?.take(PREVIEW_CHARS) ?: "",
                        Modifier.fillMaxWidth().heightIn(max = 120.dp).padding(top = 8.dp),
                        style = MaterialTheme.typography.bodyMedium,
                        maxLines = 6,
                    )
                }
            }
        },
        confirmButton = {
            TextButton(
                enabled = selected != null,
                onClick = { selected?.let { v -> scope.launch { viewModel.restoreVersion(noteId, v.id)?.let(onRestored); onDismiss() } } },
            ) { Text(stringResource(R.string.history_restore)) }
        },
        dismissButton = { TextButton(onClick = onDismiss) { Text(stringResource(R.string.history_close)) } },
    )
}

private const val PREVIEW_CHARS = 600
