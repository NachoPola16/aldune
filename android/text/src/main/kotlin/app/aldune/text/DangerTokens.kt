package app.aldune.text

/**
 * Port of `DangerTokens` in Aldune.Core: the one red of destructive actions and errors. Same hue in every look; lightness and chroma
 * are fitted in OKLCH to the palette's own backgrounds and accent (text 4.5:1, borders and icons 3:1). The vectors of `text-color.json`
 * pin the result on both sides.
 */
internal object DangerTokens {
    const val HUE = 27.0

    private val textBackgrounds = listOf("Ground", "Surface", "Raised", "Popup", "Hover")
    private val borderBackgrounds = listOf("Ground", "Surface", "Raised", "Popup")

    fun apply(source: Map<String, String>): Map<String, String> {
        val ground = OklchColor.fromHex(source.getValue("Ground")) ?: return source
        val p = LinkedHashMap(source)
        val light = ground.l > 0.6
        val accentChroma = OklchColor.fromHex(p.getValue("Accent"))?.c ?: 0.1
        // A more saturated look gets a more saturated red; a muted one a calmer red.
        val chroma = (0.07 + 0.4 * accentChroma).coerceIn(0.10, 0.15)

        fun red(l: Double, c: Double) = OklchColor(l, c, HUE).toHex()

        var bg = red((ground.l + if (light) -0.07 else 0.08).coerceIn(0.05, 0.97), if (light) 0.04 else 0.06)
        bg = ColorFit.background(bg, p.getValue("Text"), 4.5)
        bg = ColorFit.background(bg, p.getValue("TextStrong"), 4.5)
        p["DangerBg"] = bg
        p["DangerHover"] = bg

        var text = red(if (light) 0.48 else 0.76, chroma)
        for (key in textBackgrounds) text = ColorFit.foreground(text, p.getValue(key), 4.5)
        text = ColorFit.foreground(text, bg, 4.5)
        p["DangerText"] = text
        p["ErrorText"] = text
        p["OnDangerBg"] = ColorFit.foreground(red(if (light) 0.36 else 0.88, 0.06), bg, 4.5)

        var border = red(if (light) 0.58 else 0.68, chroma)
        for (key in borderBackgrounds) border = ColorFit.foreground(border, p.getValue(key), 3.0)
        p["ErrorBorder"] = border

        val onDanger = if (light) "#FFFFFF" else red(0.95, 0.015)
        val strong = ColorFit.background(red(if (light) 0.52 else 0.40, if (light) chroma + 0.03 else 0.10), onDanger, 4.5)
        val hover = ColorFit.background(if (light) ColorFit.darker(strong, 0.04) else ColorFit.lighter(strong, 0.05), onDanger, 4.5)
        p["DangerStrong"] = strong
        p["DangerStrongHover"] = hover
        p["OnDanger"] = onDanger
        return p
    }
}
