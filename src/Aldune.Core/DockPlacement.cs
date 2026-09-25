namespace Aldune.Core;

public enum DockPlacementFix { None, MoveBack, Rebuild }

/// <summary>
/// ¿Sigue la ventana del dock donde el dock cree que está? El dock solo se mueve a sí mismo al
/// calcular su rectángulo (<see cref="EdgeGeometry.WindowRect"/>), pero Windows desplaza ventanas por
/// su cuenta al cambiar la disposición de las pantallas (apagar o encender una, cambiar la principal).
/// Con la ventana en otro sitio, la tira no se ve donde debería, el ratón se compara contra la zona
/// calculada y el abanico se abre donde esté de verdad la ventana (visto por el usuario: "en medio de
/// la pantalla", tapado por otras ventanas).
///
/// Todo en las mismas unidades (píxeles físicos para los rectángulos de ventana).
/// </summary>
public static class DockPlacement
{
    /// <summary>Más que un redondeo de DPI: por debajo de esto no se considera que se haya movido.</summary>
    public const double Tolerance = 2;

    /// <param name="dockArea">El área de trabajo con la que se construyó el dock.</param>
    /// <param name="currentArea">El área de trabajo de esa misma pantalla ahora, o null si ya no está.</param>
    public static DockPlacementFix Check(Rect actual, Rect expected, WorkingArea dockArea, WorkingArea? currentArea)
    {
        if (Near(actual.X, expected.X) && Near(actual.Y, expected.Y)
            && Near(actual.Width, expected.Width) && Near(actual.Height, expected.Height))
            return DockPlacementFix.None;

        // Si la pantalla ha cambiado, devolver la ventana al rectángulo viejo la dejaría igual de mal:
        // hay que recalcular todo, que es lo que hace reconstruir los docks.
        if (currentArea is not { } area
            || !Near(area.X, dockArea.X) || !Near(area.Y, dockArea.Y)
            || !Near(area.Width, dockArea.Width) || !Near(area.Height, dockArea.Height))
            return DockPlacementFix.Rebuild;

        return DockPlacementFix.MoveBack;
    }

    private static bool Near(double a, double b) => Math.Abs(a - b) <= Tolerance;
}
