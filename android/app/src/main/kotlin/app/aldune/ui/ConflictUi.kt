package app.aldune.ui

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Warning
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.unit.dp
import app.aldune.R
import app.aldune.text.DiffLine

/**
 * La franja de un conflicto de sync, bajo la barra del editor: la otra versión en solo lectura (con las líneas que cambian resaltadas) y los
 * dos botones. [unlockedLines] es la otra versión ya abierta cuando estaba protegida. Una versión protegida se puede resolver **sin** abrirla.
 */
@Composable
fun ConflictStrip(
    view: NotesViewModel.ConflictView,
    unlockedLines: List<DiffLine>?,
    onKeep: () -> Unit,
    onUseOther: () -> Unit,
    onUnlockOther: () -> Unit,
    modifier: Modifier = Modifier,
) {
    val deleted = view.other is NotesViewModel.OtherVersion.Deleted
    // Sobre el fondo rojizo, el color de acento de los botones de texto no se lee: llevan el color del contenido de esa superficie.
    val onError = ButtonDefaults.textButtonColors(contentColor = MaterialTheme.colorScheme.onErrorContainer)
    Surface(modifier.fillMaxWidth().padding(horizontal = 12.dp, vertical = 4.dp), shape = RoundedCornerShape(12.dp), color = MaterialTheme.colorScheme.errorContainer) {
        Column(Modifier.padding(12.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
            Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                Icon(Icons.Default.Warning, contentDescription = null, Modifier.size(18.dp), tint = MaterialTheme.colorScheme.onErrorContainer)
                Text(
                    stringResource(if (deleted) R.string.conflict_strip_deleted else R.string.conflict_strip_title),
                    style = MaterialTheme.typography.titleSmall, color = MaterialTheme.colorScheme.onErrorContainer,
                )
            }
            when (val other = view.other) {
                NotesViewModel.OtherVersion.Deleted -> Unit
                NotesViewModel.OtherVersion.Protected -> {
                    if (unlockedLines != null) OtherVersionLines(unlockedLines) else {
                        Text(stringResource(R.string.conflict_other_version) + ": " + stringResource(R.string.conflict_protected_version), color = MaterialTheme.colorScheme.onErrorContainer)
                        TextButton(onClick = onUnlockOther, colors = onError) { Text(stringResource(R.string.conflict_unlock_version)) }
                    }
                }
                is NotesViewModel.OtherVersion.Text -> OtherVersionLines(other.lines)
            }
            Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.End) {
                TextButton(onClick = onKeep, colors = onError) { Text(stringResource(R.string.conflict_keep)) }
                TextButton(onClick = onUseOther, colors = onError) { Text(stringResource(if (deleted) R.string.conflict_delete_it else R.string.conflict_use_other)) }
            }
        }
    }
}

/** El texto de la otra versión, con un tope de alto y desplazable; la línea que cambia lleva fondo. */
@Composable
private fun OtherVersionLines(lines: List<DiffLine>) {
    Text(stringResource(R.string.conflict_other_version), style = MaterialTheme.typography.labelMedium, color = MaterialTheme.colorScheme.onErrorContainer)
    Column(
        Modifier.fillMaxWidth().heightIn(max = 160.dp).background(MaterialTheme.colorScheme.surface, RoundedCornerShape(8.dp)).padding(8.dp)
            .verticalScroll(rememberScrollState()),
    ) {
        for (line in lines) {
            Text(
                line.text.ifEmpty { " " }, style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurface,
                modifier = Modifier.fillMaxWidth().then(if (line.differs) Modifier.background(MaterialTheme.colorScheme.tertiaryContainer) else Modifier),
            )
        }
    }
}

/** El aviso de la lista: hay conflictos de notas que ya no existen aquí. Tocarlo abre [OrphanConflictsDialog]. */
@Composable
fun OrphanConflictsBanner(count: Int, onClick: () -> Unit) {
    Surface(
        Modifier.fillMaxWidth().padding(horizontal = 16.dp, vertical = 4.dp).clickable(onClick = onClick),
        shape = RoundedCornerShape(12.dp), color = MaterialTheme.colorScheme.errorContainer,
    ) {
        Row(Modifier.padding(12.dp), verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            Icon(Icons.Default.Warning, contentDescription = null, Modifier.size(18.dp), tint = MaterialTheme.colorScheme.onErrorContainer)
            Text(stringResource(R.string.conflicts_banner, count), color = MaterialTheme.colorScheme.onErrorContainer, style = MaterialTheme.typography.titleSmall)
        }
    }
}

/** Los conflictos sin nota (ganó un borrado): recuperar la nota editada o descartarla. Ambas se resuelven sin abrir una versión protegida. */
@Composable
fun OrphanConflictsDialog(
    items: List<OrphanConflict>,
    onRecover: (String) -> Unit,
    onDiscard: (String) -> Unit,
    onClose: () -> Unit,
) {
    AlertDialog(
        onDismissRequest = onClose,
        title = { Text(stringResource(R.string.conflicts_title)) },
        text = {
            Column(Modifier.verticalScroll(rememberScrollState()), verticalArrangement = Arrangement.spacedBy(4.dp)) {
                Text(stringResource(R.string.conflicts_orphan_hint), style = MaterialTheme.typography.bodySmall)
                for (item in items) {
                    HorizontalDivider(Modifier.padding(vertical = 4.dp))
                    Text(item.title, style = MaterialTheme.typography.titleSmall)
                    Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.End) {
                        TextButton(onClick = { onDiscard(item.noteId) }) { Text(stringResource(R.string.conflict_discard)) }
                        TextButton(onClick = { onRecover(item.noteId) }) { Text(stringResource(R.string.conflict_recover)) }
                    }
                }
            }
        },
        confirmButton = { TextButton(onClick = onClose) { Text(stringResource(R.string.done)) } },
    )
}
