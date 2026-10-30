package app.aldune.ui

import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.drawBehind
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp
import app.aldune.text.AspectSkin

/**
 * El papel de las libretas, dibujado bajo el texto: pliegues y grano en la libreta de viaje; renglones y margen rojo en el cuaderno de campo. Todo con muy poca opacidad (se nota al mirar, nunca compite con las letras) y con unas pocas líneas y puntos: sin imágenes ni
 * ruido por píxel, así que no cuesta batería ni fotogramas al desplazar la lista. [ink] es el color del texto del aspecto y [light] dice si el
 * papel es claro (las líneas del cuaderno van más apagadas en oscuro).
 */
internal fun Modifier.paperTexture(
    paper: AspectSkin.Paper, ink: Color, light: Boolean,
    // Los renglones: cada cuánto, dónde cae el primero y dónde va el margen. En el editor se ajustan al interlineado del texto para que cada línea
    // se apoye en su renglón; en las filas de la lista valen los de por defecto.
    ruleStep: Dp = 26.dp, ruleOffset: Dp = 26.dp, marginX: Dp = 28.dp,
): Modifier = when (paper) {
    AspectSkin.Paper.NONE -> this
    AspectSkin.Paper.FOLDS -> drawBehind {
        val line = ink.copy(alpha = if (light) 0.07f else 0.10f)
        val edge = Color.White.copy(alpha = if (light) 0.35f else 0.06f)
        val w = 1.dp.toPx()
        // Dos dobleces verticales a los tercios, uno horizontal casi por la mitad y una arruga en diagonal: un papel doblado en cuatro y estirado.
        for (fraction in listOf(0.33f, 0.66f)) {
            val x = size.width * fraction
            drawLine(line, Offset(x, 0f), Offset(x + size.width * 0.015f, size.height), w)
            drawLine(edge, Offset(x + 1.5.dp.toPx(), 0f), Offset(x + 1.5.dp.toPx() + size.width * 0.015f, size.height), w)
        }
        drawLine(line, Offset(0f, size.height * 0.48f), Offset(size.width, size.height * 0.5f), w)
        drawLine(line.copy(alpha = line.alpha * 0.7f), Offset(size.width * 0.1f, size.height * 0.9f), Offset(size.width * 0.55f, size.height * 0.62f), w)
        // El grano: unos puntos en posiciones fijas (siempre los mismos, no parpadean al repintar).
        var seed = 12345
        repeat(50) {
            seed = seed * 1103515245 + 12345
            val x = ((seed ushr 8) and 0xFFFF) / 65535f * size.width
            seed = seed * 1103515245 + 12345
            val y = ((seed ushr 8) and 0xFFFF) / 65535f * size.height
            drawCircle(ink.copy(alpha = if (light) 0.05f else 0.07f), radius = 0.7.dp.toPx(), center = Offset(x, y))
        }
    }
    AspectSkin.Paper.RULED -> drawBehind {
        val rule = if (light) Color(0xFF466E96).copy(alpha = 0.16f) else Color(0xFFA0BEDC).copy(alpha = 0.10f)
        val margin = Color(0xFFBE3C32).copy(alpha = if (light) 0.40f else 0.30f)
        val step = ruleStep.toPx()
        var y = ruleOffset.toPx()
        while (y < size.height) {
            drawLine(rule, Offset(0f, y), Offset(size.width, y), 1.dp.toPx())
            y += step
        }
        drawLine(margin, Offset(marginX.toPx(), 0f), Offset(marginX.toPx(), size.height), 1.5.dp.toPx())
    }
    // Cuaderno de cuadros: los mismos renglones y, además, verticales cada `ruleStep` a partir del margen (cuadros del alto de línea).
    AspectSkin.Paper.GRID -> drawBehind {
        val rule = if (light) Color(0xFF4A78B0).copy(alpha = 0.15f) else Color(0xFF9CBCE0).copy(alpha = 0.10f)
        val margin = Color(0xFFBE3C32).copy(alpha = if (light) 0.40f else 0.30f)
        val step = ruleStep.toPx()
        var y = ruleOffset.toPx()
        while (y < size.height) {
            drawLine(rule, Offset(0f, y), Offset(size.width, y), 1.dp.toPx())
            y += step
        }
        var x = marginX.toPx() + step
        while (x < size.width) {
            drawLine(rule, Offset(x, 0f), Offset(x, size.height), 1.dp.toPx())
            x += step
        }
        drawLine(margin, Offset(marginX.toPx(), 0f), Offset(marginX.toPx(), size.height), 1.5.dp.toPx())
    }
}

/**
 * El papel de una fila de la lista: más contenido que el del editor, así que aquí **nada cruza el texto**. En la libreta de viaje, solo el grano
 * (unos puntos casi invisibles, sin pliegues que pasen por las letras); en el cuaderno de campo, solo el margen rojo, dentro del hueco entre la franja
 * de color y el texto (sin renglones). El editor sí lleva los pliegues y los renglones completos: ahí el texto se apoya en ellos.
 */
internal fun Modifier.paperTextureRow(paper: AspectSkin.Paper, ink: Color, light: Boolean): Modifier = when (paper) {
    AspectSkin.Paper.NONE -> this
    AspectSkin.Paper.FOLDS -> drawBehind {
        var seed = 12345
        repeat(30) {
            seed = seed * 1103515245 + 12345
            val x = ((seed ushr 8) and 0xFFFF) / 65535f * size.width
            seed = seed * 1103515245 + 12345
            val y = ((seed ushr 8) and 0xFFFF) / 65535f * size.height
            drawCircle(ink.copy(alpha = if (light) 0.05f else 0.07f), radius = 0.7.dp.toPx(), center = Offset(x, y))
        }
    }
    AspectSkin.Paper.RULED, AspectSkin.Paper.GRID -> drawBehind {
        // La franja de color mide 8 dp y el texto empieza en 22 dp: el margen va en medio.
        drawLine(Color(0xFFBE3C32).copy(alpha = if (light) 0.40f else 0.30f), Offset(15.dp.toPx(), 0f), Offset(15.dp.toPx(), size.height), 1.5.dp.toPx())
    }
}
