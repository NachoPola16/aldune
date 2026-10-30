package app.aldune.ui

import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.test.assertCountEquals
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.test.onAllNodesWithText
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performTouchInput
import androidx.compose.ui.test.swipe
import androidx.compose.ui.geometry.Offset
import app.aldune.domain.NoteListItem
import app.aldune.domain.NoteState
import org.junit.Assert.assertEquals
import org.junit.Rule
import org.junit.Test

/** Trash: restore, "Undo" and swipe the same note again (the bug opened the note instead of asking to delete it). */
class TrashSwipeTest {
    @get:Rule val rule = createComposeRule()

    private fun note(id: String, state: NoteState) =
        NoteListItem(id, "Note $id", "", "#FFE08A", 0, 0, emptyList(), state)

    /** Control: without restoring first, the same gesture does ask to delete (if it fails, the test gesture is not enough). */
    @Test fun controlSwipeLeftAsksToDelete() {
        val notes = listOf(note("a", NoteState.TRASHED))
        val asked = mutableListOf<String>()
        rule.setContent {
            AldunaTheme {
                LazyColumn(Modifier) {
                    items(notes, key = { it.id }) { item ->
                        SwipeableRow(
                            Modifier, item, selected = false, conflicted = false, reminder = null, onOpen = {},
                            signal = app.aldune.text.NoteSyncSignal.State.HIDDEN, channel = 0, onMenu = {}, moves = emptySet(), onMove = {},
                            onArchive = {}, onTrash = {}, onRestore = {}, onDelete = { asked += item.id },
                        )
                    }
                }
            }
        }
        rule.onNodeWithText("Note a").performTouchInput { swipe(Offset(width * 0.9f, centerY), Offset(width * 0.1f, centerY), 300) }
        rule.waitForIdle()
        assertEquals(setOf("a"), asked.toSet())
    }

    @Test fun swipeAgainAfterRestoreAndUndoAsksToDeleteAndDoesNotOpen() {
        var notes by mutableStateOf(listOf(note("a", NoteState.TRASHED), note("b", NoteState.TRASHED)))
        var opened = 0
        var asked = mutableListOf<String>()
        rule.setContent {
            AldunaTheme {
                LazyColumn(Modifier) {
                    items(notes.filter { it.state == NoteState.TRASHED }, key = { it.id }) { item ->
                        SwipeableRow(
                            Modifier, item, selected = false, conflicted = false, reminder = null, onOpen = { opened++ },
                            signal = app.aldune.text.NoteSyncSignal.State.HIDDEN, channel = 0, onMenu = {}, moves = emptySet(), onMove = {},
                            onArchive = {}, onTrash = {},
                            onRestore = { notes = notes.map { if (it.id == item.id) it.copy(state = NoteState.ACTIVE) else it } },
                            onDelete = { asked += item.id },
                        )
                    }
                }
            }
        }
        rule.onNodeWithText("Note a").performTouchInput { swipe(Offset(width * 0.1f, centerY), Offset(width * 0.9f, centerY), 300) }
        rule.waitForIdle()
        rule.onAllNodesWithText("Note a").assertCountEquals(0)
        // "Undo": back to the trash.
        notes = notes.map { if (it.id == "a") it.copy(state = NoteState.TRASHED) else it }
        rule.waitForIdle()
        rule.onNodeWithText("Note a").performTouchInput { swipe(Offset(width * 0.9f, centerY), Offset(width * 0.1f, centerY), 300) }
        rule.waitForIdle()
        assertEquals(setOf("a"), asked.toSet())
        assertEquals(0, opened)
    }
}
