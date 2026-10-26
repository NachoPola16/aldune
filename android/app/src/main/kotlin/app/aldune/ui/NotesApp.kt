package app.aldune.ui

import app.aldune.capture.QuickList
import app.aldune.capture.CaptureNotification
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
import android.widget.Toast
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.unit.dp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import app.aldune.R
import app.aldune.capture.CaptureRequest
import kotlinx.coroutines.launch

/** Marca del panel de detalle para «nota nueva, todavía sin texto»: la nota no existe hasta que se escribe el primer carácter. */
private const val NEW_NOTE = ""

@Composable
fun NotesApp(viewModel: NotesViewModel) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    var showSettings by rememberSaveable { mutableStateOf(false) }
    // Una petición de captura saca de Ajustes: el editor está en la pantalla de notas. (No se atiende aquí: lo hace el andamio.)
    val capture by viewModel.capture.collectAsStateWithLifecycle()
    // Un código de perfil compartido lleva a Ajustes (a «Añadir perfil», que lo recoge); el resto de peticiones salen de Ajustes al editor.
    LaunchedEffect(capture) { if (capture != null) showSettings = capture is CaptureRequest.ProfileCode }
    val settings by viewModel.settings.collectAsStateWithLifecycle()
    viewModel.setPlaceholder(stringResource(R.string.new_note_title))
    viewModel.setProtectedTitle(stringResource(R.string.protected_note_title))
    Surface(Modifier.fillMaxSize(), color = MaterialTheme.colorScheme.background) {
        when (val s = state) {
            UiState.Loading -> Box(Modifier.fillMaxSize(), Alignment.Center) {
                CircularProgressIndicator()
            }
            is UiState.KeyLost -> KeyLostScreen(onStartOver = viewModel::startOver)
            is UiState.Ready -> {
                if (showSettings) SettingsScreen(viewModel, onClose = { showSettings = false })
                else NotesScaffold(s, viewModel, onOpenSettings = { showSettings = true })
                // Bienvenida solo en una instalación nueva: quien ya tiene notas (una actualización, o un perfil ya sincronizado) no la necesita.
                val firstRun = !settings.welcomeSeen && s.counts.values.sum() == 0
                LaunchedEffect(settings.welcomeSeen, firstRun) {
                    if (!settings.welcomeSeen && !firstRun) viewModel.updateSettings { it.copy(welcomeSeen = true) }
                }
                // El título de la lista rápida va guardado para la notificación: se actualiza si la nota se renombró.
                val context = LocalContext.current
                LaunchedEffect(s.items, settings.quickListId) {
                    val id = settings.quickListId ?: return@LaunchedEffect
                    val title = s.items.firstOrNull { it.id == id }?.title ?: return@LaunchedEffect
                    if (title != settings.quickListTitle) {
                        viewModel.updateSettings { it.copy(quickListTitle = title) }
                        CaptureNotification.sync(context, settings.captureNotification, QuickList(id, title))
                    }
                }
                if (firstRun && !showSettings) WelcomeDialog(onDismiss = { viewModel.updateSettings { it.copy(welcomeSeen = true) } })
            }
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

    // Acceso directo, mosaico, notificación: abren el editor con una nota nueva. Si ya se está en una nota nueva no se hace nada: reabrirla
    // tiraría su borrador y lo que se estuviera escribiendo no se guardaría.
    val capture by viewModel.capture.collectAsStateWithLifecycle()
    val context = LocalContext.current
    LaunchedEffect(capture) {
        when (capture) {
            CaptureRequest.NewNote -> if (navigator.currentDestination?.contentKey != NEW_NOTE) {
                viewModel.startNewNote()
                navigator.navigateTo(ListDetailPaneScaffoldRole.Detail, NEW_NOTE)
            }
            is CaptureRequest.SharedText -> {
                // Se marca atendida antes de guardar: si la actividad se recrea a mitad, no se crea una segunda nota. Pero eso cambia la
                // clave de este efecto y lo cancela, así que el trabajo va en `scope` (no depende de la clave): guardar y abrir la nota.
                val shared = capture as CaptureRequest.SharedText
                viewModel.captureHandled()
                scope.launch {
                    when (val outcome = viewModel.createShared(shared.text, shared.subject)) {
                        is NotesViewModel.SharedOutcome.Created -> navigator.navigateTo(ListDetailPaneScaffoldRole.Detail, outcome.id)
                        NotesViewModel.SharedOutcome.Empty -> Toast.makeText(context, R.string.share_empty, Toast.LENGTH_SHORT).show()
                        NotesViewModel.SharedOutcome.TooLong -> Toast.makeText(context, R.string.share_too_long, Toast.LENGTH_LONG).show()
                    }
                }
                return@LaunchedEffect
            }
            is CaptureRequest.OpenNote -> {
                val open = capture as CaptureRequest.OpenNote
                viewModel.captureHandled()
                scope.launch {
                    when (viewModel.resolveOpen(open.id)) {
                        // Una protegida también se abre: el editor pide su contraseña.
                        NotesViewModel.OpenOutcome.OPEN -> navigator.navigateTo(ListDetailPaneScaffoldRole.Detail, open.id)
                        NotesViewModel.OpenOutcome.GONE -> {
                            navigator.navigateTo(ListDetailPaneScaffoldRole.List)
                            Toast.makeText(context, R.string.note_gone, Toast.LENGTH_SHORT).show()
                        }
                    }
                }
                return@LaunchedEffect
            }
            // Lo recoge SyncSection en Ajustes y lo da por atendido.
            is CaptureRequest.ProfileCode, null -> return@LaunchedEffect
        }
        viewModel.captureHandled()
    }

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

/** Tres cosas, no un recorrido: crear una nota, hacer una lista y sincronizar. */
@Composable
private fun WelcomeDialog(onDismiss: () -> Unit) {
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text(stringResource(R.string.welcome_title)) },
        text = {
            Column(verticalArrangement = androidx.compose.foundation.layout.Arrangement.spacedBy(12.dp)) {
                Text(stringResource(R.string.welcome_add))
                Text(stringResource(R.string.welcome_lists))
                Text(stringResource(R.string.welcome_sync))
            }
        },
        confirmButton = { TextButton(onClick = onDismiss) { Text(stringResource(R.string.welcome_ok)) } },
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
