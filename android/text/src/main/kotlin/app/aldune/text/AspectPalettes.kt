package app.aldune.text

/**
 * Paletas de los aspectos retro (port de `AspectPalettes` de Core). Se derivan de Oscuro o Claro como Pastel y Medianoche (se conserva la
 * claridad de cada clave y se cambia el matiz, `AppPalette.tint`), así que heredan su contraste ya comprobado; encima van los colores del
 * usuario, ajustados con [ColorFit] cuando no dejarían leer el texto.
 */
object AspectPalettes {
    // Fondos que se mueven juntos cuando el usuario elige el fondo (panel de osciloscopio, terminal).
    private val backgrounds = listOf(
        "Ground", "GroundDeep", "Surface", "Popup", "Raised", "RaisedHover", "Hover", "Selected", "Pressed",
        "Track", "Divider", "TitleBar", "TitleBarEnd", "DockPrimary",
    )

    // El texto principal se lee sobre estos fondos. Se ajusta uno tras otro: cada ajuste solo aleja el color del fondo.
    private val textBackgrounds = listOf("Ground", "Surface", "Raised", "Popup", "Hover", "DangerBg")

    private const val WHITE = "#FFFFFF"

    fun derive(mode: Int, c: Map<String, String>): Map<String, String> = when (mode) {
        AppPalette.XP_LIGHT -> xpLight(c.getValue("titleBar"), c.getValue("accent"))
        AppPalette.XP_DARK -> xpDark(c.getValue("titleBar"), c.getValue("accent"))
        AppPalette.TELECOM_LIGHT -> telecomLight(c.getValue("ink"), c.getValue("accent"))
        AppPalette.TELECOM_DARK -> telecomDark(c.getValue("panel"), c.getValue("trace"))
        AppPalette.BASH -> bash(c.getValue("background"), c.getValue("user"), c.getValue("path"), c.getValue("accent"))
        AppPalette.PHOSPHOR -> phosphor(c.getValue("phosphor"), c.getValue("details"))
        else -> throw IllegalArgumentException("Sin aspecto retro: $mode")
    }

    private fun xpLight(bar: String, accent: String): Map<String, String> {
        // El beige de XP (#ECE9D8): el claro de siempre teñido hacia el amarillo grisáceo.
        val p = LinkedHashMap(AppPalette.tint(AppPalette.light, neutralHue = 95.0, neutralChroma = 0.018, accentHue = hue(accent)))
        setAccent(p, accent)
        // Degradado vertical de Luna: arriba más claro, abajo el color elegido; texto blanco sobre los dos.
        p["TitleBar"] = ColorFit.background(ColorFit.lighter(bar, 0.08), WHITE, 4.5)
        p["TitleBarEnd"] = ColorFit.background(bar, WHITE, 4.5)
        p["OnTitleBar"] = WHITE
        p["BevelLight"] = WHITE
        p["BevelDark"] = "#ACA899"
        return p
    }

    private fun xpDark(bar: String, accent: String): Map<String, String> {
        // Grafito neutro, sin el marrón/naranja de Royale Noir (decidido con el usuario en Windows).
        val p = LinkedHashMap(AppPalette.tint(AppPalette.dark, neutralHue = 255.0, neutralChroma = 0.012, accentHue = hue(accent)))
        setAccent(p, accent)
        // Degradado horizontal de Windows 98: oscuro a la izquierda, el color elegido más claro a la derecha.
        p["TitleBar"] = ColorFit.background(ColorFit.darker(bar, 0.12), WHITE, 4.5)
        p["TitleBarEnd"] = ColorFit.background(ColorFit.lighter(bar, 0.08), WHITE, 4.5)
        p["OnTitleBar"] = WHITE
        p["BevelLight"] = "#6A6E76"
        p["BevelDark"] = "#121316"
        return p
    }

    private fun telecomLight(ink: String, accent: String): Map<String, String> {
        // Papel de laboratorio: el claro de siempre con un gris verdoso apenas teñido; la tinta elegida es el texto.
        val p = LinkedHashMap(AppPalette.tint(AppPalette.light, neutralHue = neutralHue(ink, 100.0), neutralChroma = 0.012, accentHue = hue(accent)))
        val text = fitText(ink, p)
        p["Text"] = text
        p["TextStrong"] = text
        p["OnTitleBar"] = text
        setAccent(p, accent)
        return p
    }

    private fun telecomDark(panel: String, trace: String): Map<String, String> {
        val p = LinkedHashMap(
            AppPalette.tint(AppPalette.dark, neutralHue = neutralHue(panel, 240.0), neutralChroma = neutralChroma(panel, 0.02), accentHue = hue(trace)),
        )
        moveBackgrounds(p, panel)
        // La traza es CH1 y el acento: un amarillo de osciloscopio que se lee sobre el panel.
        p["Channel1"] = ColorFit.foreground(trace, p.getValue("Raised"), 4.5)
        setAccent(p, trace)
        return p
    }

    private fun bash(background: String, user: String, path: String, accent: String): Map<String, String> {
        val p = LinkedHashMap(
            AppPalette.tint(AppPalette.dark, neutralHue = neutralHue(background, 60.0), neutralChroma = neutralChroma(background, 0.03), accentHue = hue(accent)),
        )
        moveBackgrounds(p, background)
        p["PromptUser"] = ColorFit.foreground(user, p.getValue("Ground"), 4.5)
        p["PromptPath"] = ColorFit.foreground(path, p.getValue("Ground"), 4.5)
        // Como en la maqueta: botón del color del acento con el texto del color de la terminal.
        p["OnAccent"] = p.getValue("Ground")
        setAccent(p, accent)
        return p
    }

    private fun phosphor(phosphor: String, details: String): Map<String, String> {
        val p = LinkedHashMap(
            AppPalette.tint(AppPalette.dark, neutralHue = neutralHue(phosphor, 150.0), neutralChroma = neutralChroma(phosphor, 0.025), accentHue = hue(details)),
        )
        // Fondos casi negros del mismo matiz que el fósforo, como el cristal de un monitor apagado.
        moveBackgrounds(p, ColorMix.toward(phosphor, "#000000", 0.9))
        val text = fitText(phosphor, p)
        p["Text"] = text
        p["TextStrong"] = text
        p["OnTitleBar"] = text
        val soft = ColorFit.foreground(ColorMix.toward(text, p.getValue("Ground"), 0.15), p.getValue("Raised"), 4.5)
        p["TextSoft"] = soft
        p["TextWarm"] = soft
        p["OnAccent"] = p.getValue("Ground")
        setAccent(p, details)
        return p
    }

    // El acento y sus acompañantes, con el texto del botón (OnAccent) legible encima. También el botón «+» del dock, cuyo glifo es TextStrong:
    // se llama después de fijar TextStrong.
    private fun setAccent(p: MutableMap<String, String>, accent: String) {
        val onAccent = p.getValue("OnAccent")
        val fitted = ColorFit.background(accent, onAccent, 4.5)
        val darkText = ColorFit.ratio(onAccent, "#000000") < ColorFit.ratio(onAccent, WHITE)
        p["DockPrimary"] = ColorFit.background(accent, p.getValue("TextStrong"), 4.5)
        p["Accent"] = fitted
        p["AccentBorder"] = fitted
        p["FieldFocus"] = fitted
        // Al pasar el dedo, un paso más lejos del texto: más claro bajo texto oscuro, más oscuro bajo claro.
        p["AccentHover"] = ColorFit.background(if (darkText) ColorFit.lighter(fitted, 0.05) else ColorFit.darker(fitted, 0.05), onAccent, 4.5)
    }

    /**
     * Pone el fondo elegido como Ground y mueve todos los fondos lo mismo en claridad, para que sigan escalonados. La claridad se limita a
     * [0.14, 0.30]: más claro, los textos del oscuro dejarían de leerse; más oscuro, los escalones se pierden contra el negro.
     */
    private fun moveBackgrounds(p: MutableMap<String, String>, ground: String) {
        val chosen = OklchColor.fromHex(ground) ?: return
        val current = OklchColor.fromHex(p.getValue("Ground")) ?: return
        val target = chosen.l.coerceIn(0.14, 0.30)
        val delta = target - current.l
        for (key in backgrounds) {
            val value = p[key] ?: continue
            if (value.length != 7) continue
            val color = OklchColor.fromHex(value) ?: continue
            p[key] = color.copy(l = (color.l + delta).coerceIn(0.0, 1.0)).toHex()
        }
        p["Ground"] = chosen.copy(l = target).toHex()
        p["TitleBar"] = p.getValue("Ground")
        p["TitleBarEnd"] = p.getValue("Ground")
        p["MutedText"] = ColorFit.foreground(ColorFit.foreground(p.getValue("MutedText"), p.getValue("Ground"), 4.5), p.getValue("Surface"), 4.5)
        p["Hint"] = ColorFit.foreground(p.getValue("Hint"), p.getValue("Ground"), 3.5)
    }

    private fun fitText(text: String, p: Map<String, String>): String {
        var result = text
        for (key in textBackgrounds) result = ColorFit.foreground(result, p.getValue(key), 4.5)
        return result
    }

    private fun hue(color: String): Double = OklchColor.fromHex(color)?.h ?: 0.0

    // Un color sin matiz (gris, blanco) no puede teñir la paleta de un matiz inventado: su «matiz» es ruido.
    private fun neutralChroma(color: String, maximum: Double): Double =
        OklchColor.fromHex(color)?.takeIf { it.c >= 0.02 }?.let { minOf(it.c, maximum) } ?: 0.0

    private fun neutralHue(color: String, fallback: Double): Double =
        OklchColor.fromHex(color)?.takeIf { it.c >= 0.02 }?.h ?: fallback
}
