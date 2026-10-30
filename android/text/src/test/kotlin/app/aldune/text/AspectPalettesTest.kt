package app.aldune.text

import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertTrue
import org.junit.jupiter.api.Test

class AspectPalettesTest {
    private val extremes = listOf("#FFFFFF", "#000000", "#FFFF00")

    /** Cada retro con sus colores de fábrica y con un color extremo en todos los huecos. */
    private fun palettes(): List<Pair<String, Map<String, String>>> = buildList {
        for (aspect in AspectCatalog.retro) {
            add(aspect.id to AppPalette.forMode(aspect.mode, false))
            for (extreme in extremes) {
                val colors = aspect.slots.associate { it.id to extreme }
                add("${aspect.id}/$extreme" to AppPalette.forMode(aspect.mode, false, colors))
            }
        }
    }

    private fun contrast(p: Map<String, String>, fg: String, bg: String, minimum: Double, label: String) {
        val ratio = ColorFit.ratio(p.getValue(fg), p.getValue(bg))
        assertTrue(ratio >= minimum, "$label: $fg sobre $bg: %.2f < %.1f".format(ratio, minimum))
    }

    @Test fun `cada paleta define exactamente las mismas claves que Oscuro`() {
        for ((label, p) in palettes()) assertEquals(AppPalette.dark.keys.sorted(), p.keys.sorted(), label)
    }

    @Test fun `cada clave es un color hexadecimal valido`() {
        val hex = Regex("^#([0-9A-F]{6}|[0-9A-F]{8})$")
        for ((label, p) in palettes()) for ((key, value) in p) assertTrue(hex.matches(value.uppercase()), "$label $key $value")
    }

    @Test fun `el texto se lee sobre todos los fondos del chrome`() {
        for ((label, p) in palettes()) {
            for (text in listOf("Text", "TextStrong", "TextSoft", "TextWarm")) for (ground in listOf("Ground", "Surface", "Raised", "Popup"))
                contrast(p, text, ground, 4.5, label)
            contrast(p, "MutedText", "Ground", 4.5, label)
            contrast(p, "MutedText", "Surface", 4.5, label)
            contrast(p, "Hint", "Ground", 3.5, label)
            contrast(p, "DangerText", "Ground", 4.5, label)
            contrast(p, "ErrorText", "Ground", 4.5, label)
        }
    }

    @Test fun `los botones se leen`() {
        for ((label, p) in palettes()) {
            contrast(p, "OnAccent", "Accent", 4.5, label)
            contrast(p, "OnDanger", "DangerStrong", 4.5, label)
            contrast(p, "Text", "DangerBg", 4.5, label)
            contrast(p, "Text", "Hover", 4.5, label)
            contrast(p, "TextStrong", "DockPrimary", 4.5, label)
        }
    }

    @Test fun `la barra de titulo y el prompt se leen`() {
        for ((label, p) in palettes()) {
            contrast(p, "OnTitleBar", "TitleBar", 4.5, label)
            contrast(p, "OnTitleBar", "TitleBarEnd", 4.5, label)
            contrast(p, "PromptUser", "Ground", 4.5, label)
            contrast(p, "PromptPath", "Ground", 4.5, label)
        }
    }

    @Test fun `el color elegido se nota con su matiz`() {
        val xp = AppPalette.forMode(AppPalette.XP_LIGHT, false, mapOf("titleBar" to "#A8452A"))
        val chosen = OklchColor.fromHex("#A8452A")!!
        val bar = OklchColor.fromHex(xp.getValue("TitleBarEnd"))!!
        assertTrue(Math.abs(chosen.h - bar.h) < 15, "${chosen.h} -> ${bar.h}")

        val bash = AppPalette.forMode(AppPalette.BASH, false, mapOf("user" to "#8AE234"))
        val user = OklchColor.fromHex(bash.getValue("PromptUser"))!!
        assertTrue(Math.abs(OklchColor.fromHex("#8AE234")!!.h - user.h) < 15)
    }

    // Un fósforo blanco o un fondo gris no tienen matiz: la paleta no puede salir de un color inventado (Review Focus 1).
    @Test fun `una eleccion gris da una paleta gris`() {
        for ((mode, slot, grey) in listOf(Triple(AppPalette.PHOSPHOR, "phosphor", "#E8E8E8"), Triple(AppPalette.BASH, "background", "#1E1E1E"))) {
            val p = AppPalette.forMode(mode, false, mapOf(slot to grey))
            for (key in listOf("Ground", "Surface", "Raised", "Text")) assertTrue(OklchColor.fromHex(p.getValue(key))!!.c < 0.02, "$key ${p[key]}")
        }
    }

    @Test fun `el fondo elegido es el Ground`() {
        assertEquals("#282828", AppPalette.forMode(AppPalette.BASH, false).getValue("Ground"))
        assertEquals("#171C21", AppPalette.forMode(AppPalette.TELECOM_DARK, false).getValue("Ground"))
        assertEquals("#2B231A", AppPalette.forMode(AppPalette.TRAVEL_NOTEBOOK_DARK, false).getValue("Ground"))
        assertEquals("#27241F", AppPalette.forMode(AppPalette.FIELD_NOTEBOOK_DARK, false).getValue("Ground"))
    }

    @Test fun `el boton mas del dock sigue al acento`() {
        for ((mode, slot) in listOf(
            AppPalette.XP_LIGHT to "accent", AppPalette.XP_DARK to "accent", AppPalette.TELECOM_LIGHT to "accent",
            AppPalette.TELECOM_DARK to "trace", AppPalette.BASH to "accent", AppPalette.PHOSPHOR to "details",
            AppPalette.TRAVEL_NOTEBOOK_LIGHT to "accent", AppPalette.TRAVEL_NOTEBOOK_DARK to "accent",
            AppPalette.FIELD_NOTEBOOK_LIGHT to "accent", AppPalette.FIELD_NOTEBOOK_DARK to "accent",
        )) {
            val p = AppPalette.forMode(mode, false, mapOf(slot to "#D03020"))
            val dock = OklchColor.fromHex(p.getValue("DockPrimary"))!!
            assertTrue(dock.c > 0.05 && Math.abs(OklchColor.fromHex("#D03020")!!.h - dock.h) < 15, "mode $mode: h ${dock.h} c ${dock.c}")
        }
    }

    @Test fun `los aspectos de siempre ignoran los colores de aspecto`() {
        val colors = mapOf("accent" to "#FF0000")
        for (mode in listOf(0, 1, 3, 4)) assertEquals(AppPalette.forMode(mode, false), AppPalette.forMode(mode, false, colors))
    }

    // Review Focus 5: la claridad de cada modo decide la barra de estado y el widget.
    @Test fun `isLight de los modos retro`() {
        assertTrue(AppPalette.isLight(AppPalette.XP_LIGHT, false))
        assertTrue(AppPalette.isLight(AppPalette.TELECOM_LIGHT, false))
        assertTrue(AppPalette.isLight(AppPalette.TRAVEL_NOTEBOOK_LIGHT, false))
        assertTrue(AppPalette.isLight(AppPalette.FIELD_NOTEBOOK_LIGHT, false))
        for (mode in listOf(AppPalette.XP_DARK, AppPalette.TELECOM_DARK, AppPalette.BASH, AppPalette.PHOSPHOR, AppPalette.TRAVEL_NOTEBOOK_DARK, AppPalette.FIELD_NOTEBOOK_DARK))
            assertEquals(false, AppPalette.isLight(mode, true), "mode $mode")
    }
}
