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
        double noteWidth, double noteHeight, int level)
    {
        var dock = EdgeGeometry.WindowRect(area, edge, noteCount);
        double centeredTop = area.Y + (area.Height - noteHeight) / 2;
        double step = StepFor(area, edge, noteCount, dock, noteWidth, noteHeight);
        double offset = level * step;

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
