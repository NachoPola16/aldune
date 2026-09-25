namespace Aldune.Core;

public enum MonitorPowerReading { On, Off, NoReply }

/// <summary>
/// Qué pantallas están apagadas aunque Windows las siga dando por conectadas. Muchos monitores, al
/// apagarlos con su botón, se quedan en reposo y siguen en la lista de Windows (visto con dos
/// Gigabyte, uno por HDMI y otro por DisplayPort), así que el dock se quedaba en una pantalla negra.
/// Lo que sí dicen es su estado, preguntado por DDC/CI (código VCP 0xD6); este seguidor decide con esas
/// lecturas.
///
/// Hacen falta dos lecturas iguales seguidas para cambiar de estado: al encenderse, el monitor visto
/// alternaba lecturas buenas con fallidas, y una lectura suelta no debe mover el dock. Las fallidas
/// (<see cref="MonitorPowerReading.NoReply"/>) no cuentan ni a favor ni en contra: un monitor sin
/// DDC/CI se queda siempre como encendido, que es el comportamiento de antes.
/// </summary>
public sealed class MonitorPowerTracker
{
    private readonly Dictionary<string, (bool Off, MonitorPowerReading Last)> _states = new();

    public bool IsOff(string id) => _states.TryGetValue(id, out var state) && state.Off;

    public IReadOnlyCollection<string> OffIds => _states.Where(pair => pair.Value.Off).Select(pair => pair.Key).ToList();

    /// <summary>Apunta una lectura. Devuelve true si la pantalla acaba de pasar a apagada o a encendida.</summary>
    public bool Report(string id, MonitorPowerReading reading)
    {
        if (reading == MonitorPowerReading.NoReply) return false;

        var state = _states.TryGetValue(id, out var known) ? known : (Off: false, Last: MonitorPowerReading.NoReply);
        bool readsOff = reading == MonitorPowerReading.Off;
        bool changed = state.Last == reading && readsOff != state.Off;
        _states[id] = (changed ? readsOff : state.Off, reading);
        return changed;
    }

    /// <summary>
    /// Valor del modo de energía VCP 0xD6 (VESA DDC/CI y MCCS): 1 encendido; 2 a 4 los estados de
    /// ahorro de DPM; 5 apagado con el botón. Cualquier otro valor no se sabe interpretar.
    /// </summary>
    public static MonitorPowerReading FromVcpPowerMode(uint value) => value switch
    {
        1 => MonitorPowerReading.On,
        >= 2 and <= 5 => MonitorPowerReading.Off,
        _ => MonitorPowerReading.NoReply,
    };
}
