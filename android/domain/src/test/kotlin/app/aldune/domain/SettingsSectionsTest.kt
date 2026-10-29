package app.aldune.domain

import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertTrue

class SettingsSectionsTest {
    @Test
    fun `el modo completo ensena todas las secciones`() {
        for (section in SettingsSection.entries) assertTrue(SettingsSections.isVisible(section, simplified = false), "$section")
    }

    @Test
    fun `el modo simplificado deja sincronizacion, aspecto, idioma, la version y el interruptor de modo`() {
        val visible = SettingsSection.entries.filter { SettingsSections.isVisible(it, simplified = true) }.toSet()
        assertEquals(
            setOf(
                SettingsSection.SYNC, SettingsSection.BACKUP, SettingsSection.APPEARANCE, SettingsSection.LANGUAGE, SettingsSection.ABOUT,
                SettingsSection.INTERFACE_MODE,
            ),
            visible,
        )
    }

    @Test
    fun `el interruptor de modo esta siempre a la vista, o no habria como volver al completo`() {
        assertTrue(SettingsSections.isVisible(SettingsSection.INTERFACE_MODE, simplified = true))
        assertTrue(SettingsSections.isVisible(SettingsSection.INTERFACE_MODE, simplified = false))
    }
}
