namespace Aldune.Core;

/// <summary>
/// En qué pantallas va el dock. La elegida en Ajustes se recuerda por su identificador de hardware
/// (<see cref="MonitorInfo.StableId"/>) y no por su posición en la lista: Windows no garantiza ese
/// orden, y tras apagar y encender una pantalla podía cambiar, así que "la pantalla 1" pasaba a ser
/// la otra y el dock se quedaba allí aunque la elegida hubiera vuelto.
/// </summary>
public static class DockMonitorSelection
{
    public static IReadOnlyList<MonitorInfo> Select(
        IReadOnlyList<MonitorInfo> monitors, string? targetMonitorId, int? legacyIndex)
    {
        if (monitors.Count == 0) return Array.Empty<MonitorInfo>();

        if (!string.IsNullOrEmpty(targetMonitorId))
        {
            foreach (var monitor in monitors)
            {
                if (monitor.StableId == targetMonitorId) return [monitor];
            }

            // La elegida está apagada o desconectada: el dock va a otra mientras tanto, sin tocar el
            // ajuste, y vuelve a la suya en cuanto reaparece (la siguiente reconstrucción la encuentra).
            foreach (var monitor in monitors)
            {
                if (monitor.IsPrimary) return [monitor];
            }
            return [monitors[0]];
        }

        // Ajustes de antes de guardar el identificador: solo la posición. Si ya no existe, todas.
        if (legacyIndex is { } index && index >= 0 && index < monitors.Count) return [monitors[index]];

        return monitors;
    }

    /// <summary>El identificador de la pantalla que ocupa esa posición, para migrar un ajuste
    /// antiguo que solo guardaba la posición. Null si no hay tal pantalla o no se conoce su id.</summary>
    public static string? IdForLegacyIndex(IReadOnlyList<MonitorInfo> monitors, int index) =>
        index >= 0 && index < monitors.Count ? monitors[index].StableId : null;
}
