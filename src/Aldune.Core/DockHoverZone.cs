namespace Aldune.Core;

/// <summary>
/// Zonas del ratón del dock que no dependen de WPF. La superficie desplegada la mide la ventana
/// (pestañas visibles, botonera); aquí se le añade la holgura y se resuelven los cantos.
/// </summary>
public static class DockHoverZone
{
    /// <summary>
    /// Holgura alrededor de las pestañas y la botonera con el abanico abierto. Antes era 0 en los
    /// laterales (1 px fuera de la pestaña ya empezaba el cierre) y toda la ventana arriba/abajo (con
    /// 40 notas, ~520 px de vacío transparente mantenían el dock abierto): el mismo gesto se comportaba
    /// distinto según el borde y el número de notas. Ahora es la misma en los cuatro.
    /// </summary>
    public const double ExpandedSlop = 24;

    /// <summary>
    /// Filas del canto que el dock deja libres cuando una barra de tareas que se oculta sola comparte
    /// su borde: es la franja que la hace aparecer, y la botonera del dock desplegado la tapaba.
    /// </summary>
    public const double AutoHideTaskbarClearance = 3;

    /// <summary>Si el punto cae en alguna superficie ensanchada <paramref name="slop"/> por cada lado.</summary>
    public static bool Contains(IEnumerable<Rect> surfaces, double x, double y, double slop)
    {
        foreach (var r in surfaces)
        {
            if (r.Width <= 0 || r.Height <= 0) continue;
            if (x >= r.X - slop && x <= r.X + r.Width + slop
                && y >= r.Y - slop && y <= r.Y + r.Height + slop)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Si el cursor está en el último píxel del canto: ahí el ratón se detiene contra el borde, que es
    /// una intención mucho más clara que pasar por la tira.
    /// </summary>
    public static bool IsAgainstEdge(WorkingArea area, EdgePosition edge, double x, double y) => edge switch
    {
        EdgePosition.Right => x >= area.X + area.Width - 1,
        EdgePosition.Left => x <= area.X + 1,
        EdgePosition.Top => y <= area.Y + 1,
        EdgePosition.Bottom => y >= area.Y + area.Height - 1,
        _ => false
    };

    /// <summary>
    /// Un punto justo al otro lado del centro del canto. Si ahí hay otra pantalla (o una barra de
    /// tareas fija, que deja el área de trabajo antes del borde físico), el ratón no se detiene en
    /// el canto del dock.
    /// </summary>
    public static (double X, double Y) PointBeyondEdge(WorkingArea area, EdgePosition edge) => edge switch
    {
        EdgePosition.Right => (area.X + area.Width + 1, area.Y + area.Height / 2),
        EdgePosition.Left => (area.X - 1, area.Y + area.Height / 2),
        EdgePosition.Top => (area.X + area.Width / 2, area.Y - 1),
        EdgePosition.Bottom => (area.X + area.Width / 2, area.Y + area.Height + 1),
        _ => throw new ArgumentOutOfRangeException(nameof(edge))
    };

    /// <summary>El área de trabajo con <paramref name="inset"/> DIP menos por el lado del canto.</summary>
    public static WorkingArea Inset(WorkingArea area, EdgePosition edge, double inset) => edge switch
    {
        EdgePosition.Right => area with { Width = area.Width - inset },
        EdgePosition.Left => area with { X = area.X + inset, Width = area.Width - inset },
        EdgePosition.Top => area with { Y = area.Y + inset, Height = area.Height - inset },
        EdgePosition.Bottom => area with { Height = area.Height - inset },
        _ => throw new ArgumentOutOfRangeException(nameof(edge))
    };
}
