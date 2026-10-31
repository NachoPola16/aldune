package app.aldune.ui

import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.PathFillType
import androidx.compose.ui.graphics.SolidColor
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.graphics.vector.path
import androidx.compose.ui.unit.dp

/** El icono pequeño de etiqueta que sustituye al «#» cuando el ajuste lo oculta (no hay uno en material-icons-core). Hereda el color del texto. */
internal val TagIcon: ImageVector = ImageVector.Builder("Tag", 24.dp, 24.dp, 24f, 24f).apply {
    path(fill = SolidColor(Color.Black), pathFillType = PathFillType.EvenOdd) {
        // Contorno de etiqueta (esquina cortada a la derecha) con un agujero a la izquierda.
        moveTo(3f, 5f); lineTo(14.5f, 5f); lineTo(21f, 12f); lineTo(14.5f, 19f); lineTo(3f, 19f); close()
        moveTo(7.5f, 10.5f); curveTo(6.7f, 10.5f, 6f, 11.2f, 6f, 12f); curveTo(6f, 12.8f, 6.7f, 13.5f, 7.5f, 13.5f)
        curveTo(8.3f, 13.5f, 9f, 12.8f, 9f, 12f); curveTo(9f, 11.2f, 8.3f, 10.5f, 7.5f, 10.5f); close()
    }
}.build()
