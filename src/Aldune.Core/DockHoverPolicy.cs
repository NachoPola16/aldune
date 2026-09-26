namespace Aldune.Core;

public enum DockHoverDecision { None, Open, Close }

/// <summary>
/// Lo que el dock ve del ratón en un sondeo. Las zonas las calcula la ventana (dependen de dónde ha
/// colocado WPF las pestañas); aquí solo se decide con ellas.
/// </summary>
/// <param name="Time">Reloj monótono del sondeo.</param>
/// <param name="X">Cursor en DIP de pantalla, para medir la velocidad.</param>
/// <param name="IsExpanded">Si el abanico está desplegado ahora (lo manda <see cref="FanStateMachine"/>).</param>
/// <param name="InRestZone">Dentro de la tira de reposo y su holgura.</param>
/// <param name="InExpandedZone">Dentro de la superficie desplegada y su holgura (se calcula también plegado).</param>
/// <param name="OnTarget">Encima de una pestaña o de un botón: el usuario está usando el dock.</param>
/// <param name="AgainstEdge">En el último píxel del canto de la pantalla.</param>
/// <param name="ButtonDown">Botón izquierdo pulsado en cualquier sitio.</param>
public readonly record struct DockHoverSample(
    TimeSpan Time,
    double X,
    double Y,
    bool IsExpanded,
    bool InRestZone,
    bool InExpandedZone,
    bool OnTarget,
    bool AgainstEdge,
    bool ButtonDown);

/// <summary>
/// Tiempos de apertura y cierre del dock. Medidos con sondas en 2026-09-26 (ver docs/STATUS.md): antes
/// abría en el primer sondeo dentro de la tira y cerraba 90 ms después de salir, así que se abría al
/// pasar hacia la barra de scroll, la otra pantalla o la barra de tareas, y se cerraba con un temblor
/// de 5 px. Referencias: el Dock de macOS espera 200 ms para abrir; la barra de tareas de Windows en
/// autoocultar, unos 265 ms (medido).
/// </summary>
public sealed record DockHoverTuning(TimeSpan RestDwell, bool EdgePressOpens)
{
    /// <summary>
    /// Canto donde el ratón se detiene. Empujar contra él es intención clara (abre al momento); pasar
    /// por la tira sin pararse, no.
    /// </summary>
    public static readonly DockHoverTuning PhysicalEdge = new(TimeSpan.FromMilliseconds(150), EdgePressOpens: true);

    /// <summary>
    /// Canto que linda con otra pantalla (el ratón pasa de largo) o que comparte una barra de tareas
    /// que se oculta sola (empujar contra el canto es el gesto de la barra, no del dock).
    /// </summary>
    public static readonly DockHoverTuning SharedEdge = new(TimeSpan.FromMilliseconds(250), EdgePressOpens: false);

    /// <summary>Cierre si se abrió pero no se llegó a usar: una apertura sin querer se va enseguida.</summary>
    public static readonly TimeSpan QuickCloseDelay = TimeSpan.FromMilliseconds(100);

    /// <summary>Cierre tras haber pasado por una pestaña o un botón: perdona pasarse y temblores.</summary>
    public static readonly TimeSpan EngagedCloseDelay = TimeSpan.FromMilliseconds(400);

    /// <summary>Tras cerrarse en uso, volver a donde estaba el abanico lo reabre sin ir al canto.</summary>
    public static readonly TimeSpan RecoverWindow = TimeSpan.FromMilliseconds(400);

    /// <summary>
    /// Por encima de esta velocidad (DIP/ms) el cursor va de paso: la espera en la tira vuelve a
    /// empezar. 1,5 DIP/ms son 75 DIP entre dos sondeos de 50 ms.
    /// </summary>
    public const double TravelSpeed = 1.5;

    /// <summary>
    /// Espera de los tooltips de los botones del dock. Los de WPF esperan 1000 ms, y el dock no suele
    /// estar abierto tanto rato con el ratón quieto: medido, el de "+" tardaba 905 ms.
    /// </summary>
    public static readonly TimeSpan ButtonTooltipDelay = TimeSpan.FromMilliseconds(500);
}

/// <summary>
/// Decide cuándo abrir y cerrar el dock a partir de los sondeos del ratón. Puro y con el reloj en las
/// muestras, para que los escenarios medidos con la sonda (pasarse, temblar, salir y volver, cruzar
/// a otra pantalla…) sean tests.
///
/// No sabe nada de popups, márgenes de cortesía ni arrastres: mientras la ventana tiene una razón
/// propia para mantener el dock abierto llama a <see cref="Hold"/> y no le pasa muestras.
/// </summary>
public sealed class DockHoverPolicy
{
    private readonly DockHoverTuning _tuning;
    private DockHoverSample? _last;
    private TimeSpan? _dwellStart;
    private TimeSpan? _outsideSince;
    private bool _engaged;
    private TimeSpan? _recoverUntil;
    private bool _pressedInside;

    public DockHoverPolicy(DockHoverTuning tuning) => _tuning = tuning;

    /// <summary>Si el cursor cuenta como dentro del dock (para el teclado y la pausa de layout).</summary>
    public bool IsPointerInside { get; private set; }

    public DockHoverDecision Update(DockHoverSample sample)
    {
        var previous = _last;
        _last = sample;

        // Dónde empezó la pulsación, no dónde está ahora: arrastrar desde dentro puede salirse.
        if (!sample.ButtonDown) _pressedInside = false;
        else if (previous is not { ButtonDown: true }) _pressedInside = sample.IsExpanded && sample.InExpandedZone;

        return sample.IsExpanded ? UpdateExpanded(sample) : UpdateCollapsed(sample, previous);
    }

    /// <summary>
    /// La ventana mantiene el dock abierto por su cuenta (menú, margen de cortesía, crear una nota):
    /// cuenta como uso, y la cuenta atrás del cierre empieza de cero cuando deje de sujetarlo.
    /// </summary>
    public void Hold()
    {
        _engaged = true;
        _outsideSince = null;
        _dwellStart = null;
        _recoverUntil = null;
        _last = null;
        IsPointerInside = true;
    }

    /// <summary>El dock se ha plegado por otra razón (pantalla completa, ocultarlo a mano).</summary>
    public void Collapsed()
    {
        _engaged = false;
        _outsideSince = null;
        _dwellStart = null;
        _recoverUntil = null;
        IsPointerInside = false;
    }

    private DockHoverDecision UpdateExpanded(DockHoverSample sample)
    {
        _dwellStart = null;

        // Con el botón pulsado desde dentro (la barra de scroll del dock), salirse es parte del gesto.
        if (sample.InExpandedZone || _pressedInside)
        {
            _outsideSince = null;
            IsPointerInside = true;
            if (sample.OnTarget) _engaged = true;
            return DockHoverDecision.None;
        }

        IsPointerInside = false;
        _outsideSince ??= sample.Time;
        var delay = _engaged ? DockHoverTuning.EngagedCloseDelay : DockHoverTuning.QuickCloseDelay;
        if (sample.Time - _outsideSince < delay) return DockHoverDecision.None;

        _recoverUntil = _engaged ? sample.Time + DockHoverTuning.RecoverWindow : null;
        _engaged = false;
        _outsideSince = null;
        return DockHoverDecision.Close;
    }

    private DockHoverDecision UpdateCollapsed(DockHoverSample sample, DockHoverSample? previous)
    {
        _outsideSince = null;
        IsPointerInside = false;

        if (_recoverUntil is { } until && sample.Time > until) _recoverUntil = null;

        if (sample.ButtonDown)
        {
            _dwellStart = null;
            return DockHoverDecision.None;
        }

        if (_recoverUntil is not null && sample.InExpandedZone)
        {
            _recoverUntil = null;
            return Open(engaged: true);
        }

        if (!sample.InRestZone)
        {
            _dwellStart = null;
            return DockHoverDecision.None;
        }

        if (_tuning.EdgePressOpens && sample.AgainstEdge) return Open(engaged: false);

        if (previous is { } last && IsTravelling(last, sample)) _dwellStart = sample.Time;
        _dwellStart ??= sample.Time;

        return sample.Time - _dwellStart >= _tuning.RestDwell
            ? Open(engaged: false)
            : DockHoverDecision.None;
    }

    private DockHoverDecision Open(bool engaged)
    {
        _dwellStart = null;
        _outsideSince = null;
        _engaged = engaged;
        IsPointerInside = true;
        return DockHoverDecision.Open;
    }

    private static bool IsTravelling(DockHoverSample from, DockHoverSample to)
    {
        double ms = (to.Time - from.Time).TotalMilliseconds;
        if (ms <= 0) return false;
        double dx = to.X - from.X, dy = to.Y - from.Y;
        return Math.Sqrt(dx * dx + dy * dy) / ms > DockHoverTuning.TravelSpeed;
    }
}
