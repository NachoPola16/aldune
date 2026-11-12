package app.aldune.domain

import kotlin.test.Test
import kotlin.test.assertEquals

/**
 * The device switches for done tasks («reorder on check», «hide done tasks») reach only the notes left on «as in my settings»; a note with a
 * choice of its own keeps it. And «as in my settings» is stored as no choice (0), never as a copy of the switch at that moment.
 */
class CompletedTasksInheritanceTest {
    private fun effective(f: Fixture, id: String, reorder: Boolean, hide: Boolean): Pair<Boolean, Boolean> {
        val policy = f.stored(id).completedTasks
        return CompletedTasksPolicy.shouldMoveToEnd(policy, reorder) to CompletedTasksPolicy.mayHide(policy, hide)
    }

    @Test fun `changing the device switches changes only the inherited note`() {
        val f = Fixture()
        val inherited = f.create("☐ a")
        val keeps = f.create("☐ b")
        val moves = f.create("☐ c")
        f.service.setCompletedTasks(keeps.id, CompletedTasksPolicy.KEEP_IN_PLACE)
        f.service.setCompletedTasks(moves.id, CompletedTasksPolicy.MOVE_TO_END)

        assertEquals(false to false, effective(f, inherited.id, reorder = false, hide = false))
        assertEquals(true to true, effective(f, inherited.id, reorder = true, hide = true))
        for (switch in listOf(false, true)) {
            assertEquals(false to false, effective(f, keeps.id, reorder = switch, hide = switch))
            assertEquals(true to false, effective(f, moves.id, reorder = switch, hide = switch))
        }
    }

    @Test fun `going back to as in my settings stores no value of its own`() {
        val f = Fixture()
        val note = f.create("☐ a")
        f.service.setCompletedTasks(note.id, CompletedTasksPolicy.MOVE_TO_END)

        // The menu item cycles: move to the end -> as in my settings.
        f.service.setCompletedTasks(note.id, CompletedTasksPolicy.next(CompletedTasksPolicy.MOVE_TO_END))

        assertEquals(CompletedTasksPolicy.DEFAULT, f.stored(note.id).completedTasks)
        assertEquals(true to false, effective(f, note.id, reorder = true, hide = false))
        assertEquals(false to true, effective(f, note.id, reorder = false, hide = true))
    }
}
