package app.aldune.ui

import androidx.activity.compose.BackHandler
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.adaptive.ExperimentalMaterial3AdaptiveApi
import androidx.compose.material3.adaptive.layout.AnimatedPane
import androidx.compose.material3.adaptive.layout.ListDetailPaneScaffoldRole
import androidx.compose.material3.adaptive.layout.PaneAdaptedValue
import androidx.compose.material3.adaptive.navigation.NavigableListDetailPaneScaffold
import androidx.compose.material3.adaptive.navigation.rememberListDetailPaneScaffoldNavigator
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.unit.dp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import app.aldune.R
import kotlinx.coroutines.launch

/** Marca del panel de detalle para «nota nueva, todavía sin texto»: la nota no existe hasta que se escribe el primer carácter. */
private const val NEW_NOTE = ""

@Composable
fun NotesApp(viewModel: NotesViewModel) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    var showSettings by rememberSaveable { mutableStateOf(false) }
    viewModel.setPlaceholder(stringResource(R.string.new_note_title))
    Surface(Modifier.fillMaxSize(), color = MaterialTheme.colorScheme.background) {
        when (val s = state) {
            UiState.Loading -> Box(Modifier.fillMaxSize(), Alignment.Center) {
                CircularProgressIndicator()
            }
            is UiState.KeyLost -> KeyLostScreen(onStartOver = viewModel::startOver)
            is UiState.Ready ->
                if (showSettings) SettingsScreen(viewModel, onClose = { showSettings = false })
                else NotesScaffold(s, viewModel, onOpenSettings = { showSettings = true })
        }
    }
}

@OptIn(ExperimentalMaterial3AdaptiveApi::class, ExperimentalMaterial3Api::class)
@Composable
private fun NotesScaffold(state: UiState.Ready, viewModel: NotesViewModel, onOpenSettings: () -> Unit) {
    val navigator = rememberListDetailPaneScaffoldNavigator<String>()
    val scope = rememberCoroutineScope()
    val openNote: (String) -> Unit = { id -> scope.launch { navigator.navigateTo(ListDetailPaneScaffoldRole.Detail, id) } }
    val detailVisible = navigator.scaffoldValue[ListDetailPaneScaffoldRole.Detail] == PaneAdaptedValue.Expanded
    val listVisible = navigator.scaffoldValue[ListDetailPaneScaffoldRole.List] == PaneAdaptedValue.Expanded

    // En pantalla compacta, atrás desde el editor vuelve a la lista; en ancha no hay a dónde volver.
    BackHandler(enabled = detailVisible && !listVisible) { scope.launch { navigator.navigateBack() } }

    NavigableListDetailPaneScaffold(
        navigator = navigator,
        listPane = {
            AnimatedPane {
                NoteListPane(
                    state = state, viewModel = viewModel,
                    selectedId = navigator.currentDestination?.contentKey,
                    onOpen = openNote, onNew = { viewModel.startNewNote(); openNote(NEW_NOTE) }, onOpenSettings = onOpenSettings,
                )
            }
        },
        detailPane = {
            AnimatedPane {
                val id = navigator.currentDestination?.contentKey
                if (id == null) {
                    Box(Modifier.fillMaxSize(), Alignment.Center) {
                        Text(stringResource(R.string.select_a_note), color = MaterialTheme.colorScheme.onSurfaceVariant)
                    }
                } else {
                    NoteEditorPane(
                        key = id, viewModel = viewModel,
                        onBack = { scope.launch { navigator.navigateBack() } },
                        showBack = !listVisible,
                    )
                }
            }
        },
    )
}

@Composable
private fun KeyLostScreen(onStartOver: () -> Unit) {
    var confirming by remember { mutableStateOf(false) }
    Column(Modifier.fillMaxSize().padding(24.dp), verticalArrangement = androidx.compose.foundation.layout.Arrangement.Center) {
        Text(stringResource(R.string.key_lost_title), style = MaterialTheme.typography.headlineSmall)
        Text(stringResource(R.string.key_lost_body), Modifier.padding(vertical = 16.dp))
        Row(Modifier.fillMaxWidth()) {
            Button(onClick = { confirming = true }) { Text(stringResource(R.string.start_over)) }
        }
    }
    if (confirming) {
        AlertDialog(
            onDismissRequest = { confirming = false },
            title = { Text(stringResource(R.string.start_over_title)) },
            text = { Text(stringResource(R.string.start_over_body)) },
            confirmButton = {
                TextButton(onClick = { confirming = false; onStartOver() }) { Text(stringResource(R.string.start_over_confirm)) }
            },
            dismissButton = { TextButton(onClick = { confirming = false }) { Text(stringResource(R.string.cancel)) } },
        )
    }
}
