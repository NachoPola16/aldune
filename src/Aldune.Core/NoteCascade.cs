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

    /// <summary>Desplazamiento diagonal entre una nota y la siguiente.</summary>
    public const double Step = 32;

    /// <summary>A partir de este nivel las notas dejan de escalonarse: no cabe una escalera eterna.</summary>
    public const int MaxLevels = 5;

    /// <summary>
    /// Esquina superior izquierda de la nota del nivel <paramref name="level"/> (0 = la primera pestaña,
    /// la más cercana al dock). <paramref name="noteCount"/> es el tamaño del mazo: en arriba/abajo el
    /// abanico crece con él y por tanto también el hueco que hay que respetar.
    /// </summary>
    public static (double Left, double Top) Position(
        WorkingArea area, EdgePosition edge, int noteCount,
        double noteWidth, double noteHeight, int level)
    {
        level = Math.Clamp(level, 0, MaxLevels);
        var dock = EdgeGeometry.WindowRect(area, edge, noteCount);
        double offset = level * Step;
        double centeredTop = area.Y + (area.Height - noteHeight) / 2;

        return edge switch
        {
            EdgePosition.Left => (dock.X + dock.Width + DockGap + offset, centeredTop + offset),
            EdgePosition.Right => (dock.X - DockGap - noteWidth - offset, centeredTop + offset),
            EdgePosition.Top => (dock.X + offset, dock.Y + dock.Height + DockGap + offset),
            EdgePosition.Bottom => (dock.X + offset, dock.Y - DockGap - noteHeight - offset),
            _ => throw new ArgumentOutOfRangeException(nameof(edge))
        };
    }
}
