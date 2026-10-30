package app.aldune.ui

import androidx.compose.material3.Typography
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.FontFamily
import app.aldune.text.AppPalette
import app.aldune.text.AspectSkin

/** Bash, Telecomunicaciones (claro y oscuro) y Fósforo usan fuente monoespaciada, como Cascadia Mono en Windows. XP y los demás, la de siempre. */
internal fun isMonospaceAspect(appearance: Int): Boolean =
    appearance == AppPalette.TELECOM_LIGHT || appearance == AppPalette.TELECOM_DARK ||
        appearance == AppPalette.BASH || appearance == AppPalette.PHOSPHOR

/** La tipografía de Material con la familia monoespaciada del sistema en los aspectos que la piden. */
internal fun typographyFor(appearance: Int): Typography {
    val base = Typography()
    if (AspectSkin.serif(appearance)) {
        // Las libretas: serifa en el texto y los títulos; las etiquetas pequeñas de los botones se quedan como siempre (se leen mejor sin remates).
        fun TextStyle.serif() = copy(fontFamily = FontFamily.Serif)
        return base.copy(
            headlineLarge = base.headlineLarge.serif(), headlineMedium = base.headlineMedium.serif(), headlineSmall = base.headlineSmall.serif(),
            titleLarge = base.titleLarge.serif(), titleMedium = base.titleMedium.serif(), titleSmall = base.titleSmall.serif(),
            bodyLarge = base.bodyLarge.serif(), bodyMedium = base.bodyMedium.serif(), bodySmall = base.bodySmall.serif(),
        )
    }
    if (!isMonospaceAspect(appearance)) return base
    fun TextStyle.mono() = copy(fontFamily = FontFamily.Monospace)
    return base.copy(
        displayLarge = base.displayLarge.mono(), displayMedium = base.displayMedium.mono(), displaySmall = base.displaySmall.mono(),
        headlineLarge = base.headlineLarge.mono(), headlineMedium = base.headlineMedium.mono(), headlineSmall = base.headlineSmall.mono(),
        titleLarge = base.titleLarge.mono(), titleMedium = base.titleMedium.mono(), titleSmall = base.titleSmall.mono(),
        bodyLarge = base.bodyLarge.mono(), bodyMedium = base.bodyMedium.mono(), bodySmall = base.bodySmall.mono(),
        labelLarge = base.labelLarge.mono(), labelMedium = base.labelMedium.mono(), labelSmall = base.labelSmall.mono(),
    )
}
