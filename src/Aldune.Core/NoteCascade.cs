namespace Aldune.Core;

/// <summary>
/// Posición de cada nota en la disposición "Cascada junto al dock", para los cuatro cantos.
///
/// La cascada arranca pegada al dock DESPLEGADO, no al canto de la pantalla: el hueco que ocupa el
/// abanico (<see cref="EdgeGeometry.WindowRect"/>) queda libre, así que se puede volver a desplegar el
/// dock sin que las notas lo tapen. En los laterales el abanico es una columna pegada al canto; arriba y
/// abajo cuelga desde el canto, centrado, y la cascada arranca en su misma columna.
/// </summary>
public static class NoteCascade
{
    /// <summary>Aire entre el dock desplegado (sombra incluida) y la primera nota.</summary>
    public const double DockGap = 12;

    /// <summary>Desplazamiento diagonal entre una nota y la siguiente, con sitio de sobra.</summary>
    public const double Step = 32;

    /// <summary>
    /// Desplazamiento mínimo entre una nota y la siguiente: lo que hace falta para que asome al menos
    /// la cabecera de la de abajo. Por debajo de esto una nota quedaría completamente tapada por la
    /// siguiente, indistinguible de que no estuviera abierta.
    /// </summary>
    public const double MinStep = 18;

    /// <summary>
    /// Esquina superior izquierda de la nota del nivel <paramref name="level"/> (0 = la primera pestaña,
    /// la más cercana al dock). <paramref name="noteCount"/> es el tamaño del mazo: en arriba/abajo el
    /// abanico crece con él y por tanto también el hueco que hay que respetar.
    ///
    /// El paso entre notas se comprime cuando el mazo es grande, igual que <see cref="EdgeGeometry.PitchFor"/>
    /// comprime el paso entre pestañas: sin esto, a partir de la sexta nota el desplazamiento se clampaba a
    /// un tope fijo y todas las siguientes caían exactamente en las mismas coordenadas, tapándose del todo
    /// entre sí. Comprimiendo el paso según el hueco disponible en pantalla, todas las notas del mazo quedan
    /// con al menos <see cref="MinStep"/> de solape entre sí — se ve algo de cada una — y, cuando cabe sin
    /// comprimir, el paso es el natural (<see cref="Step"/>).
    /// </summary>
    public static (double Left, double Top) Position(
        WorkingArea area, EdgePosition edge, int noteCount,
        double noteWidth, double noteHeight, int level, DockAlignment alignment = DockAlignment.Center)
    {
        // La alineación solo mueve el dock en su borde: arriba y abajo las notas siguen su X, y en los
        // laterales se abren centradas en vertical igual que antes (con el dock arriba o abajo del borde,
        // abrir ahí empujaría la cascada, que baja, fuera de la pantalla).
        var dock = EdgeGeometry.WindowRect(area, edge, noteCount, alignment);
        double centeredTop = area.Y + (area.Height - noteHeight) / 2;
        double step = StepFor(area, edge, noteCount, dock, noteWidth, noteHeight);
        double offset = level * step;

        // Arriba y abajo con un mazo grande el abanico desplegado ocupa casi todo el alto: la cascada "debajo" (o "encima") del dock no cabría
        // en pantalla y, al recortarla, las notas acababan encima de las pestañas. Entonces se abre al lado de la columna de pestañas.
        if (edge is EdgePosition.Top or EdgePosition.Bottom && SideOfColumn(area, edge, noteCount, dock, noteWidth, noteHeight, level) is { } side)
            return side;

        return edge switch
        {
            EdgePosition.Left => (dock.X + dock.Width + DockGap + offset, centeredTop + offset),
            EdgePosition.Right => (dock.X - DockGap - noteWidth - offset, centeredTop + offset),
            EdgePosition.Top => (dock.X + offset, dock.Y + dock.Height + DockGap + offset),
            EdgePosition.Bottom => (dock.X + offset, dock.Y - DockGap - noteHeight - offset),
            _ => throw new ArgumentOutOfRangeException(nameof(edge))
        };
    }

    /// <summary>
    /// La posición al lado de la columna de pestañas del dock de arriba/abajo, o <c>null</c> si la cascada cabe debajo (encima) del dock como
    /// siempre. Elige el lado derecho si hay sitio y, si no, el izquierdo; el paso se acota al hueco de pantalla como en <see cref="StepFor"/>.
    /// </summary>
    private static (double Left, double Top)? SideOfColumn(
        WorkingArea area, EdgePosition edge, int noteCount, Rect dock, double noteWidth, double noteHeight, int level)
    {
        double baseTop = edge == EdgePosition.Top
            ? dock.Y + dock.Height + DockGap
            : dock.Y - DockGap - noteHeight;
        bool fitsBelow = edge == EdgePosition.Top
            ? baseTop + noteHeight <= area.Y + area.Height
            : baseTop >= area.Y;
        if (fitsBelow) return null;

        double columnLeft = dock.X + (dock.Width - EdgeGeometry.TabWidth) / 2;
        double columnRight = columnLeft + EdgeGeometry.TabWidth;
        bool onRight = columnRight + DockGap + noteWidth <= area.X + area.Width;
        double baseLeft = onRight ? columnRight + DockGap : columnLeft - DockGap - noteWidth;
        double top0 = edge == EdgePosition.Top ? area.Y + EdgeGeometry.EdgeMargin + DockGap : area.Y + area.Height - DockGap - noteHeight;

        double roomX = onRight ? area.X + area.Width - noteWidth - baseLeft : baseLeft - area.X;
        double roomY = edge == EdgePosition.Top ? area.Y + area.Height - noteHeight - top0 : top0 - area.Y;
        double step = noteCount <= 1 ? Step : Math.Clamp(Math.Max(Math.Min(roomX, roomY), 0) / (noteCount - 1), MinStep, Step);
        double offset = level * step;

        double left = onRight ? baseLeft + offset : baseLeft - offset;
        double top = edge == EdgePosition.Top ? top0 + offset : top0 - offset;
        return (Math.Clamp(left, area.X, Math.Max(area.X, area.X + area.Width - noteWidth)), Math.Clamp(top, area.Y, Math.Max(area.Y, area.Y + area.Height - noteHeight)));
    }

    /// <summary>
    /// Paso real entre notas, acotado al hueco que queda en pantalla para que la última nota del mazo
    /// no se salga del monitor. Si el mazo es pequeño y cabe de sobra, es <see cref="Step"/>; si no,
    /// se encoge hasta <see cref="MinStep"/> como mínimo — nunca menos, aunque eso empuje a las últimas
    /// notas del mazo parcialmente fuera del área de trabajo (mejor eso que solapadas del todo).
    /// </summary>
    private static double StepFor(
        WorkingArea area, EdgePosition edge, int noteCount, Rect dock, double noteWidth, double noteHeight)
    {
        if (noteCount <= 1) return Step;

        double headroom = edge switch
        {
            EdgePosition.Left => Math.Min(
                area.X + area.Width - noteWidth - (dock.X + dock.Width + DockGap),
                (area.Height - noteHeight) / 2),
            EdgePosition.Right => Math.Min(
                dock.X - DockGap - noteWidth - area.X,
                (area.Height - noteHeight) / 2),
            EdgePosition.Top => Math.Min(
                area.X + area.Width - noteWidth - dock.X,
                area.Y + area.Height - noteHeight - (dock.Y + dock.Height + DockGap)),
            EdgePosition.Bottom => Math.Min(
                area.X + area.Width - noteWidth - dock.X,
                dock.Y - DockGap - noteHeight - area.Y),
            _ => throw new ArgumentOutOfRangeException(nameof(edge))
        };

        double compressed = Math.Max(headroom, 0) / (noteCount - 1);
        return Math.Clamp(compressed, MinStep, Step);
    }
}
