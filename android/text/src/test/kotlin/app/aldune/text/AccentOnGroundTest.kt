package app.aldune.text

import org.junit.jupiter.api.Assertions.assertTrue
import org.junit.jupiter.api.Test

/**
 * The list screen now draws with the look's accent (outline of the chosen tag chip, arrow of each group, fill of the new-note button), so the
 * accent has to be seen against the screen: 3:1 for graphics and outlines. Its label colour (`OnAccent`) is already fixed at 4.5:1 by
 * `AspectPalettesTest`. Covers every look with its own colours and with each of the colour suggestions the settings offer.
 */
class AccentOnGroundTest {
    private fun palettes(): List<Pair<String, Map<String, String>>> = buildList {
        for (mode in listOf(AppPalette.DARK, AppPalette.LIGHT, AppPalette.PASTEL, AppPalette.MIDNIGHT)) add("mode $mode" to AppPalette.forMode(mode, false))
        for (aspect in AspectCatalog.retro) {
            add(aspect.id to AppPalette.forMode(aspect.mode, false))
            for (slot in aspect.slots) for (suggestion in slot.suggestions) {
                add("${aspect.id}/${slot.id}=$suggestion" to AppPalette.forMode(aspect.mode, false, mapOf(slot.id to suggestion)))
            }
            for (preset in aspect.presets) add("${aspect.id}/${preset.id}" to AppPalette.forMode(aspect.mode, false, preset.colors))
        }
    }

    @Test fun `the accent is seen against the ground, the surface and the raised surface (dialogs, menus) of every look`() {
        for ((label, p) in palettes()) for (background in listOf("Ground", "Surface", "Raised")) {
            val ratio = ColorFit.ratio(p.getValue("Accent"), p.getValue(background))
            assertTrue(ratio >= 3.0, "$label: Accent on $background: %.2f < 3.0".format(ratio))
        }
    }
}
