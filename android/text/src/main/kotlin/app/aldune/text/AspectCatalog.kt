package app.aldune.text

import java.util.Locale

/** Un color que el usuario puede cambiar en un aspecto, con muestras de un toque. */
data class AspectColorSlot(val id: String, val default: String, val suggestions: List<String>)

/** Varios huecos de una vez (las paletas de terminal de Bash). */
data class AspectPreset(val id: String, val name: String, val colors: Map<String, String>)

/** `id` es la clave estable de `aspectColors`: no cambiar nunca. `suggestedThemeId` es el tema de notas que el aspecto propone, o `null`. */
data class AspectDefinition(
    val mode: Int, val id: String, val slots: List<AspectColorSlot>, val presets: List<AspectPreset>, val suggestedThemeId: String?,
)

/** Los aspectos retro y sus colores personalizables (port de `AspectCatalog` de Core). */
object AspectCatalog {
    val retro: List<AspectDefinition> = listOf(
        AspectDefinition(
            AppPalette.XP_LIGHT, "xp-light",
            listOf(
                AspectColorSlot("titleBar", "#0055E5", listOf("#0055E5", "#5A7A2E", "#8C8CA8", "#A8452A")), // Luna azul, oliva, plata, teja
                AspectColorSlot("accent", "#3C9A3C", listOf("#3C9A3C", "#0055E5", "#C46A1C")),
            ),
            emptyList(), NoteThemes.XP_ID,
        ),
        AspectDefinition(
            AppPalette.XP_DARK, "xp-dark",
            listOf(
                AspectColorSlot("titleBar", "#1C4FA8", listOf("#1C4FA8", "#008080", "#6B2E8A", "#8A2E2E")),
                AspectColorSlot("accent", "#4C86E8", listOf("#4C86E8", "#2EA8A8", "#C9A227")),
            ),
            emptyList(), NoteThemes.SERENE_ID,
        ),
        AspectDefinition(
            AppPalette.TELECOM_LIGHT, "telecom-light",
            listOf(
                AspectColorSlot("ink", "#1C2A22", listOf("#1C2A22", "#1F2A44", "#3A2418")),
                AspectColorSlot("accent", "#2F7A3C", listOf("#2F7A3C", "#1F57B5", "#9C1F6E")),
            ),
            emptyList(), null,
        ),
        AspectDefinition(
            AppPalette.TELECOM_DARK, "telecom-dark",
            listOf(
                AspectColorSlot("panel", "#171C21", listOf("#171C21", "#10161C", "#1B1B1B")),
                AspectColorSlot("trace", "#F2D338", listOf("#F2D338", "#3FD0E0", "#7CFC7C")),
            ),
            emptyList(), NoteThemes.SERENE_ID,
        ),
        AspectDefinition(
            AppPalette.BASH, "bash",
            listOf(
                AspectColorSlot("background", "#282828", listOf("#282828", "#300A24", "#1E1E1E")),
                AspectColorSlot("user", "#B8BB26", listOf("#B8BB26", "#8AE234", "#FABD2F", "#8EC07C")),
                AspectColorSlot("path", "#83A598", listOf("#83A598", "#729FCF", "#D3869B", "#FE8019")),
                AspectColorSlot("accent", "#D79921", listOf("#D79921", "#458588", "#98971A", "#B16286")),
            ),
            listOf(
                // Gruvbox por defecto: cálido y apagado, nada de colores fuertes (decidido con el usuario en Windows).
                AspectPreset("gruvbox", "Gruvbox", mapOf("background" to "#282828", "user" to "#B8BB26", "path" to "#83A598", "accent" to "#D79921")),
                AspectPreset("ubuntu", "Ubuntu", mapOf("background" to "#300A24", "user" to "#8AE234", "path" to "#729FCF", "accent" to "#E95420")),
                AspectPreset("tango", "Tango", mapOf("background" to "#1E1E1E", "user" to "#8AE234", "path" to "#729FCF", "accent" to "#3465A4")),
            ),
            NoteThemes.SERENE_ID,
        ),
        AspectDefinition(
            AppPalette.PHOSPHOR, "phosphor",
            listOf(
                AspectColorSlot("phosphor", "#A8E6B4", listOf("#A8E6B4", "#FFB547", "#8EC9FF", "#E8E8E8")), // verde, ámbar, azul, blanco
                AspectColorSlot("details", "#D9A441", listOf("#D9A441", "#A8E6B4", "#E8E8E8")),
            ),
            emptyList(), NoteThemes.SERENE_ID,
        ),
    )

    fun forMode(mode: Int): AspectDefinition? = retro.firstOrNull { it.mode == mode }

    /**
     * Colores efectivos de un aspecto: los elegidos que sean `#RRGGBB` válidos (en mayúsculas) y, para el resto, los de fábrica. Vacío para los
     * aspectos que no tienen huecos.
     */
    fun resolve(mode: Int, chosen: Map<String, String>?): Map<String, String> {
        val aspect = forMode(mode) ?: return emptyMap()
        val result = linkedMapOf<String, String>()
        for (slot in aspect.slots) {
            val value = chosen?.get(slot.id)
            result[slot.id] = if (value != null && NoteDisplayColor.isActive(value)) value.uppercase(Locale.ROOT) else slot.default
        }
        return result
    }
}
