package app.aldune.ui

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.ModalBottomSheet
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.semantics.heading
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.unit.dp
import app.aldune.R
import app.aldune.core.PasswordProblem
import app.aldune.domain.NoteListItem
import app.aldune.domain.ReorderPlan
import app.aldune.syncclient.ProtectionError
import app.aldune.syncclient.ProtectionResult
import kotlinx.coroutines.launch

/**
 * El menú de una nota de la lista, al mantenerla pulsada (o con «Más opciones» de TalkBack). Archivar, la papelera y borrar **no** están
 * aquí: ya se hacen deslizando la fila y con las acciones de accesibilidad; esto es lo que el deslizamiento no cubre.
 *
 * Proteger y quitar la protección reutilizan [PasswordDialog] sin pasar por el editor. Si la nota es la que está abierta al lado (tableta)
 * no se ofrecen: el editor tiene su propio estado de protección y se quedaría con el viejo; ahí se hacen desde su menú de opciones.
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun NoteActionsDialog(
    item: NoteListItem, openInEditor: Boolean, viewModel: NotesViewModel, onOpen: () -> Unit, onClose: () -> Unit,
    moves: Set<ReorderPlan.Direction> = emptySet(), onMove: (Int) -> Unit = {},
) {
    var options by remember { mutableStateOf<NotesViewModel.NoteOptions?>(null) }
    var naming by remember { mutableStateOf(false) }
    var password by remember { mutableStateOf<PasswordMode?>(null) }
    val scope = rememberCoroutineScope()

    // Las notas viven en el modelo, no en el elemento de la lista: el nombre visible actual se lee de ahí al abrir.
    var visibleName by remember(item.id) { mutableStateOf<String?>(null) }
    var visibleNameLoaded by remember(item.id) { mutableStateOf(false) }

    if (options == null && !naming && password == null) {
        // Panel inferior y no un diálogo en medio: cinco acciones de lista que se alcanzan con el pulgar y se cierran deslizando.
        ModalBottomSheet(onDismissRequest = onClose) {
                Column(Modifier.padding(horizontal = 24.dp).padding(bottom = 24.dp)) {
                    Text(
                        item.title, Modifier.padding(bottom = 4.dp).semantics { heading() }, style = MaterialTheme.typography.titleLarge,
                        maxLines = 1, overflow = TextOverflow.Ellipsis,
                    )
                    Action(stringResource(R.string.note_actions_open)) { onClose(); onOpen() }
                    // Subir/Bajar: lo mismo que arrastrar, sin arrastrar. Solo con la lista completa (con un filtro no se ofrecen).
                    if (ReorderPlan.Direction.UP in moves) Action(stringResource(R.string.note_move_up)) { onClose(); onMove(-1) }
                    if (ReorderPlan.Direction.DOWN in moves) Action(stringResource(R.string.note_move_down)) { onClose(); onMove(+1) }
                    Action(stringResource(if (item.pinnedToTop) R.string.note_unpin else R.string.note_pin)) {
                        onClose(); viewModel.setPinned(item.id, !item.pinnedToTop)
                    }
                    Action(stringResource(R.string.note_options)) {
                        scope.launch { viewModel.noteOptions(item.id)?.let { options = it } ?: onClose() }
                    }
                    if (item.isProtected) {
                        Action(stringResource(R.string.visible_name_menu)) {
                            scope.launch {
                                visibleName = viewModel.noteOptions(item.id)?.protectedTitle
                                visibleNameLoaded = true
                                naming = true
                            }
                        }
                    }
                    if (!openInEditor) {
                        Action(stringResource(if (item.isProtected) R.string.remove_protection else R.string.protect_note)) {
                            password = if (item.isProtected) PasswordMode.Remove else PasswordMode.Protect
                        }
                    }
                }
        }
    }

    options?.let { current ->
        NoteOptionsDialog(
            current, viewModel, onClose = { options = null; onClose() },
            onProtection = { options = null; password = if (current.isProtected) PasswordMode.Remove else PasswordMode.Protect },
            showProtection = !openInEditor,
        )
    }

    if (naming && visibleNameLoaded) {
        VisibleNameDialog(visibleName, onDismiss = { naming = false; onClose() }) { viewModel.setVisibleName(item.id, it) }
    }

    password?.let { mode ->
        PasswordDialog(mode, onDismiss = { password = null; onClose() }) { pw, confirmation, name ->
            when (mode) {
                PasswordMode.Protect -> when (val result = viewModel.protectNote(item.id, pw, confirmation, name)) {
                    is ProtectionResult.Ok -> null
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
                PasswordMode.Remove -> when (val result = viewModel.removeProtection(item.id, pw)) {
                    is ProtectionResult.Ok -> {
                        viewModel.dropProtected(item.id)
                        null
                    }
                    is ProtectionResult.Err -> when (result.error) {
                        ProtectionError.WrongPassword -> PasswordFailure.Wrong
                        is ProtectionError.UnreadableContent -> PasswordFailure.Unreadable
                        is ProtectionError.UnsupportedVersion -> PasswordFailure.Unsupported
                        else -> PasswordFailure.Changed
                    }
                }
                PasswordMode.Unlock -> null
            }
        }
    }
}

@Composable
private fun Action(label: String, onClick: () -> Unit) {
    Text(
        label, style = MaterialTheme.typography.bodyLarge,
        modifier = Modifier.fillMaxWidth().clickable(role = Role.Button, onClick = onClick).padding(vertical = 14.dp),
    )
}
