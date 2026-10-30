package app.aldune.ui

import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.gestures.awaitEachGesture
import androidx.compose.foundation.gestures.awaitFirstDown
import androidx.compose.foundation.gestures.awaitLongPressOrCancellation
import androidx.compose.foundation.gestures.detectDragGesturesAfterLongPress
import androidx.compose.ui.focus.focusProperties
import androidx.compose.ui.input.pointer.PointerEventPass
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.lazy.rememberLazyListState
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.mutableFloatStateOf
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.hapticfeedback.HapticFeedbackType
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.platform.LocalHapticFeedback
import androidx.compose.ui.zIndex
import app.aldune.domain.ReorderPlan
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.imePadding
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.focusable
import androidx.compose.ui.focus.focusRequester
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.first
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.foundation.lazy.LazyRow
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.Check
import androidx.compose.material.icons.filled.Lock
import androidx.compose.material.icons.filled.Notifications
import androidx.compose.material.icons.filled.Warning
import androidx.compose.material.icons.filled.Close
import androidx.compose.material.icons.filled.Search
import androidx.compose.material.icons.filled.Settings
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.FilterChip
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.FloatingActionButton
import androidx.compose.material3.pulltorefresh.PullToRefreshBox
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import app.aldune.syncclient.StatusIcon
import app.aldune.syncclient.SyncEvent
import app.aldune.syncclient.formatStatus
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.SnackbarDuration
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.SnackbarResult
import androidx.compose.material3.SwipeToDismissBox
import androidx.compose.material3.SwipeToDismissBoxValue
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.rememberSwipeToDismissBoxState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.semantics.CustomAccessibilityAction
import androidx.compose.ui.semantics.LiveRegionMode
import androidx.compose.ui.semantics.customActions
import androidx.compose.ui.semantics.liveRegion
import androidx.compose.ui.semantics.stateDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.semantics.heading
import androidx.compose.ui.unit.dp
import app.aldune.R
import app.aldune.domain.ReminderLabel
import app.aldune.domain.NoteListItem
import app.aldune.domain.NoteState
import app.aldune.text.AndroidReleaseUpdate
import app.aldune.text.NoteChannels
import app.aldune.text.NoteColorContrast
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.StrokeCap
import androidx.compose.ui.graphics.luminance
import androidx.compose.ui.graphics.StrokeJoin
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.graphics.drawscope.scale
import androidx.compose.ui.semantics.clearAndSetSemantics
import app.aldune.text.NoteDisplayColor
import app.aldune.text.NoteSyncSignal
import androidx.compose.ui.graphics.RectangleShape
import androidx.compose.ui.text.font.FontFamily
import kotlinx.coroutines.launch

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun NoteListPane(
    state: UiState.Ready,
    viewModel: NotesViewModel,
    selectedId: String?,
    onOpen: (String) -> Unit,
    onNew: () -> Unit,
    onOpenSettings: () -> Unit,
) {
    val snackbar = remember { SnackbarHostState() }
    val scope = rememberCoroutineScope()
    val rest = remember { androidx.compose.ui.focus.FocusRequester() }
    var searchTouched by remember { mutableStateOf(false) }
    val windowInfo = androidx.compose.ui.platform.LocalWindowInfo.current
    val keyboard = androidx.compose.ui.platform.LocalSoftwareKeyboardController.current
    androidx.compose.runtime.LaunchedEffect(Unit) {
        // El foco inicial lo reparte Android cuando la ventana lo recibe, un rato después de componer: se espera a eso y se lleva al
        // elemento invisible (no a «nada»: sin destino, el sistema se lo vuelve a dar al buscador).
        androidx.compose.runtime.snapshotFlow { windowInfo.isWindowFocused }.first { it }
        kotlinx.coroutines.delay(100)
        runCatching { rest.requestFocus() }
        keyboard?.hide()
    }
    var deleting by remember { mutableStateOf<NoteListItem?>(null) }
    val controller by viewModel.sync.collectAsStateWithLifecycle()
    val hasProfile = controller?.profiles?.collectAsStateWithLifecycle()?.value?.isNotEmpty() == true
    val status = controller?.status?.collectAsStateWithLifecycle()?.value
    val conflicted = controller?.conflicted?.collectAsStateWithLifecycle()?.value ?: emptySet()
    val bases = controller?.bases?.collectAsStateWithLifecycle()?.value ?: emptyMap()
    val look = LocalNoteLook.current
    val settingsNow by viewModel.settings.collectAsStateWithLifecycle()
    var showSyncDetail by remember { mutableStateOf(false) }
    var showOrphans by remember { mutableStateOf(false) }
    // La nota cuyo menú (mantener pulsada) está abierto.
    var menuFor by remember { mutableStateOf<NoteListItem?>(null) }

    // Reordenar (arrastrando o con Subir/Bajar) solo con la lista completa (sin búsqueda ni etiqueta): con un filtro, las vecinas que se ven no
    // son las vecinas reales y el orden no se puede decidir.
    val reorderable = state.query.isBlank() && state.tag == null && state.items.size > 1 && settingsNow.listOrder == 0
    val context = androidx.compose.ui.platform.LocalContext.current

    /** Sube o baja la nota una posición (la alternativa al arrastre) y lo anuncia: «Movida a la posición N de M». */
    fun moveBy(id: String, delta: Int) {
        val ids = state.items.map { it.id }
        val step = ReorderPlan.step(ids, id, delta) ?: return
        viewModel.moveNote(id, step.neighbours.before, step.neighbours.after)
        scope.launch {
            snackbar.currentSnackbarData?.dismiss()
            snackbar.showSnackbar(context.getString(R.string.note_moved, step.position, ids.size), duration = SnackbarDuration.Short)
        }
    }

    val archivedMsg = stringResource(R.string.note_archived)
    val trashedMsg = stringResource(R.string.note_trashed)
    val restoredMsg = stringResource(R.string.note_restored)
    val undoLabel = stringResource(R.string.undo)

    fun announce(message: String, undo: () -> Unit) {
        scope.launch {
            snackbar.currentSnackbarData?.dismiss()
            if (snackbar.showSnackbar(message, undoLabel, duration = SnackbarDuration.Short) == SnackbarResult.ActionPerformed) undo()
        }
    }

    Scaffold(
        containerColor = MaterialTheme.colorScheme.background,
        snackbarHost = { SnackbarHost(snackbar) },
        // Buscar, Ajustes y «+» abajo, a la altura del pulgar: arriba solo quedan las pestañas y el estado de la sync. Antes el buscador y el
        // engranaje estaban arriba y el «+» abajo, así que para buscar había que soltar la mano de donde se sujeta el móvil.
        bottomBar = {
            Row(
                Modifier.fillMaxWidth().navigationBarsPadding().imePadding().padding(horizontal = 16.dp, vertical = 8.dp),
                verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(8.dp),
            ) {
                // El buscador no puede tomar el foco hasta que se toca: si lo toma al arrancar (primer elemento enfocable), Android abre el
                // teclado, y ocultarlo después es una carrera que a veces se pierde. El toque lo habilita antes de que el campo pida el foco.
                SearchField(
                    state.query, viewModel::setQuery,
                    Modifier.weight(1f)
                        .focusProperties { canFocus = searchTouched }
                        .pointerInput(Unit) {
                            awaitEachGesture {
                                awaitFirstDown(requireUnconsumed = false, pass = PointerEventPass.Initial)
                                searchTouched = true
                            }
                        },
                )
                IconButton(onClick = onOpenSettings) { Icon(Icons.Default.Settings, contentDescription = stringResource(R.string.settings)) }
                FloatingActionButton(onClick = onNew, shape = if (look.squareNewButton) RectangleShape else RoundedCornerShape(16.dp)) { Icon(Icons.Default.Add, contentDescription = stringResource(R.string.new_note)) }
            }
        },
    ) { padding ->
        Column(Modifier.padding(padding).fillMaxSize()) {
            // Al arrancar, Android da el foco inicial al primer elemento enfocable de la ventana y, si es un campo de texto, abre el teclado
            // (tapando el «+»). Este elemento invisible va primero para quedárselo; el buscador solo toma el foco si se toca.
            Box(Modifier.size(0.dp).focusRequester(rest).focusable())
            Row(Modifier.fillMaxWidth().padding(top = 8.dp), verticalAlignment = Alignment.CenterVertically) {
                Box(Modifier.weight(1f)) { StateTabs(state, viewModel::setFilter) }
                if (hasProfile && status != null) {
                    val line = formatStatus(status, rememberNow())
                    IconButton(onClick = { showSyncDetail = true }, modifier = Modifier.semantics { liveRegion = LiveRegionMode.Polite }) { SyncIcon(line.icon, stringResource(R.string.sync_status_cd)) }
                }
            }
            if (state.allTags.isNotEmpty()) TagRow(state, viewModel::setTag)
            if (state.orphans.isNotEmpty()) OrphanConflictsBanner(state.orphans.size) { showOrphans = true }
            // Una versión nueva de Android en GitHub (una vez al día, desactivable en Ajustes → General): solo avisa y abre la página.
            val pendingUpdate by viewModel.update.collectAsStateWithLifecycle()
            androidx.compose.runtime.LaunchedEffect(settingsNow.checkUpdates) { viewModel.checkForUpdates(currentVersionName(context)) }
            pendingUpdate?.let { found ->
                UpdateNotice(
                    found.version, onDismiss = { viewModel.dismissUpdate(found.version) },
                    onOpen = { context.startActivity(android.content.Intent(android.content.Intent.ACTION_VIEW, android.net.Uri.parse(AndroidReleaseUpdate.PAGE + found.tag))) },
                )
            }
            // Sin perfil de sync, las notas solo viven en el móvil: se avisa una vez que haya alguna (en la bienvenida ya se habla de la sync).
            if (!hasProfile && !settingsNow.backupNoticeDismissed && state.counts.values.sum() > 0) {
                NoBackupNotice(onSettings = onOpenSettings, onDismiss = { viewModel.updateSettings { it.copy(backupNoticeDismissed = true) } })
            }

            // Tirar hacia abajo sincroniza (spec madre §2.1). Sin perfil no hace nada: la política no lanza pasadas sin perfil.
            PullToRefreshBox(
                isRefreshing = status?.running == true,
                onRefresh = { viewModel.syncEvent(SyncEvent.PullToRefresh) },
                modifier = Modifier.fillMaxSize(),
            ) {
            if (state.items.isEmpty()) {
                EmptyMessage(state)
            } else {
                // Reordenar: mantener pulsada una nota y arrastrarla (o Subir/Bajar, para quien no puede arrastrar); `reorderable` arriba.
                val listState = rememberLazyListState()
                val haptic = LocalHapticFeedback.current
                var order by remember { mutableStateOf(state.items) }
                var draggedId by remember { mutableStateOf<String?>(null) }
                var dragDelta by remember { mutableFloatStateOf(0f) }
                var dragStartIds by remember { mutableStateOf<List<String>>(emptyList()) }
                // Mientras se arrastra manda el orden local; fuera del arrastre, el de la base.
                LaunchedEffect(state.items) { if (draggedId == null) order = state.items }
                // La fila que señala el widget (una protegida): se lleva a la vista.
                val highlight by viewModel.highlight.collectAsStateWithLifecycle()
                LaunchedEffect(highlight, order) {
                    val index = order.indexOfFirst { it.id == highlight }
                    if (highlight != null && index >= 0) listState.animateScrollToItem(index)
                }

                fun dragStart(id: String) {
                    haptic.performHapticFeedback(HapticFeedbackType.LongPress)
                    draggedId = id
                    dragDelta = 0f
                    dragStartIds = order.map { it.id }
                }

                fun dragBy(dy: Float) {
                    val id = draggedId ?: return
                    dragDelta += dy
                    val visible = listState.layoutInfo.visibleItemsInfo
                    // Tras un intercambio, la pantalla tarda un fotograma en recolocarse: sin esperar, el mismo gesto intercambiaría dos veces.
                    val shown = visible.sortedBy { it.offset }.map { it.key }
                    if (shown != order.map { it.id }.filter { it in shown }) return
                    val me = visible.firstOrNull { it.key == id } ?: return
                    val center = me.offset + me.size / 2f + dragDelta
                    val target = visible.firstOrNull { it.key != id && center >= it.offset && center <= it.offset + it.size } ?: return
                    val from = order.indexOfFirst { it.id == id }
                    val to = order.indexOfFirst { it.id == target.key }
                    if (from < 0 || to < 0) return
                    order = order.toMutableList().apply { add(to, removeAt(from)) }
                    // La nota arrastrada cambia de sitio en la lista: su desplazamiento se compensa para que siga bajo el dedo.
                    val newOffset = if (to < from) target.offset else target.offset + target.size - me.size
                    dragDelta -= (newOffset - me.offset)
                }

                fun dragEnd() {
                    val id = draggedId ?: return
                    val plan = ReorderPlan.neighbours(dragStartIds, dragStartIds.indexOf(id), order.indexOfFirst { it.id == id })
                    draggedId = null
                    dragDelta = 0f
                    if (plan != null) viewModel.moveNote(id, plan.before, plan.after) else order = state.items
                }

                // El margen de abajo deja la última nota por encima del «+» al llegar al final de la lista.
                LazyColumn(
                    Modifier.fillMaxSize(), state = listState, verticalArrangement = Arrangement.spacedBy(8.dp),
                    contentPadding = androidx.compose.foundation.layout.PaddingValues(bottom = 16.dp),
                ) {
                    items(order, key = { it.id }) { item ->
                        val dragging = draggedId == item.id
                        // Mantener pulsada una nota: arrastrar la reordena y soltar sin mover abre su menú. En los dos casos el «soltar» no debe
                        // abrir la nota, que es lo que haría el toque de la fila al levantar el dedo.
                        val hold = remember(item.id) { PressHold() }
                        SwipeableRow(
                            modifier = (if (dragging) Modifier.zIndex(1f).graphicsLayer { translationY = dragDelta } else Modifier.animateItem())
                                .then(
                                    if (reorderable) Modifier.pointerInput(item.id) {
                                        detectDragGesturesAfterLongPress(
                                            onDragStart = {
                                                hold.active = true; hold.travelled = 0f; hold.menuShown = false; dragStart(item.id)
                                                // Si el dedo sigue quieto un momento tras la pulsación larga no se quiere recolocar la nota: el menú se
                                                // abre sin esperar a levantarlo. Si se mueve antes, es un arrastre y el menú no sale.
                                                scope.launch {
                                                    delay(MENU_HOLD_MS)
                                                    if (hold.active && !hold.menuShown && hold.travelled <= viewConfiguration.touchSlop) {
                                                        hold.menuShown = true
                                                        menuFor = item
                                                    }
                                                }
                                            },
                                            onDrag = { change, amount ->
                                                change.consume()
                                                hold.travelled += kotlin.math.abs(amount.x) + kotlin.math.abs(amount.y)
                                                if (!hold.menuShown) dragBy(amount.y)
                                            },
                                            // Un dedo del todo quieto no genera ningún movimiento y Compose lo da por «cancelado», no por terminado:
                                            // sin movimiento, las dos salidas abren el menú.
                                            onDragEnd = {
                                                val moved = hold.travelled > viewConfiguration.touchSlop
                                                dragEnd()
                                                if (!moved && !hold.menuShown) menuFor = item
                                                hold.active = false
                                            },
                                            onDragCancel = {
                                                val moved = hold.travelled > viewConfiguration.touchSlop
                                                dragEnd()
                                                if (!moved && !hold.menuShown) menuFor = item
                                                hold.active = false
                                            },
                                        )
                                    } else Modifier.pointerInput(item.id) {
                                        // Con un filtro no se puede reordenar: mantener pulsada abre el menú directamente, al cumplirse el tiempo.
                                        awaitEachGesture {
                                            val down = awaitFirstDown(requireUnconsumed = false)
                                            if (awaitLongPressOrCancellation(down.id) != null) {
                                                hold.active = true
                                                haptic.performHapticFeedback(HapticFeedbackType.LongPress)
                                                menuFor = item
                                                do {
                                                    val event = awaitPointerEvent()
                                                } while (event.changes.any { it.pressed })
                                                hold.active = false
                                            }
                                        }
                                    }
                                ),
                            item = item, selected = item.id == selectedId || item.id == highlight, conflicted = item.id in conflicted, reminder = state.reminders[item.id],
                            onOpen = { if (!hold.active) onOpen(item.id) }, onMenu = { menuFor = item },
                            moves = ReorderPlan.available(order.map { it.id }, item.id, reorderable), onMove = { delta -> moveBy(item.id, delta) },
                            channel = order.indexOfFirst { it.id == item.id },
                            signal = if (look.syncSignal) NoteSyncSignal.of(hasProfile, item.id in conflicted, bases[item.id], item.updatedAt) else NoteSyncSignal.State.HIDDEN,
                            onArchive = { viewModel.archive(item.id); announce(archivedMsg) { viewModel.restore(item.id) } },
                            onTrash = { viewModel.trash(item.id); announce(trashedMsg) { viewModel.restore(item.id) } },
                            onRestore = { viewModel.restore(item.id); announce(restoredMsg) { viewModel.trash(item.id) } },
                            onDelete = { deleting = item },
                        )
                    }
                }
            }
            }
        }
    }

    if (showSyncDetail && controller != null && status != null) {
        val line = formatStatus(status, rememberNow())
        AlertDialog(
            onDismissRequest = { showSyncDetail = false },
            title = { Text(stringResource(R.string.sync_title), Modifier.semantics { heading() }) },
            text = {
                Column {
                    Text(statusText(line))
                    line.failure?.detail?.let {
                        Text(it, Modifier.padding(top = 8.dp), style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                    }
                }
            },
            confirmButton = {
                TextButton(enabled = !status.running, onClick = { controller?.request(SyncEvent.ManualSync); showSyncDetail = false }) {
                    Text(stringResource(R.string.sync_now))
                }
            },
            dismissButton = { TextButton(onClick = { showSyncDetail = false }) { Text(stringResource(R.string.back)) } },
        )
    }

    if (showOrphans) {
        if (state.orphans.isEmpty()) {
            showOrphans = false
        } else {
            val context = androidx.compose.ui.platform.LocalContext.current
            fun resolve(id: String, useOther: Boolean) {
                scope.launch {
                    if (!viewModel.resolveConflict(id, useOther)) {
                        android.widget.Toast.makeText(context, R.string.conflict_failed, android.widget.Toast.LENGTH_LONG).show()
                    }
                }
            }
            OrphanConflictsDialog(
                state.orphans, onRecover = { resolve(it, true) }, onDiscard = { resolve(it, false) }, onClose = { showOrphans = false },
            )
        }
    }

    menuFor?.let { item ->
        NoteActionsDialog(
            item, openInEditor = item.id == selectedId, viewModel = viewModel, onOpen = { onOpen(item.id) }, onClose = { menuFor = null },
            moves = ReorderPlan.available(state.items.map { it.id }, item.id, reorderable), onMove = { delta -> moveBy(item.id, delta) },
        )
    }

    deleting?.let { item ->
        AlertDialog(
            onDismissRequest = { deleting = null },
            title = { Text(stringResource(R.string.delete_forever_title), Modifier.semantics { heading() }) },
            text = { Text(item.title + "\n\n" + stringResource(R.string.delete_forever_body)) },
            confirmButton = {
                TextButton(onClick = { viewModel.deleteForever(item.id); deleting = null }) { Text(stringResource(R.string.delete_forever)) }
            },
            dismissButton = { TextButton(onClick = { deleting = null }) { Text(stringResource(R.string.cancel)) } },
        )
    }
}

@Composable
private fun SearchField(query: String, onChange: (String) -> Unit, modifier: Modifier = Modifier) {
    OutlinedTextField(
        value = query, onValueChange = onChange, singleLine = true,
        modifier = modifier,
        placeholder = { Text(stringResource(R.string.search_hint)) },
        leadingIcon = { Icon(Icons.Default.Search, contentDescription = null) },
        trailingIcon = {
            if (query.isNotEmpty()) {
                IconButton(onClick = { onChange("") }) { Icon(Icons.Default.Close, contentDescription = stringResource(R.string.clear_search)) }
            }
        },
        keyboardOptions = KeyboardOptions(imeAction = ImeAction.Search),
        shape = if (LocalNoteLook.current.squareCorners) RectangleShape else RoundedCornerShape(28.dp),
    )
}

@Composable
private fun StateTabs(state: UiState.Ready, onSelect: (NoteState) -> Unit) {
    // Con scroll: en una ventana muy estrecha (burbuja, multiventana) la última pestaña se comprimía hasta quedar en una tira de letras apiladas.
    Row(Modifier.horizontalScroll(rememberScrollState()).padding(horizontal = 16.dp), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
        for ((filter, label) in listOf(
            NoteState.ACTIVE to R.string.tab_active, NoteState.ARCHIVED to R.string.tab_archived, NoteState.TRASHED to R.string.tab_trash,
        )) {
            val count = state.counts[filter] ?: 0
            FilterChip(
                selected = state.filter == filter, onClick = { onSelect(filter) },
                label = { Text(stringResource(label) + if (count > 0) " $count" else "") },
            )
        }
    }
}

@Composable
private fun TagRow(state: UiState.Ready, onSelect: (String?) -> Unit) {
    LazyRow(
        Modifier.padding(top = 4.dp), contentPadding = androidx.compose.foundation.layout.PaddingValues(horizontal = 16.dp),
        horizontalArrangement = Arrangement.spacedBy(8.dp),
    ) {
        items(state.allTags) { tag ->
            val selected = state.tag.equals(tag, ignoreCase = true)
            FilterChip(selected = selected, onClick = { onSelect(if (selected) null else tag) }, label = { Text("#$tag") })
        }
    }
}

@Composable
private fun EmptyMessage(state: UiState.Ready) {
    val text = when {
        state.query.isNotBlank() || state.tag != null -> R.string.empty_search
        state.filter == NoteState.ACTIVE -> R.string.empty_active
        state.filter == NoteState.ARCHIVED -> R.string.empty_archived
        else -> R.string.empty_trash
    }
    // Desplazable aunque no haya nada que desplazar: sin eso, «tirar para sincronizar» no engancha en una lista vacía.
    Box(Modifier.fillMaxSize().verticalScroll(rememberScrollState()).padding(32.dp), Alignment.Center) {
        Text(stringResource(text), color = MaterialTheme.colorScheme.onSurfaceVariant)
    }
}

/**
 * Una fila que se desliza: en Notas, a la derecha la manda a la papelera y a la izquierda la archiva; en Archivadas, a la derecha la
 * restaura y a la izquierda la manda a la papelera; en la papelera, a la derecha la restaura y a la izquierda pide confirmar el borrado.
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun SwipeableRow(
    modifier: Modifier,
    item: NoteListItem, selected: Boolean, conflicted: Boolean, reminder: java.time.Instant?, onOpen: () -> Unit, signal: NoteSyncSignal.State, channel: Int,
    onMenu: () -> Unit, moves: Set<ReorderPlan.Direction>, onMove: (Int) -> Unit,
    onArchive: () -> Unit, onTrash: () -> Unit, onRestore: () -> Unit, onDelete: () -> Unit,
) {
    val dismiss = rememberSwipeToDismissBoxState(
        confirmValueChange = { value ->
            when (value) {
                SwipeToDismissBoxValue.StartToEnd -> when (item.state) {
                    NoteState.ACTIVE -> onTrash()
                    else -> onRestore()
                }
                SwipeToDismissBoxValue.EndToStart -> when (item.state) {
                    NoteState.ACTIVE -> onArchive()
                    NoteState.ARCHIVED -> onTrash()
                    NoteState.TRASHED -> { onDelete(); return@rememberSwipeToDismissBoxState false }
                }
                SwipeToDismissBoxValue.Settled -> Unit
            }
            value != SwipeToDismissBoxValue.Settled
        },
    )
    // El estado del deslizamiento se guarda por id de nota (rememberSaveable): una nota que acaba de archivarse o ir a la papelera volvía a
    // aparecer en su nueva pestaña ya «descartada», fuera de la pantalla. Al entrar en la composición se repone.
    LaunchedEffect(item.state) { dismiss.snapTo(SwipeToDismissBoxValue.Settled) }
    // Deslizar no es posible con TalkBack ni con un interruptor: las mismas salidas del deslizamiento, como acciones del menú de la nota.
    val archiveLabel = stringResource(R.string.archive)
    val trashLabel = stringResource(R.string.move_to_trash)
    val restoreLabel = stringResource(R.string.restore)
    val deleteLabel = stringResource(R.string.delete_forever)
    val moreLabel = stringResource(R.string.note_actions_more)
    val upLabel = stringResource(R.string.note_move_up)
    val downLabel = stringResource(R.string.note_move_down)
    val openLabel = stringResource(R.string.note_open_in_editor)
    // Subir/Bajar: la alternativa al arrastre (WCAG 2.5.7) para quien usa TalkBack, un interruptor o no puede arrastrar.
    val moveActions = buildList {
        if (ReorderPlan.Direction.UP in moves) add(CustomAccessibilityAction(upLabel) { onMove(-1); true })
        if (ReorderPlan.Direction.DOWN in moves) add(CustomAccessibilityAction(downLabel) { onMove(+1); true })
    }
    val actions = moveActions + when (item.state) {
        NoteState.ACTIVE -> listOf(
            CustomAccessibilityAction(archiveLabel) { onArchive(); true },
            CustomAccessibilityAction(trashLabel) { onTrash(); true },
            CustomAccessibilityAction(moreLabel) { onMenu(); true },
        )
        NoteState.ARCHIVED -> listOf(
            CustomAccessibilityAction(restoreLabel) { onRestore(); true },
            CustomAccessibilityAction(trashLabel) { onTrash(); true },
            CustomAccessibilityAction(moreLabel) { onMenu(); true },
        )
        NoteState.TRASHED -> listOf(
            CustomAccessibilityAction(restoreLabel) { onRestore(); true },
            CustomAccessibilityAction(deleteLabel) { onDelete(); true },
            CustomAccessibilityAction(moreLabel) { onMenu(); true },
        )
    }
    SwipeToDismissBox(
        state = dismiss,
        modifier = modifier.padding(horizontal = 16.dp).semantics { customActions = actions; if (selected) stateDescription = openLabel },
        backgroundContent = {
            Box(
                Modifier.fillMaxSize().background(MaterialTheme.colorScheme.surfaceVariant, LocalNoteLook.current.cardShape).padding(horizontal = 20.dp),
                contentAlignment = if (dismiss.dismissDirection == SwipeToDismissBoxValue.StartToEnd) Alignment.CenterStart else Alignment.CenterEnd,
            ) {
                val label = when (dismiss.dismissDirection) {
                    SwipeToDismissBoxValue.StartToEnd -> if (item.state == NoteState.ACTIVE) R.string.move_to_trash else R.string.restore
                    else -> when (item.state) {
                        NoteState.ACTIVE -> R.string.archive
                        NoteState.ARCHIVED -> R.string.move_to_trash
                        NoteState.TRASHED -> R.string.delete_forever
                    }
                }
                Text(stringResource(label), color = MaterialTheme.colorScheme.onSurfaceVariant)
            }
        },
    ) {
        NoteRow(item, selected, conflicted, reminder, signal, channel, onOpen)
    }
}

@Composable
private fun NoteRow(item: NoteListItem, selected: Boolean, conflicted: Boolean, reminder: java.time.Instant?, signal: NoteSyncSignal.State, channel: Int, onOpen: () -> Unit) {
    val look = LocalNoteLook.current
    // «Mismo color en todas las notas»: solo cambia lo que se ve, la nota conserva el suyo.
    val hex = NoteDisplayColor.resolve(item.color, look.uniformColor)
    val face = noteFace(hex)
    // Aspectos Telecom: cada nota es un canal del osciloscopio (CH1…CH4, con su onda y su color) y no lleva su propio color, salvo que el
    // usuario active «color de la nota como fondo» a mano, que manda.
    val tint = look.telecom?.takeIf { !look.fillColor }?.let { NoteChannels.tint(channel, it, look.uniformColor) }
    // Con el color de fondo la tinta sale del contraste con ese color (la misma regla que en Windows); la selección se marca con un borde.
    val fill = look.fillColor
    val ink = if (fill) noteInk(hex) else MaterialTheme.colorScheme.onSurface
    val muted = if (fill) ink.copy(alpha = 0.75f) else MaterialTheme.colorScheme.onSurfaceVariant
    Card(
        onClick = onOpen,
        shape = look.cardShape,
        colors = CardDefaults.cardColors(
            containerColor = when {
                tint != null -> noteFace(tint.face)
                fill -> face
                selected -> MaterialTheme.colorScheme.primaryContainer
                else -> MaterialTheme.colorScheme.surfaceContainerHigh
            },
            contentColor = ink,
        ),
        border = if ((fill || tint != null) && selected) BorderStroke(3.dp, MaterialTheme.colorScheme.primary) else null,
    ) {
        Row(
            Modifier.fillMaxWidth().height(androidx.compose.foundation.layout.IntrinsicSize.Min)
                .paperTextureRow(look.paper, MaterialTheme.colorScheme.onSurface, MaterialTheme.colorScheme.surface.luminance() > 0.5f),
        ) {
            // La tira de color de la nota: es lo que reconoce una nota de un vistazo.
            if (!fill) Box(Modifier.width(8.dp).fillMaxHeight().background(if (tint != null) noteFace(tint.accent) else face))
            Column(Modifier.weight(1f).padding(horizontal = 14.dp, vertical = 10.dp)) {
                if (tint != null) {
                    val titleColor = noteFace(tint.title)
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Text(NoteChannels.number(channel), Modifier.padding(end = 8.dp), style = MaterialTheme.typography.titleMedium, fontWeight = androidx.compose.ui.text.font.FontWeight.Bold, color = titleColor)
                        Text(item.title, Modifier.weight(1f), style = MaterialTheme.typography.titleMedium, maxLines = 1, overflow = TextOverflow.Ellipsis, color = titleColor)
                        ChannelWave(channel, noteFace(tint.accent), Modifier.padding(start = 8.dp))
                    }
                } else {
                    Text(item.title, style = MaterialTheme.typography.titleMedium, maxLines = 1, overflow = TextOverflow.Ellipsis)
                }
                if (item.preview.isNotEmpty()) {
                    Text(
                        item.preview, style = MaterialTheme.typography.bodyMedium, maxLines = 1, overflow = TextOverflow.Ellipsis,
                        color = muted,
                    )
                }
                if (signal != NoteSyncSignal.State.HIDDEN) {
                    val text = stringResource(R.string.sync_signal_format, NoteSyncSignal.glyph(signal), stringResource(syncSignalLabel(signal)))
                    Text(
                        text, Modifier.padding(top = 2.dp), style = MaterialTheme.typography.labelSmall, fontFamily = FontFamily.Monospace, maxLines = 1,
                        color = if (signal == NoteSyncSignal.State.CONFLICT) MaterialTheme.colorScheme.error else MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                }
                if (reminder != null) {
                    val label = ReminderLabel.of(reminder, java.time.Instant.now(), java.time.ZoneId.systemDefault())
                    val tint = if (label == ReminderLabel.Overdue && !fill) MaterialTheme.colorScheme.error else if (fill) ink else MaterialTheme.colorScheme.primary
                    Row(Modifier.padding(top = 2.dp), verticalAlignment = Alignment.CenterVertically) {
                        Icon(Icons.Default.Notifications, contentDescription = stringResource(R.string.reminder_title), Modifier.size(14.dp), tint = tint)
                        Text(reminderText(label), Modifier.padding(start = 4.dp), style = MaterialTheme.typography.labelMedium, color = tint, maxLines = 1)
                    }
                }
            }
            Row(Modifier.align(Alignment.CenterVertically).padding(end = 10.dp), verticalAlignment = Alignment.CenterVertically) {
                if (item.isProtected) {
                    // Con el título genérico («Nota protegida») el candado no repite lo mismo: TalkBack leería «Nota protegida, Nota protegida».
                    val protectedLabel = stringResource(R.string.protected_note_title)
                    Icon(Icons.Default.Lock, contentDescription = protectedLabel.takeUnless { it == item.title }, Modifier.size(18.dp), tint = muted)
                }
                if (conflicted) {
                    Icon(Icons.Default.Warning, contentDescription = stringResource(R.string.conflict_flag_cd), Modifier.padding(start = 6.dp).size(18.dp), tint = MaterialTheme.colorScheme.error)
                }
            }
            if (item.hasTasks) {
                Text(
                    stringResource(R.string.tasks_progress, item.tasksDone, item.tasksTotal),
                    Modifier.padding(end = 14.dp).align(Alignment.CenterVertically),
                    style = MaterialTheme.typography.labelLarge, color = muted,
                )
            }
        }
    }
}

/** La onda del canal (senoidal, cuadrada, triangular o diente de sierra), en el color del canal. */
@Composable
private fun ChannelWave(channel: Int, color: Color, modifier: Modifier = Modifier) {
    val path = remember(channel) { androidx.compose.ui.graphics.vector.PathParser().parsePathString(NoteChannels.wavePath(channel)).toPath() }
    androidx.compose.foundation.Canvas(modifier.size(width = 40.dp, height = 14.dp).clearAndSetSemantics {}) {
        scale(size.width / NoteChannels.WAVE_WIDTH, size.height / NoteChannels.WAVE_HEIGHT, pivot = Offset.Zero) {
            drawPath(path, color, style = Stroke(width = 1.6.dp.toPx() * NoteChannels.WAVE_WIDTH / size.width, cap = StrokeCap.Round, join = StrokeJoin.Round))
        }
    }
}

private fun syncSignalLabel(state: NoteSyncSignal.State): Int = when (state) {
    NoteSyncSignal.State.SYNCED -> R.string.sync_signal_synced
    NoteSyncSignal.State.PENDING -> R.string.sync_signal_pending
    else -> R.string.sync_signal_conflict
}

/** El color de la nota tal cual (también un hex de un tema propio de Windows); uno ilegible cae en un gris, nunca rompe la lista. */
internal fun noteFace(hex: String): Color = runCatching { paletteColor(hex) }.getOrDefault(Color.Gray)

/** La tinta que se lee sobre ese color (la misma regla de contraste que en Windows). */
internal fun noteInk(hex: String): Color = runCatching { paletteColor(NoteColorContrast.foregroundFor(hex)) }.getOrDefault(Color.Black)

/** El icono discreto del estado de la sync. Sin conexión no es un error (no es culpa del usuario): se pinta apagado, no en rojo. */
@Composable
private fun SyncIcon(icon: StatusIcon, description: String) {
    when (icon) {
        StatusIcon.NONE -> Unit
        StatusIcon.OK -> Icon(Icons.Default.Check, description, tint = MaterialTheme.colorScheme.primary)
        StatusIcon.SYNCING -> CircularProgressIndicator(Modifier.size(20.dp), strokeWidth = 2.dp)
        StatusIcon.OFFLINE -> Icon(Icons.Default.Warning, description, tint = MaterialTheme.colorScheme.onSurfaceVariant)
        StatusIcon.ERROR -> Icon(Icons.Default.Warning, description, tint = MaterialTheme.colorScheme.error)
    }
}

/** Un dedo que mantiene pulsada una fila: si ya abrió el menú o empezó a arrastrar, al levantarlo no se debe abrir la nota. */
/** Cuánto aguanta el dedo quieto, tras la pulsación larga, para abrir el menú sin soltar. */
private const val MENU_HOLD_MS = 350L

private class PressHold {
    var active = false
    var travelled = 0f
    /** El menú ya se abrió con el dedo todavía puesto: al levantarlo no hay que abrirlo otra vez. */
    var menuShown = false
}

/** El aviso de que, sin sincronización, no hay copia de las notas: lleva a Ajustes (sincronizar o exportar) y se puede descartar. */
/** El aviso de que hay una versión nueva de Android: abre la página de la release en el navegador; no descarga nada. */
@Composable
private fun UpdateNotice(version: String, onDismiss: () -> Unit, onOpen: () -> Unit) {
    androidx.compose.material3.Surface(
        Modifier.fillMaxWidth().padding(horizontal = 16.dp, vertical = 4.dp),
        shape = MaterialTheme.shapes.medium, color = MaterialTheme.colorScheme.primaryContainer,
    ) {
        Column(Modifier.padding(horizontal = 12.dp, vertical = 8.dp)) {
            Text(stringResource(R.string.update_available, version), style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onPrimaryContainer)
            Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.End) {
                TextButton(onClick = onDismiss) { Text(stringResource(R.string.update_dismiss)) }
                TextButton(onClick = onOpen) { Text(stringResource(R.string.update_open)) }
            }
        }
    }
}

private fun currentVersionName(context: android.content.Context): String =
    runCatching { context.packageManager.getPackageInfo(context.packageName, 0).versionName }.getOrNull().orEmpty()

@Composable
private fun NoBackupNotice(onSettings: () -> Unit, onDismiss: () -> Unit) {
    androidx.compose.material3.Surface(
        Modifier.fillMaxWidth().padding(horizontal = 16.dp, vertical = 4.dp),
        shape = MaterialTheme.shapes.medium, color = MaterialTheme.colorScheme.surfaceVariant,
    ) {
        Column(Modifier.padding(start = 12.dp, end = 4.dp, top = 8.dp, bottom = 0.dp)) {
            Text(stringResource(R.string.no_backup_notice), style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
            Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.End) {
                // Botones de texto con menos relleno: el alto táctil sigue siendo el mínimo, pero el aviso deja de ocupar media pantalla.
                TextButton(onClick = onDismiss, contentPadding = androidx.compose.foundation.layout.PaddingValues(horizontal = 12.dp)) { Text(stringResource(R.string.no_backup_dismiss)) }
                TextButton(onClick = onSettings, contentPadding = androidx.compose.foundation.layout.PaddingValues(horizontal = 12.dp)) { Text(stringResource(R.string.settings)) }
            }
        }
    }
}
