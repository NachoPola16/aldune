package app.aldune.text

import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertFalse
import org.junit.jupiter.api.Assertions.assertNull
import org.junit.jupiter.api.Assertions.assertTrue
import org.junit.jupiter.api.Test

class AspectCatalogTest {
    @Test fun `los retro son los seis de la spec y las cuatro libretas con sus numeros`() {
        assertEquals(listOf(5, 6, 7, 8, 9, 10, 11, 12, 13, 14), AspectCatalog.retro.map { it.mode })
        assertEquals(
            listOf(
                "xp-light", "xp-dark", "telecom-light", "telecom-dark", "bash", "phosphor",
                "travel-notebook-light", "travel-notebook-dark", "field-notebook-light", "field-notebook-dark",
            ),
            AspectCatalog.retro.map { it.id },
        )
    }

    @Test fun `los aspectos de siempre no tienen entrada en el catalogo`() {
        for (mode in 0..4) assertNull(AspectCatalog.forMode(mode))
    }

    @Test fun `cada hueco tiene un valor por defecto y muestras validos`() {
        for (aspect in AspectCatalog.retro) for (slot in aspect.slots) {
            assertTrue(NoteDisplayColor.isActive(slot.default), "${aspect.id}.${slot.id}")
            for (s in slot.suggestions) assertTrue(NoteDisplayColor.isActive(s), "${aspect.id}.${slot.id} $s")
        }
    }

    @Test fun `las paletas de bash rellenan todos los huecos`() {
        val bash = AspectCatalog.forMode(AppPalette.BASH)!!
        assertEquals(listOf("gruvbox", "ubuntu", "tango"), bash.presets.map { it.id })
        for (preset in bash.presets) assertEquals(bash.slots.map { it.id }.sorted(), preset.colors.keys.sorted())
    }

    @Test fun `resolve sin nada elegido da los valores de fabrica`() {
        val colors = AspectCatalog.resolve(AppPalette.XP_LIGHT, null)
        assertEquals("#0055E5", colors["titleBar"])
        assertEquals("#3C9A3C", colors["accent"])
    }

    @Test fun `resolve conserva lo valido, pone en mayusculas e ignora el resto`() {
        val colors = AspectCatalog.resolve(
            AppPalette.XP_LIGHT, mapOf("titleBar" to "#5a7a2e", "accent" to "rojo", "unknown" to "#123456"),
        )
        assertEquals("#5A7A2E", colors["titleBar"])
        assertEquals("#3C9A3C", colors["accent"])
        assertFalse(colors.containsKey("unknown"))
    }

    @Test fun `resolve de un aspecto de siempre da un mapa vacio`() =
        assertEquals(emptyMap<String, String>(), AspectCatalog.resolve(AppPalette.DARK, mapOf("accent" to "#FF0000")))

    @Test fun `los temas sugeridos siguen la spec`() {
        assertEquals(NoteThemes.XP_ID, AspectCatalog.forMode(AppPalette.XP_LIGHT)!!.suggestedThemeId)
        assertNull(AspectCatalog.forMode(AppPalette.TELECOM_LIGHT)!!.suggestedThemeId)
        for (mode in listOf(AppPalette.XP_DARK, AppPalette.TELECOM_DARK, AppPalette.BASH, AppPalette.PHOSPHOR))
            assertEquals(NoteThemes.SERENE_ID, AspectCatalog.forMode(mode)!!.suggestedThemeId)
    }
}
