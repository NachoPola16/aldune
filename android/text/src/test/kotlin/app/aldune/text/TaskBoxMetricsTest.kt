package app.aldune.text

import kotlin.math.abs
import org.junit.jupiter.api.Test
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertTrue

/**
 * Where the ink of a task box sits on its line, so the drag handle can share its centre whatever the note font and the line height are.
 * Measured on the emulator (1080x2400, 420 dpi, 42 px text, 2026-11-12): baseline of the first line at 259 px, large box drawn from 226 to
 * 263 px (centre 244.5); the normal box is the height of the capitals (30 px) standing on the baseline.
 */
class TaskBoxMetricsTest {
    @Test fun `the centre of the large box hangs from the baseline`() {
        assertTrue(abs(TaskBoxMetrics.centreY(baseline = 259f, fontSize = 42f, large = true) - 244.5f) <= 0.5f)
    }

    @Test fun `the centre of the normal box is the middle of the capitals`() {
        assertTrue(abs(TaskBoxMetrics.centreY(baseline = 259f, fontSize = 42f, large = false) - 244f) <= 0.5f)
    }

    @Test fun `the offset grows with the text size and does not depend on the line height`() {
        val small = 100f - TaskBoxMetrics.centreY(100f, 20f, large = false)
        val big = 300f - TaskBoxMetrics.centreY(300f, 40f, large = false)
        assertEquals(small * 2, big, 0.001f)
    }
}
