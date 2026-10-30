package app.aldune.text

/**
 * Paleta del «chrome» de la app (fondos, textos, acentos), port de `AppPalette` de Core para los aspectos de siempre: Oscuro, Claro,
 * Sistema, Pastel y Medianoche, y de los aspectos retro de Windows (XP, Telecom, Bash, Fósforo), que se derivan en `AspectPalettes`.
 *
 * Los valores son `#RRGGBB` o `#AARRGGBB` (alfa PRIMERO, como `Color.parseColor` de Android). Las tablas de Oscuro y Claro están copiadas
 * de Core; Pastel y Medianoche se derivan de ellas igual que allí (se conserva la claridad OKLCH y solo se tiñe), y los vectores de
 * `tests/vectors/text-color.json` fijan el resultado completo.
 */
object AppPalette {
    /** Los números de `AppearanceMode` de Core, que son los que viajan en `settings.json`. */
    const val DARK = 0
    const val LIGHT = 1
    const val SYSTEM = 2
    const val PASTEL = 3
    const val MIDNIGHT = 4

    /** Los aspectos retro de Windows (XP, Telecomunicaciones, Bash, Fósforo): su paleta se deriva en `AspectPalettes`. */
    const val XP_LIGHT = 5
    const val XP_DARK = 6
    const val TELECOM_LIGHT = 7
    const val TELECOM_DARK = 8
    const val BASH = 9
    const val PHOSPHOR = 10

    /** Las libretas (claro y oscuro de la libreta de viaje y del cuaderno de campo): también se derivan en `AspectPalettes`. */
    const val TRAVEL_NOTEBOOK_LIGHT = 11
    const val TRAVEL_NOTEBOOK_DARK = 12
    const val FIELD_NOTEBOOK_LIGHT = 13
    const val FIELD_NOTEBOOK_DARK = 14

    val dark: Map<String, String> = linkedMapOf(
        "Ground" to "#2A261F",
        "GroundDeep" to "#25211B",
        "Surface" to "#332F28",
        "Popup" to "#2D2923",
        "Raised" to "#3C3730",
        "RaisedHover" to "#403B33",
        "Hover" to "#4E4840",
        "Selected" to "#443F36",
        "Pressed" to "#5F584E",
        "Track" to "#4A443B",
        "Divider" to "#3C3730",
        "Text" to "#EDE7DC",
        "TextStrong" to "#F5F0E6",
        "TextSoft" to "#C9C0B2",
        "TextWarm" to "#C8B9A8",
        "MutedText" to "#A79E90",
        "Hint" to "#8A8175",
        "TextFaint" to "#6B6459",
        "TextDisabled" to "#71695E",
        "BorderSubtle" to "#5A5146",
        "Border" to "#62594D",
        "FieldHover" to "#8A8175",
        "FieldFocus" to "#A18465",
        "AccentBorder" to "#B39A7E",
        "Accent" to "#A18465",
        "AccentHover" to "#B79570",
        "OnAccent" to "#201B16",
        "Selection" to "#806A55",
        "DangerBg" to "#5A2A28",
        "DangerStrong" to "#6B302C",
        "DangerStrongHover" to "#823A35",
        "OnDanger" to "#F5E6E3",
        "DangerText" to "#E8A0A0",
        "ErrorText" to "#E0A08A",
        "ErrorBorder" to "#D98C84",
        "Warning" to "#E0B070",
        "Success" to "#A9C99A",
        "Notice" to "#E0CBA8",
        "DockBar" to "#B52A241E",
        "Hairline" to "#40FFFFFF",
        "ScrollThumbBorder" to "#D0B28E",
        "ScrollThumbDragBorder" to "#E0C6A4",
        "DangerHover" to "#5A2E2E",
        "OnDangerBg" to "#F0C1BA",
        "Attention" to "#C9A46C",
        "DockPrimary" to "#575044",
        "DockArrowText" to "#E8D8C5",
        "DockArrowBorder" to "#D08F7965",
        "DockArrowHover" to "#D14A3D31",
        "DockArrowHoverBorder" to "#F0C7A5",
        "DockArrowPressed" to "#E05B4939",
        "DockRailBorder" to "#C08B7461",
        "DockThumb" to "#E1C2A88F",
        "DockThumbBorder" to "#F2DCC2",
        "TitleBar" to "#2A261F",
        "TitleBarEnd" to "#2A261F",
        "OnTitleBar" to "#F5F0E6",
        "BevelLight" to "#5F584E",
        "BevelDark" to "#15120E",
        "Channel1" to "#F2D338",
        "Channel2" to "#3FD0E0",
        "Channel3" to "#EA80DD",
        "Channel4" to "#7FA8FF",
        "PromptUser" to "#A9C99A",
        "PromptPath" to "#9FB8D0"
    )

    // Papel cálido, no blanco puro: el mismo tono tostado del oscuro llevado a claro (comentario de Core).
    val light: Map<String, String> = linkedMapOf(
        "Ground" to "#F6F1E8",
        "GroundDeep" to "#EFE9DE",
        "Surface" to "#ECE5D9",
        "Popup" to "#FBF8F2",
        "Raised" to "#E2DACB",
        "RaisedHover" to "#DCD3C3",
        "Hover" to "#D4CAB8",
        "Selected" to "#D8CEBD",
        "Pressed" to "#C8BDA9",
        "Track" to "#D0C6B4",
        "Divider" to "#E2DACB",
        "Text" to "#2A261F",
        "TextStrong" to "#1E1A14",
        "TextSoft" to "#4A443B",
        "TextWarm" to "#5C5044",
        "MutedText" to "#655E53",
        "Hint" to "#766D61",
        "TextFaint" to "#978D80",
        "TextDisabled" to "#A39A8D",
        "BorderSubtle" to "#D0C6B5",
        "Border" to "#BFB4A1",
        "FieldHover" to "#A89D8B",
        "FieldFocus" to "#8C6B4B",
        "AccentBorder" to "#9A7B5B",
        "Accent" to "#8C6B4B",
        "AccentHover" to "#7A5C3E",
        "OnAccent" to "#FFF9F0",
        "Selection" to "#D6C0A3",
        "DangerBg" to "#F0D5D0",
        "DangerStrong" to "#B5473E",
        "DangerStrongHover" to "#9E3C34",
        "OnDanger" to "#FFFFFF",
        "DangerText" to "#A8453B",
        "ErrorText" to "#A2502F",
        "ErrorBorder" to "#C0655B",
        "Warning" to "#9A6B1F",
        "Success" to "#4E7A3E",
        "Notice" to "#7A5C2E",
        "DockBar" to "#E6F0EAE0",
        "Hairline" to "#33000000",
        "ScrollThumbBorder" to "#7A5C3E",
        "ScrollThumbDragBorder" to "#6A4E33",
        "DangerHover" to "#F0D5D0",
        "OnDangerBg" to "#8A2F28",
        "Attention" to "#80591A",
        "DockPrimary" to "#E2DACB",
        "DockArrowText" to "#4A3F33",
        "DockArrowBorder" to "#D0A89880",
        "DockArrowHover" to "#E6E2D8C8",
        "DockArrowHoverBorder" to "#8C6B4B",
        "DockArrowPressed" to "#F0D2C6B2",
        "DockRailBorder" to "#C0B5A58F",
        "DockThumb" to "#E0A08C74",
        "DockThumbBorder" to "#6A5A48",
        "TitleBar" to "#F6F1E8",
        "TitleBarEnd" to "#F6F1E8",
        "OnTitleBar" to "#1E1A14",
        "BevelLight" to "#FFFFFF",
        "BevelDark" to "#978D80",
        "Channel1" to "#6E5600",
        "Channel2" to "#00636C",
        "Channel3" to "#9C1F6E",
        "Channel4" to "#1F57B5",
        "PromptUser" to "#44703A",
        "PromptPath" to "#2F5F86"
    )

    private val pastel: Map<String, String> by lazy { tint(light, neutralHue = 300.0, neutralChroma = 0.022, accentHue = 350.0) }
    private val midnight: Map<String, String> by lazy { tint(dark, neutralHue = 255.0, neutralChroma = 0.03, accentHue = 235.0) }

    /** Colores con significado propio (peligro, aviso, éxito…): no se tiñen, un «borrar» tiene que seguir pareciendo peligroso. */
    private val semantic = setOf(
        "DangerBg", "DangerStrong", "DangerStrongHover", "OnDanger", "DangerText", "DangerHover",
        "OnDangerBg", "ErrorText", "ErrorBorder", "Warning", "Success", "Notice", "Attention",
        "Channel1", "Channel2", "Channel3", "Channel4", "PromptUser", "PromptPath",
    )

    /**
     * La paleta de [mode]. Los modos retro (5-14) se derivan en [AspectPalettes] con los colores que haya elegido el usuario ([aspectColors]:
     * id de hueco → hex; lo que falta o no es válido cae en los de fábrica). Un modo desconocido cae en Claro u Oscuro según [isLight], para
     * que un valor guardado por una versión más nueva nunca deje la app sin paleta.
     */
    fun forMode(mode: Int, systemUsesLight: Boolean, aspectColors: Map<String, String>? = null): Map<String, String> = when (mode) {
        PASTEL -> pastel
        MIDNIGHT -> midnight
        in XP_LIGHT..FIELD_NOTEBOOK_DARK -> AspectPalettes.derive(mode, AspectCatalog.resolve(mode, aspectColors))
        else -> if (isLight(mode, systemUsesLight)) light else dark
    }

    fun isLight(mode: Int, systemUsesLight: Boolean): Boolean = when (mode) {
        LIGHT, PASTEL, XP_LIGHT, TELECOM_LIGHT, TRAVEL_NOTEBOOK_LIGHT, FIELD_NOTEBOOK_LIGHT -> true
        SYSTEM -> systemUsesLight
        else -> false
    }

    internal fun tint(source: Map<String, String>, neutralHue: Double, neutralChroma: Double, accentHue: Double): Map<String, String> {
        val result = linkedMapOf<String, String>()
        for ((token, value) in source) {
            // #AARRGGBB: el alfa se conserva y se tiñe el color.
            val alpha = if (value.length == 9) value.substring(1, 3) else ""
            val rgb = "#" + value.substring(value.length - 6)
            val color = OklchColor.fromHex(rgb)
            if (token in semantic || color == null) {
                result[token] = value
                continue
            }
            // Casi gris (fondos, textos, bordes): toma el matiz de la paleta con poco croma. Con color de verdad (acentos): cambia de
            // matiz y conserva su croma.
            val tinted = if (color.c < 0.045) color.copy(c = neutralChroma, h = neutralHue) else color.copy(h = accentHue)
            val hex = tinted.toHex()
            result[token] = if (alpha.isNotEmpty()) "#" + alpha + hex.substring(1) else hex
        }
        return result
    }
}
