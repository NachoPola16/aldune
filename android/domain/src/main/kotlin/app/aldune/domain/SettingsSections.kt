package app.aldune.domain

/** Los bloques de la pantalla de Ajustes que el modo simplificado puede esconder. */
enum class SettingsSection { SYNC, CAPTURE, REMINDERS, APPEARANCE, APPEARANCE_ADVANCED, LANGUAGE, NOTES, ABOUT, INTERFACE_MODE }

/**
 * Qué se enseña en Ajustes según el modo (igual que el modo simplificado de Windows: lo esencial a la vista y lo demás con sus valores por
 * defecto, que ya valen para empezar). Aquí y no en la pantalla para poder probarlo; la pantalla solo pregunta por cada bloque.
 */
object SettingsSections {
    private val essential = setOf(SettingsSection.SYNC, SettingsSection.APPEARANCE, SettingsSection.LANGUAGE, SettingsSection.ABOUT)

    /** El interruptor de modo no se esconde nunca: sin él no habría forma de volver al modo completo. */
    fun isVisible(section: SettingsSection, simplified: Boolean): Boolean =
        !simplified || section == SettingsSection.INTERFACE_MODE || section in essential
}
