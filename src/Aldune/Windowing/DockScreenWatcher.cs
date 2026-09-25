using System.Windows.Threading;
using Aldune.Core;
using Aldune.Interop;

namespace Aldune.Windowing;

/// <summary>
/// Decide cuándo el dock tiene que cambiar de pantalla por algo de lo que Windows no avisa:
///
/// - **Una pantalla apagada con su botón** que Windows sigue dando por conectada. Cada segundo se pregunta
///   por DDC/CI (<see cref="MonitorPower"/>, fuera del hilo de la interfaz) a la pantalla elegida, o a
///   todas si el dock va en todas; <see cref="MonitorPowerTracker"/> decide con dos lecturas seguidas,
///   así que el dock cambia en 1-2 s. Cada lectura son ~60 ms en segundo plano.
///   Solo con más de una pantalla: con una sola no hay a dónde ir.
/// - **"El dock va a la pantalla del ratón"** (<see cref="AppSettings.DockFollowsMouse"/>): el respaldo
///   para monitores que no contestan. Si el cursor se queda en otra pantalla, el dock va detrás.
///
/// No mueve nada por sí mismo: pide reconstruir los docks, y <c>App.BuildDocks</c> elige con
/// <see cref="PoweredOff"/> o con la pantalla del ratón.
/// </summary>
internal sealed class DockScreenWatcher : IDisposable
{
    private static readonly TimeSpan CursorSettle = TimeSpan.FromMilliseconds(800);

    private readonly AppSettings _settings;
    private readonly Func<IReadOnlyCollection<string>> _dockDevices;
    private readonly Action _rebuild;
    private readonly MonitorPowerTracker _power = new();
    private readonly DispatcherTimer _powerTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _cursorTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private bool _reading;
    private bool _disposed;
    private string? _cursorCandidate;
    private DateTime _cursorSince;

    /// <param name="dockDevices">Las pantallas (DeviceName) donde hay dock ahora mismo.</param>
    internal DockScreenWatcher(AppSettings settings, Func<IReadOnlyCollection<string>> dockDevices, Action rebuild)
    {
        _settings = settings;
        _dockDevices = dockDevices;
        _rebuild = rebuild;
        _powerTimer.Tick += (_, _) => CheckPower();
        _cursorTimer.Tick += (_, _) => CheckCursor();
        _powerTimer.Start();
        _cursorTimer.Start();
    }

    /// <summary>Identificadores (<see cref="MonitorInfo.StableId"/>) de las pantallas que dicen estar apagadas.</summary>
    internal IReadOnlyCollection<string> PoweredOff => _power.OffIds;

    private void CheckPower()
    {
        if (_reading || _settings.DockFollowsMouse) return;

        var monitors = MonitorEnumerator.EnumerateMonitors();
        if (monitors.Count < 2) return;

        var watched = monitors
            .Where(monitor => monitor.StableId is not null
                && (_settings.TargetMonitorId is not { } target || monitor.StableId == target))
            .ToDictionary(monitor => monitor.DeviceName, monitor => monitor.StableId!);
        if (watched.Count == 0) return;

        _reading = true;
        var devices = watched.Keys.ToList();
        Task.Run(() => MonitorPower.Read(devices)).ContinueWith(task =>
        {
            _reading = false;
            if (_disposed || task.IsFaulted) return;

            bool changed = false;
            foreach (var (device, reading) in task.Result)
            {
                if (!watched.TryGetValue(device, out var id) || !_power.Report(id, reading)) continue;
                changed = true;
                DockDiagnostics.Write("pantallas",
                    $"{device} dice estar {(_power.IsOff(id) ? "apagada" : "encendida")} (DDC/CI): se recolocan los docks");
            }
            if (changed) _rebuild();
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    private void CheckCursor()
    {
        if (!_settings.DockFollowsMouse)
        {
            _cursorCandidate = null;
            return;
        }

        var monitor = MonitorEnumerator.MonitorContainingCursor();
        if (monitor is null || _dockDevices().Contains(monitor.Value.DeviceName))
        {
            _cursorCandidate = null;
            return;
        }

        // Un paso rápido por otra pantalla (camino de la esquina opuesta) no mueve el dock: tiene
        // que quedarse ahí un momento.
        if (_cursorCandidate != monitor.Value.DeviceName)
        {
            _cursorCandidate = monitor.Value.DeviceName;
            _cursorSince = DateTime.UtcNow;
            return;
        }
        if (DateTime.UtcNow - _cursorSince < CursorSettle) return;

        _cursorCandidate = null;
        DockDiagnostics.Write("pantallas", $"el ratón está en {monitor.Value.DeviceName}: el dock va detrás");
        _rebuild();
    }

    public void Dispose()
    {
        _disposed = true;
        _powerTimer.Stop();
        _cursorTimer.Stop();
    }
}
