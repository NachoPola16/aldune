package app.aldune.ui

import androidx.compose.material3.Text
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.text.TextLayoutResult
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.rememberTextMeasurer
import androidx.compose.ui.unit.sp
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Rule
import org.junit.Test

/** The tap target of a task checkbox: the box plus the space after it, the whole line high; the task text is not part of it. */
class CheckboxTapTest {
    @get:Rule val rule = createComposeRule()

    private val text = "☐ tarea uno\n☐ tarea dos"

    private fun layout(): TextLayoutResult {
        lateinit var result: TextLayoutResult
        rule.setContent {
            val measurer = rememberTextMeasurer()
            result = measurer.measure(text, TextStyle(fontSize = 18.sp))
            Text("")
        }
        rule.waitForIdle()
        return result
    }

    @Test fun boxAndTheGapAfterItTickTheLineButTheTextDoesNot() {
        val layout = layout()
        val box = layout.getBoundingBox(0)
        val gapEnd = layout.getBoundingBox(1).right
        val y = (box.top + box.bottom) / 2
        assertEquals(0, toggleCheckboxAtTap(text, layout, Offset(box.center.x, y), 8f))
        // The middle of the gap between the box and the first letter used to resolve to the text and put the cursor there.
        assertEquals(0, toggleCheckboxAtTap(text, layout, Offset((box.right + gapEnd) / 2, y), 8f))
        assertEquals(0, toggleCheckboxAtTap(text, layout, Offset(gapEnd - 1f, y), 8f))
        // Left of the box (the field's left margin, where a thumb lands) counts too.
        assertEquals(0, toggleCheckboxAtTap(text, layout, Offset(-40f, y), 8f))
        assertNull(toggleCheckboxAtTap(text, layout, Offset(gapEnd + layout.getBoundingBox(2).width, y), 8f))
    }

    @Test fun theSecondLineResolvesToItsOwnBox() {
        val layout = layout()
        val second = text.indexOf('\n') + 1
        val box = layout.getBoundingBox(second)
        assertEquals(second, toggleCheckboxAtTap(text, layout, Offset(box.center.x, (box.top + box.bottom) / 2), 8f))
    }
}
