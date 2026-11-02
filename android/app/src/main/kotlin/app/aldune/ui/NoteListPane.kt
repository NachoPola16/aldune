package app.aldune.ui

import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.gestures.awaitEachGesture
import androidx.compose.foundation.gestures.scrollBy
import androidx.compose.foundation.gestures.awaitFirstDown
import androidx.compose.foundation.gestures.awaitLongPressOrCancellation
import androidx.compose.foundation.gestures.detectDragGesturesAfterLongPress
import androidx.compose.ui.focus.focusProperties
import androidx.compose.ui.input.pointer.PointerEventPass
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.lazy.rememberLazyListState
import androidx.compose.runtime.withFrameNanos
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.ui.layout.onSizeChanged
import androidx.compose.runtime.mutableFloatStateOf
import androidx.compose.runtime.mutableLongStateOf
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.hapticfeedback.HapticFeedbackType
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.platform.LocalHapticFeedback
import androidx.compose.ui.zIndex
import app.aldune.domain.ReorderPlan
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.imePadding
import androidx.compose.foundation.layout.isImeVisible
import androidx.compose.ui.platform.LocalConfiguration
import androidx.compose.ui.focus.onFocusChanged
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
import androidx.compose.material.icons.filled.MoreVert
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.Check
import androidx.compose.material.icons.filled.KeyboardArrowDown
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
import androidx.compose.material3.SwipeToDismissBoxState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.ui.text.font.FontWeight
import app.aldune.text.NoteThemes
import app.aldune.domain.TagColors
import app.aldune.domain.CollapsedGroups
import app.aldune.domain.TagDrop
import app.aldune.domain.TagViewCycle
import app.aldune.domain.TagGrouping
import app.aldune.domain.TagOrder
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
import app.aldune.domain.RowKeys
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
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.graphics.drawscope.rotate
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
    onSearchFocus: (Boolean) -> Unit = {},
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
    // Selection mode: notes picked for a bulk action (archive, trash, restore, tag). Empty = normal list.
    var picked by remember { mutableStateOf<Set<String>>(emptySet()) }
    var taggingPicked by remember { mutableStateOf(false) }
    androidx.activity.compose.BackHandler(enabled = picked.isNotEmpty()) { picked = emptySet() }
    LaunchedEffect(state.items) { if (picked.isNotEmpty()) picked = picked.filter { id -> state.items.any { it.id == id } }.toSet() }

    // Reordering (by dragging, or with Move up / Move down): in the plain list, in the view of one tag (the neighbours that are shown are what
    // decides: the note is placed between them, and the order of the others is untouched), and inside each group of the grouped list. Not with
    // a search: its results are not a list the user arranged.
    val groupedReorder = state.query.isBlank() && state.tag == null && state.items.size > 1 && settingsNow.listOrder == 0 && settingsNow.groupByTag
    val reorderable = state.query.isBlank() && state.items.size > 1 && settingsNow.listOrder == 0 && (state.tag != null || !settingsNow.groupByTag)
    val context = androidx.compose.ui.platform.LocalContext.current

    // Template read configuration-aware here and formatted when the note moves.
    val movedTpl = stringResource(R.string.note_moved)
    val movedTagTpl = stringResource(R.string.note_moved_tag)

    /** Sube o baja la nota una posición (la alternativa al arrastre) y lo anuncia: «Movida a la posición N de M». */
    fun moveBy(id: String, delta: Int) {
        val ids = sameGroup(state.items, id)
        val step = ReorderPlan.step(ids, id, delta) ?: return
        viewModel.moveNote(id, step.neighbours.before, step.neighbours.after)
        scope.launch {
            snackbar.currentSnackbarData?.dismiss()
            snackbar.showSnackbar(String.format(movedTpl, step.position, ids.size), duration = SnackbarDuration.Short)
        }
    }

    val archivedMsg = stringResource(R.string.note_archived)
    val trashedMsg = stringResource(R.string.note_trashed)
    val restoredMsg = stringResource(R.string.note_restored)
    // The «N notes …» texts for a multi-selection: read as templates here (configuration-aware) and formatted when the action runs.
    val archivedManyTpl = stringResource(R.string.notes_archived_n)
    val trashedManyTpl = stringResource(R.string.notes_trashed_n)
    val restoredManyTpl = stringResource(R.string.notes_restored_n)
    val undoLabel = stringResource(R.string.undo)

    // Right after swiping a note away or undoing it, the finger is still near the list: a hold detector must not read that touch as a request
    // for the options of the note that just moved (reported: undo opened the menu of the restored note).
    var calmUntil by remember { mutableLongStateOf(0L) }
    fun announce(message: String, undo: () -> Unit) {
        calmUntil = System.currentTimeMillis() + MENU_CALM_MS
        scope.launch {
            snackbar.currentSnackbarData?.dismiss()
            if (snackbar.showSnackbar(message, undoLabel, duration = SnackbarDuration.Short) == SnackbarResult.ActionPerformed) {
                calmUntil = System.currentTimeMillis() + MENU_CALM_MS
                undo()
            }
        }
    }

    // A phone on its side with the keyboard up (typing a search, or a note beside the list) leaves the results no height at all: the state tabs
    // and the tag chips step aside until the keyboard goes, so the search box has its results under it.
    val tight = LocalConfiguration.current.screenHeightDp < 480 && imeVisible()
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
                        .onFocusChanged { onSearchFocus(it.isFocused) }
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
            if (picked.isNotEmpty()) SelectionBar(
                count = picked.size, filter = state.filter,
                onAll = { picked = state.items.map { it.id }.toSet() }, onExit = { picked = emptySet() },
                onArchive = { val ids = picked; ids.forEach(viewModel::archive); picked = emptySet(); announce(if (ids.size == 1) archivedMsg else String.format(archivedManyTpl, ids.size)) { ids.forEach(viewModel::restore) } },
                onTrash = { val ids = picked; ids.forEach(viewModel::trash); picked = emptySet(); announce(if (ids.size == 1) trashedMsg else String.format(trashedManyTpl, ids.size)) { ids.forEach(viewModel::restore) } },
                onRestore = { val ids = picked; ids.forEach(viewModel::restore); picked = emptySet(); announce(if (ids.size == 1) restoredMsg else String.format(restoredManyTpl, ids.size)) { ids.forEach(viewModel::trash) } },
                onTag = { taggingPicked = true },
            ) else if (!tight) Row(Modifier.fillMaxWidth().padding(top = 8.dp, end = 8.dp), verticalAlignment = Alignment.CenterVertically) {
                Box(Modifier.weight(1f)) { StateTabs(state, viewModel::setFilter) }
                if (hasProfile && status != null) {
                    val line = formatStatus(status, rememberNow())
                    IconButton(onClick = { showSyncDetail = true }, modifier = Modifier.semantics { liveRegion = LiveRegionMode.Polite }) { SyncIcon(line.icon, stringResource(R.string.sync_status_cd)) }
                }
            }
            if (!tight && state.allTags.isNotEmpty()) TagRow(
                state, settingsNow.showTagHash, { tag -> calmUntil = System.currentTimeMillis() + MENU_CALM_MS; viewModel.setTag(tag) }, onMoveTag = viewModel::moveTag,
                tagColors = remember(settingsNow.tagColors) { TagColors.decode(settingsNow.tagColors) },
                paletteColors = NoteThemes.resolve(settingsNow.noteThemeId, null).let { it.lightColors + it.darkColors },
                onSetTagColor = viewModel::setTagColor,
                uniformColorOn = settingsNow.uniformNoteColor != null,
                groupOrder = remember(state.allTags, settingsNow.tagOrder) {
                    val custom = TagOrder.decode(settingsNow.tagOrder)
                    TagOrder.groupOrder(TagOrder.apply(state.allTags, custom), custom)
                },
            )
            if (state.orphans.isNotEmpty()) OrphanConflictsBanner(state.orphans.size) { showOrphans = true }
            // Una versión nueva de Android en GitHub (una vez al día, desactivable en Ajustes → General): solo avisa y abre la página.
            val pendingUpdate by viewModel.update.collectAsStateWithLifecycle()
            androidx.compose.runtime.LaunchedEffect(settingsNow.checkUpdates) { viewModel.checkForUpdates(currentVersionName(context)) }
            pendingUpdate?.let { found ->
                val openPage = { context.startActivity(android.content.Intent(android.content.Intent.ACTION_VIEW, android.net.Uri.parse(AndroidReleaseUpdate.PAGE + found.tag))) }
                // A pop-up once per version (a banner alone went unnoticed); the banner below stays as a reminder.
                if (settingsNow.announcedUpdate != found.version) {
                    AlertDialog(
                        onDismissRequest = { viewModel.updateSettings { it.copy(announcedUpdate = found.version) } },
                        title = { Text(stringResource(R.string.update_available, found.version), Modifier.semantics { heading() }) },
                        text = { Text(stringResource(R.string.update_dialog_body)) },
                        confirmButton = {
                            TextButton(onClick = { viewModel.updateSettings { it.copy(announcedUpdate = found.version) }; openPage() }) { Text(stringResource(R.string.update_open)) }
                        },
                        dismissButton = {
                            TextButton(onClick = { viewModel.updateSettings { it.copy(announcedUpdate = found.version) } }) { Text(stringResource(R.string.update_dismiss)) }
                        },
                    )
                }
                UpdateNotice(
                    found.version, onDismiss = { viewModel.dismissUpdate(found.version) },
                    onOpen = { context.startActivity(android.content.Intent(android.content.Intent.ACTION_VIEW, android.net.Uri.parse(AndroidReleaseUpdate.PAGE + found.tag))) },
                )
            }
            // Sin perfil de sync, las notas solo viven en el móvil: se avisa una vez que haya alguna (en la bienvenida ya se habla de la sync).
            if (!hasProfile && !settingsNow.backupNoticeDismissed && state.counts.values.sum() > 0) {
                NoBackupNotice(R.string.no_backup_notice, onSettings = onOpenSettings, onDismiss = { viewModel.updateSettings { it.copy(backupNoticeDismissed = true) } })
            }
            // La copia automática perdió su carpeta (o el permiso): se avisa hasta que se descarta, y descartar la deja apagada.
            val autoExportLost by app.aldune.export.AutoExportStatus.lost.collectAsStateWithLifecycle()
            if (autoExportLost) {
                NoBackupNotice(
                    R.string.auto_export_lost, onSettings = onOpenSettings,
                    onDismiss = {
                        app.aldune.export.AutoExportStatus.setLost(context, false)
                        viewModel.updateSettings { it.copy(autoExportEnabled = false) }
                    },
                )
            }

            // Tirar hacia abajo sincroniza (spec madre §2.1). Without a profile there is nothing to sync: no pass starts and the pull draws no
            // spinner, so the gesture does not suggest that something is being checked.
            val pullState = androidx.compose.material3.pulltorefresh.rememberPullToRefreshState()
            // While the list is being pulled (and just after), a hold on a row is part of the pull, not a request for its options.
            LaunchedEffect(pullState.distanceFraction) { if (pullState.distanceFraction > 0f) calmUntil = System.currentTimeMillis() + MENU_CALM_MS }
            PullToRefreshBox(
                isRefreshing = status?.running == true,
                onRefresh = {
                    // The finger that pulled the list is still around: for a moment nothing may read it as a request for a note's options.
                    calmUntil = System.currentTimeMillis() + MENU_CALM_MS
                    if (hasProfile) viewModel.syncEvent(SyncEvent.PullToRefresh)
                },
                modifier = Modifier.fillMaxSize(),
                state = pullState,
                indicator = {
                    if (hasProfile) androidx.compose.material3.pulltorefresh.PullToRefreshDefaults.Indicator(
                        state = pullState, isRefreshing = status?.running == true, modifier = Modifier.align(Alignment.TopCenter),
                    )
                },
            ) {
            if (state.items.isEmpty()) {
                EmptyMessage(state)
            } else {
                // Reordenar: mantener pulsada una nota y arrastrarla (o Subir/Bajar, para quien no puede arrastrar); `reorderable` arriba.
                val listState = rememberLazyListState()
                val haptic = LocalHapticFeedback.current
                var order by remember { mutableStateOf(state.items) }
                var draggedId by remember { mutableStateOf<String?>(null) }
                // The key of the row being dragged: the note id in the plain list, `<group>/<id>` in the grouped one (a note can show under several tags).
                var dragRowKey by remember { mutableStateOf<String?>(null) }
                // The group (tag) the dragged note is over when it is not its own: letting go there moves the note to that tag.
                var dropGroup by remember { mutableStateOf<String?>(null) }
                // Where in that group it was let go: over a note of it (and in which half), or over its header (the top).
                var dropOnId by remember { mutableStateOf<String?>(null) }
                var dropBelow by remember { mutableStateOf(false) }
                var dragDelta by remember { mutableFloatStateOf(0f) }
                var dragStartIds by remember { mutableStateOf<List<String>>(emptyList()) }
                // Mientras se arrastra manda el orden local; fuera del arrastre, el de la base.
                // Synchronous (in the same composition that receives the new items), not in an effect: an effect leaves one frame in which the
                // rows are built from the old items with the new view, and the groups jumped when going back to «All».
                var orderSource by remember { mutableStateOf(state.items) }
                if (draggedId == null && orderSource !== state.items) { orderSource = state.items; order = state.items }

                // Vista agrupada de «Todas las notas»: una cabecera plegable por etiqueta y, debajo, sus notas. Una nota con varias etiquetas sale
                // bajo cada una (por eso su clave lleva la sección). Con búsqueda o con una etiqueta elegida se enseña la lista de siempre.
                // By what the items really are (see UiState.Ready.itemsTag), not by the view just asked for: for a moment after going back to «All» the
                // items are still those of the tag, and grouping them put the headers in a wrong order that then animated into the right one.
                val grouped = settingsNow.groupByTag && state.itemsTag == null && state.itemsQuery.isBlank()
                // Kept in the settings, not just in the saved state of the screen: it has to survive closing the app, not only turning the phone.
                val collapsed = remember(settingsNow.collapsedGroups) { CollapsedGroups.decode(settingsNow.collapsedGroups) }
                val untaggedLabel = stringResource(R.string.group_untagged)
                // Dragging a header reorders the tags: while it lasts every group is folded (equal rows to swap) and the groups follow the local
                // order; the tag order is saved when the finger lifts.
                var tagDrag by remember { mutableStateOf<String?>(null) }
                var tagDragDelta by remember { mutableFloatStateOf(0f) }
                var tagDragOrder by remember { mutableStateOf<List<String>?>(null) }
                var tagDragStart by remember { mutableStateOf<List<String>>(emptyList()) }
                // The group order, straight from the saved setting (not from `state.allTags`, which refreshes a moment later): after a drop the list
                // must already be in the new order, or it would jump back to the old one and then animate to the new.
                val customTagOrder = remember(settingsNow.tagOrder) { TagOrder.decode(settingsNow.tagOrder) }
                val groupOrder = remember(state.allTags, customTagOrder) { TagOrder.groupOrder(TagOrder.apply(state.allTags, customTagOrder), customTagOrder) }
                // The local order of a finished drag is dropped once the saved one has arrived.
                LaunchedEffect(settingsNow.tagOrder) { if (tagDrag == null) tagDragOrder = null }
                val entries = remember(order, grouped, collapsed, groupOrder, tagDrag != null, tagDragOrder) {
                    if (!grouped) order.map<NoteListItem, ListEntry> { ListEntry.Note(it, it.id) }
                    else buildList<ListEntry> {
                        val sequence = tagDragOrder ?: groupOrder
                        val groups = TagGrouping.group(order, sequence) { it.tags }
                        val byKey = groups.associateBy { it.tag?.lowercase() ?: TagGrouping.UNTAGGED }
                        // Every tag has its group, an empty one too, which goes after those with notes (see `sectionKeys`).
                        for (groupKey in TagGrouping.sectionKeys(sequence, byKey.keys, dragging = tagDrag != null)) {
                            val group = byKey[groupKey]
                            if (group == null && groupKey == TagGrouping.UNTAGGED) continue
                            val title = group?.tag ?: if (groupKey == TagGrouping.UNTAGGED) untaggedLabel else sequence.first { it.lowercase() == groupKey }
                            val items = group?.items.orEmpty()
                            val open = groupKey !in collapsed && tagDrag == null
                            add(ListEntry.Header(title, groupKey, items.size, open))
                            if (open) for (note in items) add(ListEntry.Note(note, "$groupKey/${note.id}"))
                        }
                    }
                }
                // La fila que señala el widget (una protegida): se lleva a la vista.
                val highlight by viewModel.highlight.collectAsStateWithLifecycle()
                LaunchedEffect(highlight, entries) {
                    // The index is in the rows actually drawn (headers and repeated notes included), not in `order`.
                    val index = entries.indexOfFirst { it is ListEntry.Note && it.item.id == highlight }
                    if (highlight != null && index >= 0) listState.animateScrollToItem(index)
                }

                // A note just created from the editor goes to the end of the list, which may be off screen: bring it into view (unless it already
                // is) so that leaving the note shows where it went instead of the place the list was at when it was created.
                val scrollTo by viewModel.scrollTo.collectAsStateWithLifecycle()
                LaunchedEffect(scrollTo, entries) {
                    val target = scrollTo ?: return@LaunchedEffect
                    val index = entries.indexOfFirst { it is ListEntry.Note && it.item.id == target }
                    if (index < 0) return@LaunchedEffect
                    withFrameNanos { }
                    val shown = listState.layoutInfo.visibleItemsInfo.any { RowKeys.shows(it.key, target) }
                    // A jump, not an animation: the list is coming back under the closing editor, and watching it travel to the end looked like
                    // it was struggling to get there.
                    if (!shown) listState.scrollToItem(index)
                    viewModel.scrollHandled()
                }

                // A menu only opens for a row that is on screen: a gesture that ends or is cancelled after the list scrolled must never
                // surface the options of a note the user cannot see.
                fun openMenuIfShown(item: NoteListItem) {
                    if (System.currentTimeMillis() < calmUntil) return
                    if (listState.layoutInfo.visibleItemsInfo.any { RowKeys.shows(it.key, item.id) }) menuFor = item
                }

                fun dragStart(id: String, rowKey: String) {
                    haptic.performHapticFeedback(HapticFeedbackType.LongPress)
                    draggedId = id
                    dragRowKey = rowKey
                    dragDelta = 0f
                    dragStartIds = order.map { it.id }
                }

                fun swapUnderDrag() {
                    val id = draggedId ?: return
                    val rowKey = dragRowKey ?: return
                    val visible = listState.layoutInfo.visibleItemsInfo
                    val noteIds = order.map { it.id }.toHashSet()
                    fun idOf(key: Any): String? = (key as? String)?.substringAfterLast('/')?.takeIf { it in noteIds }
                    fun groupOf(key: Any): String = (key as? String)?.substringBeforeLast('/', "") ?: ""
                    // Only the rows of the group the note is dragged in (the whole list when it is not grouped) take part: a note does not
                    // jump to another tag by being dragged over it.
                    val rows = visible.filter { idOf(it.key) != null && groupOf(it.key) == groupOf(rowKey) }
                    // Tras un intercambio, la pantalla tarda un fotograma en recolocarse: sin esperar, el mismo gesto intercambiaría dos veces.
                    val shown = rows.sortedBy { it.offset }.mapNotNull { idOf(it.key) }
                    if (shown != order.map { it.id }.filter { it in shown }) return
                    val me = rows.firstOrNull { it.key == rowKey } ?: return
                    val center = me.offset + me.size / 2f + dragDelta
                    val pinned = order.firstOrNull { it.id == id }?.pinnedToTop ?: return
                    // Fijadas y no fijadas son dos grupos: arrastrar no cruza la frontera (las fijadas van siempre delante).
                    val target = rows.firstOrNull {
                        it.key != rowKey && center >= it.offset && center <= it.offset + it.size && order.firstOrNull { n -> n.id == idOf(it.key) }?.pinnedToTop == pinned
                    } ?: return
                    val from = order.indexOfFirst { it.id == id }
                    val to = order.indexOfFirst { it.id == idOf(target.key) }
                    if (from < 0 || to < 0) return
                    order = order.toMutableList().apply { add(to, removeAt(from)) }
                    // La nota arrastrada cambia de sitio en la lista: su desplazamiento se compensa para que siga bajo el dedo.
                    val newOffset = if (to < from) target.offset else target.offset + target.size - me.size
                    dragDelta -= (newOffset - me.offset)
                }

                // Over another group (its header or one of its notes) the note will change tag when dropped; the header lights up to say so.
                fun updateDropGroup() {
                    val rowKey = dragRowKey ?: return
                    val own = draggedId?.let { rowKey.removeSuffix("/$it") }
                    if (own == null || own == rowKey) { dropGroup = null; return }
                    val visible = listState.layoutInfo.visibleItemsInfo
                    val me = visible.firstOrNull { it.key == rowKey } ?: return
                    val center = me.offset + me.size / 2f + dragDelta
                    val underInfo = visible.firstOrNull { it.key != rowKey && center >= it.offset && center <= it.offset + it.size }
                    val under = underInfo?.key as? String
                    if (underInfo == null || under == null) { dropGroup = null; return }
                    val onHeader = entries.any { it is ListEntry.Header && it.key == under }
                    val group = if (onHeader) under else under.substringBeforeLast('/')
                    dropGroup = group.takeIf { it != own }
                    dropOnId = if (onHeader) null else under.substringAfterLast('/')
                    dropBelow = !onHeader && center > underInfo.offset + underInfo.size / 2f
                }

                fun swapTagUnderDrag() {
                    val key = tagDrag ?: return
                    val local = tagDragOrder ?: return
                    val visible = listState.layoutInfo.visibleItemsInfo
                    // After a swap the screen takes a frame to re-place the rows: without waiting, the same gesture would swap twice (or, dragging
                    // a header from the top to the bottom, not finish). Same guard as the notes' drag.
                    val wanted = local.map { it.lowercase() }.filter { k -> visible.any { it.key == k } }
                    if (visible.sortedBy { it.offset }.map { it.key }.filter { it in wanted } != wanted) return
                    val me = visible.firstOrNull { it.key == key } ?: return
                    val center = me.offset + me.size / 2f + tagDragDelta
                    val target = visible.firstOrNull { it.key != key && it.key is String && local.any { t -> t.lowercase() == it.key } && center >= it.offset && center <= it.offset + it.size } ?: return
                    val moved = TagOrder.moveTo(local, key, target.key as String)
                    if (moved == local) return
                    tagDragOrder = moved
                    val newOffset = if (target.offset < me.offset) target.offset else target.offset + target.size - me.size
                    tagDragDelta -= (newOffset - me.offset)
                }

                // Folding the groups moves the header that is being dragged (the ones above it shrink). The folded list is pushed down by that much,
                // so the slot of the dragged header stays where the finger is and the other slots are where they look to be (moving the header alone
                // under the finger made it overlap the slot of its neighbour and swap with it the wrong way).
                var tagDragStartOffset by remember { androidx.compose.runtime.mutableIntStateOf(0) }
                var tagDragPad by remember { mutableFloatStateOf(0f) }
                // The tag whose options are open (rename / delete…).
                var tagMenu by remember { mutableStateOf<String?>(null) }
                LaunchedEffect(tagDrag) {
                    val key = tagDrag ?: run { tagDragPad = 0f; return@LaunchedEffect }
                    androidx.compose.runtime.withFrameNanos { }
                    androidx.compose.runtime.withFrameNanos { }
                    val now = listState.layoutInfo.visibleItemsInfo.firstOrNull { it.key == key } ?: return@LaunchedEffect
                    tagDragPad = (tagDragStartOffset - now.offset).coerceAtLeast(0).toFloat()
                }

                fun tagDragEnd(save: Boolean) {
                    val finished = tagDragOrder
                    tagDrag = null; tagDragDelta = 0f
                    if (save && finished != null && finished != tagDragStart) viewModel.setTagOrder(finished) else tagDragOrder = null
                }

                fun dragBy(dy: Float) {
                    dragDelta += dy
                    swapUnderDrag()
                    updateDropGroup()
                }

                // Dragging near the top or bottom edge scrolls the list so the note can keep moving (the finger can be still at the edge, hence a
                // loop per frame and not the drag callback). The scrolled distance goes into `dragDelta`: the row stays under the finger.
                val edge = with(LocalDensity.current) { 72.dp.toPx() }
                LaunchedEffect(draggedId) {
                    draggedId ?: return@LaunchedEffect
                    while (true) {
                        androidx.compose.runtime.withFrameNanos { }
                        val rowKey = dragRowKey ?: continue
                        val info = listState.layoutInfo
                        val me = info.visibleItemsInfo.firstOrNull { it.key == rowKey } ?: continue
                        val center = me.offset + me.size / 2f + dragDelta
                        val speed = 14f
                        val step = when {
                            center < info.viewportStartOffset + edge -> -speed * ((info.viewportStartOffset + edge - center) / edge).coerceIn(0.2f, 1.5f)
                            center > info.viewportEndOffset - edge -> speed * ((center - (info.viewportEndOffset - edge)) / edge).coerceIn(0.2f, 1.5f)
                            else -> 0f
                        }
                        if (step != 0f) {
                            val moved = listState.scrollBy(step)
                            if (moved != 0f) { dragDelta += moved; swapUnderDrag(); updateDropGroup() }
                        }
                    }
                }

                fun dragEnd() {
                    val id = draggedId ?: return
                    val pinned = order.firstOrNull { it.id == id }?.pinnedToTop
                    val startGroup = dragStartIds.filter { start -> order.firstOrNull { it.id == start }?.pinnedToTop == pinned }
                    val nowGroup = order.filter { it.pinnedToTop == pinned }.map { it.id }
                    val plan = ReorderPlan.neighbours(startGroup, startGroup.indexOf(id), nowGroup.indexOf(id))
                    val target = dropGroup
                    val onId = dropOnId
                    val below = dropBelow
                    val fromGroup = dragRowKey?.removeSuffix("/$id")
                    draggedId = null
                    dragRowKey = null
                    dragDelta = 0f
                    dropGroup = null
                    if (target != null && fromGroup != null) {
                        // Dropped on another tag: the note leaves its tag and takes that one (the order of the list stays as it was).
                        fun tagOf(group: String): String? =
                            if (group == TagGrouping.UNTAGGED) null else entries.firstNotNullOfOrNull { (it as? ListEntry.Header)?.takeIf { h -> h.key == group }?.title }
                        val from = tagOf(fromGroup)
                        val to = tagOf(target)
                        // Its place in the other tag: where it was let go (over a note: before or after it by the half; over the header: first).
                        val inTarget = order.filter { n -> n.id != id && (if (to == null) n.tags.isEmpty() else n.tags.any { t -> t.equals(to, ignoreCase = true) }) }.map { n -> n.id }
                        val place = TagDrop.place(inTarget, onId, below)
                        // The note shows in its new group at once: waiting for the saved list, it jumped back to its old place for a frame.
                        order = order.map { n -> if (n.id != id) n else n.copy(tags = TagDrop.retag(n.tags, from, to)) }
                        if (place.hasNeighbours) {
                            val moved = order.first { n -> n.id == id }
                            val without = order.filter { n -> n.id != id }
                            order = without.toMutableList().apply { add(TagDrop.insertIndex(without.map { n -> n.id }, place), moved) }
                        }
                        // The list keeps its place by the key of its first visible row; if that row is the header of the tag that just emptied
                        // (it goes to the end), the list would follow it all the way down. Asking for the same place before the change is
                        // laid out keeps the view where it was, with no frame in between.
                        listState.requestScrollToItem(listState.firstVisibleItemIndex, listState.firstVisibleItemScrollOffset)
                        viewModel.moveNoteToTag(id, from, to)
                        if (place.hasNeighbours) viewModel.moveNote(id, place.before, place.after)
                        val shownTo = to ?: untaggedLabel
                        announce(String.format(movedTagTpl, shownTo)) { viewModel.moveNoteToTag(id, to, from) }
                    } else if (plan != null) viewModel.moveNote(id, plan.before, plan.after) else order = state.items
                }

                tagMenu?.let { tag ->
                    TagOptionsDialogs(
                        tag, onDismiss = { tagMenu = null },
                        onRename = { to -> viewModel.renameTag(tag, to) },
                        onDelete = { withNotes -> viewModel.deleteTag(tag, withNotes) },
                    )
                }

                // El margen de abajo deja la última nota por encima del «+» al llegar al final de la lista.
                // A swipe over the empty part of the list (below the last note, when the notes do not fill the screen) goes to the next view: All,
                // then each tag, then All again. Rows keep their own swipes: a gesture that starts on a note is not read here.
                val swipeMinimum = with(LocalDensity.current) { 64.dp.toPx() }
                LazyColumn(
                    Modifier.fillMaxSize().pointerInput(state.allTags, state.tag) {
                        awaitEachGesture {
                            val down = awaitFirstDown(requireUnconsumed = false)
                            val info = listState.layoutInfo
                            val endOfNotes = info.visibleItemsInfo.lastOrNull()?.let { it.offset + it.size } ?: info.viewportStartOffset
                            val onEmptySpace = info.visibleItemsInfo.size == info.totalItemsCount && down.position.y > endOfNotes
                            if (!onEmptySpace || state.allTags.isEmpty()) return@awaitEachGesture
                            var end = down.position
                            while (true) {
                                val event = awaitPointerEvent()
                                val change = event.changes.firstOrNull { it.id == down.id } ?: break
                                if (!change.pressed) break
                                end = change.position
                            }
                            val swipe = TagViewCycle.swipe(end.x - down.position.x, end.y - down.position.y, swipeMinimum)
                            if (swipe != TagViewCycle.Swipe.NONE) viewModel.setTag(TagViewCycle.next(state.allTags, state.tag, forward = swipe == TagViewCycle.Swipe.NEXT))
                        }
                    },
                    state = listState, verticalArrangement = Arrangement.spacedBy(8.dp),
                    contentPadding = androidx.compose.foundation.layout.PaddingValues(top = with(LocalDensity.current) { tagDragPad.toDp() }, bottom = 16.dp),
                ) {
                    items(entries, key = { it.key }) { entry ->
                      when (entry) {
                        is ListEntry.Header -> {
                            val draggable = groupOrder.size > 1
                            val headerDragging = tagDrag == entry.key
                            GroupHeader(
                                entry, highlighted = dropGroup == entry.key,
                                modifier = (if (headerDragging) Modifier.zIndex(1f).graphicsLayer { translationY = tagDragDelta } else Modifier.animateItem())
                                    .then(
                                        Modifier.pointerInput(entry.key, groupOrder) {
                                            // Holding a header and moving it reorders the tags. The folding of the groups waits for the first
                                            // movement, so a plain hold does not flicker the list. The options of the tag are the three dots.
                                            var started = false
                                            var travelled = 0f
                                            detectDragGesturesAfterLongPress(
                                                onDragStart = {
                                                    haptic.performHapticFeedback(HapticFeedbackType.LongPress)
                                                    started = false; travelled = 0f
                                                },
                                                onDrag = { change, amount ->
                                                    change.consume()
                                                    if (!started) {
                                                        travelled += amount.y
                                                        if (draggable && kotlin.math.abs(travelled) > viewConfiguration.touchSlop) {
                                                            started = true
                                                            val me = listState.layoutInfo.visibleItemsInfo.firstOrNull { it.key == entry.key }
                                                            tagDragStartOffset = me?.offset ?: 0
                                                            // The folded list puts this header at (its position among the headers) rows from the top, all
                                                            // headers being the same height: the padding that keeps it under the finger is known right now,
                                                            // so folding and padding happen in the same frame and the rows slide instead of jumping up and
                                                            // then down a couple of frames later (the exact value is still measured below, once laid out).
                                                            if (me != null) {
                                                                val position = entries.filterIsInstance<ListEntry.Header>().indexOfFirst { it.key == entry.key }
                                                                tagDragPad = (me.offset - position * (me.size + 8.dp.toPx())).coerceAtLeast(0f)
                                                            }
                                                            tagDrag = entry.key; tagDragDelta = travelled; tagDragStart = groupOrder; tagDragOrder = groupOrder
                                                        }
                                                    } else {
                                                        tagDragDelta += amount.y
                                                        swapTagUnderDrag()
                                                    }
                                                },
                                                onDragEnd = { if (started) tagDragEnd(save = true) },
                                                onDragCancel = { if (started) tagDragEnd(save = false) },
                                            )
                                        },
                                    ),
                                onMenu = if (entry.key != TagGrouping.UNTAGGED) ({ tagMenu = entry.title }) else null,
                            ) {
                                viewModel.updateSettings { st -> st.copy(collapsedGroups = CollapsedGroups.encode(CollapsedGroups.toggle(CollapsedGroups.decode(st.collapsedGroups), entry.key))) }
                            }
                        }
                        is ListEntry.Note -> {
                        val item = entry.item
                        val dragging = draggedId == item.id && dragRowKey == entry.key
                        // Mantener pulsada una nota: arrastrar la reordena y soltar sin mover abre su menú. En los dos casos el «soltar» no debe
                        // abrir la nota, que es lo que haría el toque de la fila al levantar el dedo.
                        val hold = remember(item.id) { PressHold() }
                        SwipeableRow(
                            modifier = (if (dragging) Modifier.zIndex(1f).graphicsLayer { translationY = dragDelta } else Modifier.animateItem())
                                .then(
                                    if (reorderable || groupedReorder) Modifier.pointerInput(item.id) {
                                        detectDragGesturesAfterLongPress(
                                            onDragStart = {
                                                hold.active = true; hold.travelled = 0f; hold.menuShown = false; dragStart(item.id, entry.key)
                                                val press = ++hold.press
                                                // Si el dedo sigue quieto un momento tras la pulsación larga no se quiere recolocar la nota: el menú se
                                                // abre sin esperar a levantarlo. Si se mueve antes, es un arrastre y el menú no sale.
                                                scope.launch {
                                                    delay(MENU_HOLD_MS)
                                                    if (hold.press == press && hold.active && !hold.menuShown && hold.travelled <= viewConfiguration.touchSlop) {
                                                        hold.menuShown = true
                                                        openMenuIfShown(item)
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
                                                if (!moved && !hold.menuShown) openMenuIfShown(item)
                                                hold.active = false
                                            },
                                            // Also called when the row leaves the composition (scrolled away) mid-gesture: then it is not a held
                                            // finger and openMenuIfShown refuses the row because it is no longer on screen.
                                            onDragCancel = {
                                                val moved = hold.travelled > viewConfiguration.touchSlop
                                                dragEnd()
                                                if (!moved && !hold.menuShown) openMenuIfShown(item)
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
                                                openMenuIfShown(item)
                                                do {
                                                    val event = awaitPointerEvent()
                                                } while (event.changes.any { it.pressed })
                                                hold.active = false
                                            }
                                        }
                                    }
                                ),
                            item = item, selected = item.id == selectedId || item.id == highlight || item.id in picked, swipeEnabled = picked.isEmpty(), conflicted = item.id in conflicted, reminder = state.reminders[item.id],
                            onOpen = { if (picked.isNotEmpty()) picked = if (item.id in picked) picked - item.id else picked + item.id else if (!hold.active) onOpen(item.id) }, onMenu = { menuFor = item },
                            moves = ReorderPlan.available(sameGroup(order, item.id), item.id, reorderable), onMove = { delta -> moveBy(item.id, delta) },
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

    // A menu or confirmation about a note that is no longer in this list (deleted, restored, archived) is closed: otherwise
    // "Open note" would open a note that no longer exists, and the menu would show stale data.
    LaunchedEffect(state.items) {
        if (menuFor != null && state.items.none { it.id == menuFor?.id }) menuFor = null
        if (deleting != null && state.items.none { it.id == deleting?.id }) deleting = null
    }
    menuFor?.let { shown ->
        val item = state.items.firstOrNull { it.id == shown.id } ?: return@let
        NoteActionsDialog(
            item, openInEditor = item.id == selectedId, viewModel = viewModel, onOpen = { onOpen(item.id) }, onClose = { menuFor = null },
            moves = ReorderPlan.available(sameGroup(state.items, item.id), item.id, reorderable), onMove = { delta -> moveBy(item.id, delta) },
            onArchive = { viewModel.archive(item.id); announce(archivedMsg) { viewModel.restore(item.id) } },
            onTrash = { viewModel.trash(item.id); announce(trashedMsg) { viewModel.restore(item.id) } },
            onRestore = { viewModel.restore(item.id); announce(restoredMsg) { viewModel.trash(item.id) } },
            onSelect = { picked = setOf(item.id) },
        )
    }

    if (taggingPicked) TagPickedDialog(
        state.allTags,
        onPick = { tag ->
            for (id in picked) state.items.firstOrNull { it.id == id }?.let { n -> viewModel.setNoteTags(id, (n.tags + tag).distinct()) }
            taggingPicked = false; picked = emptySet()
        },
        onDismiss = { taggingPicked = false },
    )

    deleting?.let { item ->
        AlertDialog(
            onDismissRequest = { deleting = null },
            title = { Text(stringResource(R.string.delete_forever_title), Modifier.semantics { heading() }) },
            text = { Text(item.title + "\n\n" + stringResource(R.string.delete_forever_body)) },
            confirmButton = {
                TextButton(onClick = { viewModel.deleteForever(item.id); deleting = null }) { Text(stringResource(R.string.delete_forever), color = MaterialTheme.colorScheme.error) }
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
private fun TagRow(
    state: UiState.Ready, showHash: Boolean, onSelect: (String?) -> Unit, onMoveTag: (String, Int) -> Unit,
    tagColors: Map<String, String>, paletteColors: List<String>, onSetTagColor: (String, String?) -> Unit,
    groupOrder: List<String>, uniformColorOn: Boolean,
) {
    val listState = rememberLazyListState()
    var sheetOpen by remember { mutableStateOf(false) }
    if (sheetOpen) AllTagsDialog(groupOrder, state.tag, showHash, onSelect = { sheetOpen = false; onSelect(it) }, onMove = onMoveTag, onDismiss = { sheetOpen = false },
        untaggedLabel = stringResource(R.string.group_untagged), uniformColorOn = uniformColorOn, tagColors = tagColors, paletteColors = paletteColors, onSetColor = onSetTagColor)
    // end = 8.dp: the same margin as the row above, so the arrow sits right under the sync status icon.
    Row(Modifier.padding(bottom = 10.dp, end = 8.dp), verticalAlignment = Alignment.CenterVertically) {
    Box(Modifier.weight(1f)) {
        LazyRow(
            // Space below the chips equal to the space between them and the tabs (it was 20 dp above and 8 dp below).
            state = listState, contentPadding = androidx.compose.foundation.layout.PaddingValues(horizontal = 16.dp),
            horizontalArrangement = Arrangement.spacedBy(8.dp),
        ) {
            // «Todas»: quitar el filtro de etiqueta sin tener que volver a pulsar la activa (y ver que no hay ninguna activa).
            item {
                FilterChip(selected = state.tag == null, onClick = { onSelect(null) }, label = { Text(stringResource(R.string.all_notes_chip)) })
            }
            items(state.allTags) { tag ->
                val selected = state.tag.equals(tag, ignoreCase = true)
                FilterChip(
                    selected = selected, onClick = { onSelect(if (selected) null else tag) },
                    label = { Text(if (showHash) "#$tag" else tag) },
                    leadingIcon = if (showHash) null else ({ Icon(TagIcon, contentDescription = null, Modifier.size(16.dp)) }),
                )
            }

        }
        // A fade on the right edge says "there are more tags": it only shows while some are out of view, and the half-cut chip does the rest.
        if (listState.canScrollForward) {
            val ground = MaterialTheme.colorScheme.background
            Box(
                Modifier.align(Alignment.CenterEnd).size(width = 40.dp, height = 32.dp)
                    .background(androidx.compose.ui.graphics.Brush.horizontalGradient(listOf(Color.Transparent, ground))),
            )
        }
    }
    // Siempre a la vista, fuera del desplazamiento: todas las etiquetas en una hoja buscable y ordenable (la fila solo enseña las primeras).
    val allTagsLabel = stringResource(R.string.all_tags)
    IconButton(onClick = { sheetOpen = true }, Modifier.semantics { contentDescription = allTagsLabel }) {
        Icon(Icons.Filled.KeyboardArrowDown, contentDescription = null)
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
internal fun SwipeableRow(
    modifier: Modifier,
    item: NoteListItem, selected: Boolean, swipeEnabled: Boolean = true, conflicted: Boolean, reminder: java.time.Instant?, onOpen: () -> Unit, signal: NoteSyncSignal.State, channel: Int,
    onMenu: () -> Unit, moves: Set<ReorderPlan.Direction>, onMove: (Int) -> Unit,
    onArchive: () -> Unit, onTrash: () -> Unit, onRestore: () -> Unit, onDelete: () -> Unit,
) {
    // No rememberSaveable: that state was restored already "dismissed" (StartToEnd) when a note came back to the trash after being restored and
    // undone, and the row stopped swiping. It is created anew per note and per state, always at rest.
    val density = androidx.compose.ui.platform.LocalDensity.current
    // A note was archived by accident: the swipe must travel most of the row (70 %) and a quick flick that does not get there is refused
    // (the library also accepts a fast fling from any distance).
    val rowWidth = remember(item.id, item.state) { FloatArray(1) }
    val stateRef = remember(item.id, item.state) { arrayOfNulls<SwipeToDismissBoxState>(1) }
    val dismiss = remember(item.id, item.state) { SwipeToDismissBoxState(
        SwipeToDismissBoxValue.Settled, density, positionalThreshold = { total -> total * 0.7f },
        confirmValueChange = { value ->
            if (value != SwipeToDismissBoxValue.Settled) {
                val travelled = runCatching { kotlin.math.abs(stateRef[0]?.requireOffset() ?: 0f) }.getOrDefault(0f)
                if (rowWidth[0] > 0f && travelled < rowWidth[0] * 0.5f) return@SwipeToDismissBoxState false
            }
            when (value) {
                SwipeToDismissBoxValue.StartToEnd -> when (item.state) {
                    NoteState.ACTIVE -> onTrash()
                    else -> onRestore()
                }
                SwipeToDismissBoxValue.EndToStart -> when (item.state) {
                    NoteState.ACTIVE -> onArchive()
                    NoteState.ARCHIVED -> onTrash()
                    NoteState.TRASHED -> { onDelete(); return@SwipeToDismissBoxState false }
                }
                SwipeToDismissBoxValue.Settled -> Unit
            }
            value != SwipeToDismissBoxValue.Settled
        },
    ).also { stateRef[0] = it } }
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
        enableDismissFromStartToEnd = swipeEnabled, enableDismissFromEndToStart = swipeEnabled,
        modifier = modifier.padding(horizontal = 16.dp).onSizeChanged { rowWidth[0] = it.width.toFloat() }.semantics { customActions = actions; if (selected) stateDescription = openLabel },
        backgroundContent = {
            // Only while the row is actually swiped: drawn at rest, this fill peeks out around the card's anti-aliased edge as a thin outline whose
            // colour depends on the note colour (the "some notes have a border" report).
            val swiped = runCatching { kotlin.math.abs(dismiss.requireOffset()) >= 1f }.getOrDefault(false)
            if (swiped) Box(
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
    val cardColor = when {
        tint != null -> noteFace(tint.face)
        fill -> face
        selected -> MaterialTheme.colorScheme.primaryContainer
        else -> MaterialTheme.colorScheme.surfaceContainerHigh
    }
    // A note whose face is almost the colour of the app background would read as loose text: a faint outline marks the card. Decided by the
    // contrast between the two (not by the note colour), so the same rule gives the same result in every look and theme.
    val blendsIn = contrastRatio(cardColor, MaterialTheme.colorScheme.background) < CARD_OUTLINE_BELOW
    Card(
        onClick = onOpen,
        shape = look.cardShape,
        colors = CardDefaults.cardColors(containerColor = cardColor, contentColor = ink),
        border = when {
            (fill || tint != null) && selected -> BorderStroke(3.dp, MaterialTheme.colorScheme.primary)
            blendsIn -> BorderStroke(1.dp, MaterialTheme.colorScheme.outline)
            else -> null
        },
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
                if (item.pinnedToTop) {
                    PinMark(muted, stringResource(R.string.pinned_mark_cd), Modifier.padding(end = 6.dp).size(18.dp))
                }
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

/** Los ids del grupo de [id] (fijadas o no) en el orden de la lista: Subir/Bajar y el arrastre solo se mueven dentro de su grupo. */
private fun sameGroup(items: List<NoteListItem>, id: String): List<String> {
    val pinned = items.firstOrNull { it.id == id }?.pinnedToTop ?: return emptyList()
    return items.filter { it.pinnedToTop == pinned }.map { it.id }
}

/** Chincheta de «fijada arriba», dibujada: `material-icons-core` no trae ninguna. Cabeza redonda, aro y aguja, inclinada como una chincheta clavada. */
@Composable
private fun PinMark(color: Color, description: String, modifier: Modifier = Modifier) {
    androidx.compose.foundation.Canvas(modifier.semantics { contentDescription = description }) {
        val unit = size.minDimension / 24f
        rotate(degrees = 40f, pivot = Offset(size.width / 2f, size.height / 2f)) {
            // Cabeza (cuerpo) + base más ancha + aguja.
            drawRoundRect(color, Offset(8f * unit, 2f * unit), androidx.compose.ui.geometry.Size(8f * unit, 9f * unit), androidx.compose.ui.geometry.CornerRadius(2f * unit))
            drawRect(color, Offset(5f * unit, 11f * unit), androidx.compose.ui.geometry.Size(14f * unit, 2.4f * unit))
            drawLine(color, Offset(12f * unit, 13.4f * unit), Offset(12f * unit, 22f * unit), strokeWidth = 1.8f * unit, cap = StrokeCap.Round)
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

/** Below this WCAG contrast ratio between a card and the background behind it, the card gets a thin outline. */
private const val CARD_OUTLINE_BELOW = 1.25f

/** WCAG contrast ratio (1 to 21) between two opaque colours. */
internal fun contrastRatio(a: Color, b: Color): Float {
    val la = a.luminance() + 0.05f
    val lb = b.luminance() + 0.05f
    return if (la > lb) la / lb else lb / la
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
        // "All good" is the usual state, so it stays small and dim; the warning and error icons below keep their full weight.
        StatusIcon.OK -> Icon(Icons.Default.Check, description, Modifier.size(18.dp), tint = MaterialTheme.colorScheme.primary.copy(alpha = 0.7f))
        StatusIcon.SYNCING -> CircularProgressIndicator(Modifier.size(20.dp), strokeWidth = 2.dp)
        StatusIcon.OFFLINE -> Icon(Icons.Default.Warning, description, tint = MaterialTheme.colorScheme.onSurfaceVariant)
        StatusIcon.ERROR -> Icon(Icons.Default.Warning, description, tint = MaterialTheme.colorScheme.error)
    }
}

/** Un dedo que mantiene pulsada una fila: si ya abrió el menú o empezó a arrastrar, al levantarlo no se debe abrir la nota. */
/** Cuánto aguanta el dedo quieto, tras la pulsación larga, para abrir el menú sin soltar. */
// After the system long press (~400 ms), how long a still finger waits before the menu opens on its own. 350 ms felt slow for whoever wants the
// menu, 100 ms opened it on people who were only about to drag (the buzz tells them to go, and a reaction takes 200-300 ms): 300 ms is the
// middle. Lifting the finger without moving opens it at once either way.
private const val MENU_HOLD_MS = 300L
private const val MENU_CALM_MS = 700L

@OptIn(androidx.compose.foundation.layout.ExperimentalLayoutApi::class)
@Composable
private fun imeVisible(): Boolean = WindowInsets.isImeVisible

private class PressHold {
    var active = false
    var travelled = 0f
    /** El menú ya se abrió con el dedo todavía puesto: al levantarlo no hay que abrirlo otra vez. */
    var menuShown = false
    /** Bumped on every press: a delayed menu request from an earlier press must not fire for a later one. */
    var press = 0
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
private fun NoBackupNotice(message: Int, onSettings: () -> Unit, onDismiss: () -> Unit) {
    androidx.compose.material3.Surface(
        Modifier.fillMaxWidth().padding(horizontal = 16.dp, vertical = 4.dp),
        shape = MaterialTheme.shapes.medium, color = MaterialTheme.colorScheme.surfaceVariant,
    ) {
        Column(Modifier.padding(start = 12.dp, end = 4.dp, top = 8.dp, bottom = 0.dp)) {
            Text(stringResource(message), style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
            Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.End) {
                // Botones de texto con menos relleno: el alto táctil sigue siendo el mínimo, pero el aviso deja de ocupar media pantalla.
                TextButton(onClick = onDismiss, contentPadding = androidx.compose.foundation.layout.PaddingValues(horizontal = 12.dp)) { Text(stringResource(R.string.no_backup_dismiss)) }
                TextButton(onClick = onSettings, contentPadding = androidx.compose.foundation.layout.PaddingValues(horizontal = 12.dp)) { Text(stringResource(R.string.settings)) }
            }
        }
    }
}


/** The bar shown instead of the tabs while notes are picked: close, count, select all and the bulk actions that fit the current list. */
@Composable
private fun SelectionBar(
    count: Int, filter: NoteState, onAll: () -> Unit, onExit: () -> Unit,
    onArchive: () -> Unit, onTrash: () -> Unit, onRestore: () -> Unit, onTag: () -> Unit,
) {
    Row(Modifier.fillMaxWidth().padding(end = 8.dp), verticalAlignment = Alignment.CenterVertically) {
        val exit = stringResource(R.string.selection_exit)
        IconButton(onClick = onExit, Modifier.semantics { contentDescription = exit }) { Icon(Icons.Filled.Close, contentDescription = null) }
        Text(stringResource(R.string.selection_count, count), Modifier.weight(1f), style = MaterialTheme.typography.titleMedium, maxLines = 1, overflow = TextOverflow.Ellipsis)
        TextButton(onClick = onAll) { Text(stringResource(R.string.selection_all)) }
    }
    Row(Modifier.fillMaxWidth().padding(horizontal = 8.dp), horizontalArrangement = Arrangement.spacedBy(4.dp)) {
        if (filter == NoteState.ACTIVE) TextButton(onClick = onArchive) { Text(stringResource(R.string.archive)) } else TextButton(onClick = onRestore) { Text(stringResource(R.string.restore)) }
        if (filter != NoteState.TRASHED) TextButton(onClick = onTrash) { Text(stringResource(R.string.move_to_trash)) }
        if (filter != NoteState.TRASHED) TextButton(onClick = onTag) { Text(stringResource(R.string.selection_tag)) }
    }
}

/** Pick one of the existing tags (or write a new one) to add to every selected note. */
@Composable
private fun TagPickedDialog(tags: List<String>, onPick: (String) -> Unit, onDismiss: () -> Unit) {
    var typed by remember { mutableStateOf("") }
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text(stringResource(R.string.selection_tag_title), Modifier.semantics { heading() }) },
        text = {
            Column(Modifier.verticalScroll(rememberScrollState())) {
                androidx.compose.material3.OutlinedTextField(
                    typed, { typed = it }, label = { Text(stringResource(R.string.selection_new_tag)) }, singleLine = true, modifier = Modifier.fillMaxWidth(),
                )
                for (tag in tags) Text(tag, Modifier.fillMaxWidth().clickable { onPick(tag) }.padding(vertical = 12.dp), style = MaterialTheme.typography.bodyLarge)
            }
        },
        confirmButton = { TextButton(onClick = { onPick(typed.trim()) }, enabled = typed.isNotBlank()) { Text(stringResource(R.string.selection_tag)) } },
        dismissButton = { TextButton(onClick = onDismiss) { Text(stringResource(R.string.update_dismiss)) } },
    )
}

/** Una fila de la lista: una nota o, en la vista agrupada, la cabecera plegable de una etiqueta. [key] es única aunque la nota salga en varias secciones. */
private sealed interface ListEntry {
    val key: String
    class Note(val item: NoteListItem, override val key: String) : ListEntry
    class Header(val title: String, override val key: String, val count: Int, val open: Boolean) : ListEntry
}


@Composable
private fun GroupHeader(header: ListEntry.Header, modifier: Modifier = Modifier, highlighted: Boolean = false, onMenu: (() -> Unit)? = null, onToggle: () -> Unit) {
    val state = stringResource(if (header.open) R.string.group_expanded else R.string.group_collapsed)
    Row(
        modifier.fillMaxWidth().then(if (highlighted) Modifier.background(MaterialTheme.colorScheme.primary.copy(alpha = 0.18f), RoundedCornerShape(12.dp)) else Modifier)
            .padding(start = 16.dp, end = 8.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        // Arrow, title and count are one thing to tap (fold or unfold) and to read aloud; the options button is its own, so TalkBack keeps both.
        Row(
            Modifier.weight(1f).heightIn(min = 48.dp).clickable(onClick = onToggle).semantics(mergeDescendants = true) { heading(); stateDescription = state },
            verticalAlignment = Alignment.CenterVertically,
        ) {
            Text(if (header.open) "▾" else "▸", Modifier.padding(end = 8.dp), style = MaterialTheme.typography.titleMedium)
            Text(header.title, Modifier.weight(1f), style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.SemiBold)
            Text(header.count.toString(), style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurfaceVariant)
        }
        // A 48 dp column on the right with the options of the tag (three dots); the headers without options («No tag») leave it empty, so every count
        // stays in the same column.
        Box(Modifier.width(48.dp).height(48.dp), contentAlignment = Alignment.Center) {
            if (onMenu != null) {
                val cd = stringResource(R.string.tag_options_cd, header.title)
                IconButton(onClick = onMenu, Modifier.semantics { contentDescription = cd }) { Icon(Icons.Filled.MoreVert, null) }
            }
        }
    }
}

/**
 * The options of a tag: rename it, delete it (the notes stay) or delete it with its notes (they go to the trash and can be restored). The two
 * deletions ask first: they touch many notes at once.
 */
@Composable
private fun TagOptionsDialogs(tag: String, onDismiss: () -> Unit, onRename: (String) -> Unit, onDelete: (withNotes: Boolean) -> Unit) {
    var step by remember(tag) { mutableStateOf(0) } // 0 menu, 1 rename, 2 confirm delete, 3 confirm delete with notes
    // The name starts selected, so typing replaces it.
    var typed by remember(tag) { mutableStateOf(androidx.compose.ui.text.input.TextFieldValue(tag, androidx.compose.ui.text.TextRange(0, tag.length))) }
    val nameFocus = remember { androidx.compose.ui.focus.FocusRequester() }
    when (step) {
        0 -> androidx.compose.material3.AlertDialog(
            onDismissRequest = onDismiss,
            title = { Text("#$tag") },
            text = {
                // No horizontal padding in the buttons: their text starts where the title does (the default padding indented it 12 dp).
                val rowPadding = androidx.compose.foundation.layout.PaddingValues(horizontal = 0.dp, vertical = 12.dp)
                Column {
                    TextButton({ step = 1 }, Modifier.fillMaxWidth(), contentPadding = rowPadding) { Text(stringResource(R.string.tag_rename), Modifier.fillMaxWidth()) }
                    TextButton({ step = 2 }, Modifier.fillMaxWidth(), contentPadding = rowPadding) { Text(stringResource(R.string.tag_remove), Modifier.fillMaxWidth()) }
                    TextButton({ step = 3 }, Modifier.fillMaxWidth(), contentPadding = rowPadding) { Text(stringResource(R.string.tag_remove_with_notes), Modifier.fillMaxWidth(), color = MaterialTheme.colorScheme.error) }
                }
            },
            confirmButton = { TextButton(onDismiss) { Text(stringResource(R.string.cancel)) } },
        )
        1 -> androidx.compose.material3.AlertDialog(
            onDismissRequest = onDismiss,
            title = { Text(stringResource(R.string.tag_rename_title)) },
            text = {
                androidx.compose.material3.OutlinedTextField(
                    typed, { typed = it.copy(text = it.text.replace("\n", " ")) }, Modifier.focusRequester(nameFocus), singleLine = true,
                    label = { Text(stringResource(R.string.tag_new_name)) },
                )
                LaunchedEffect(Unit) { nameFocus.requestFocus() }
            },
            confirmButton = { TextButton({ onRename(typed.text); onDismiss() }, enabled = typed.text.isNotBlank() && typed.text.trim() != tag) { Text(stringResource(R.string.tag_rename)) } },
            dismissButton = { TextButton(onDismiss) { Text(stringResource(R.string.cancel)) } },
        )
        else -> androidx.compose.material3.AlertDialog(
            onDismissRequest = onDismiss,
            title = { Text("#$tag") },
            text = { Text(stringResource(if (step == 2) R.string.tag_remove_confirm else R.string.tag_remove_notes_confirm, tag)) },
            confirmButton = {
                TextButton({ onDelete(step == 3); onDismiss() }) {
                    Text(stringResource(if (step == 2) R.string.tag_confirm_remove else R.string.tag_confirm_trash), color = MaterialTheme.colorScheme.error)
                }
            },
            dismissButton = { TextButton(onDismiss) { Text(stringResource(R.string.cancel)) } },
        )
    }
}
