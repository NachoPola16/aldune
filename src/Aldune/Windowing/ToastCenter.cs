using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using Aldune.Core;
using Aldune.Interop;

namespace Aldune.Windowing;

/// <summary>
/// Muestra y apila los avisos de la app (<see cref="ToastWindow"/>) en la esquina inferior derecha de
/// la pantalla donde está el ratón (ver <see cref="ToastStack"/>).
///
/// Los globos de Windows esperaban solos si había algo a pantalla completa o una presentación; estos
/// son ventanas propias, así que lo hacen aquí: mientras Windows diga que no es buen momento
/// (<c>SHQueryUserNotificationState</c>), los avisos nuevos esperan y se enseñan después, en orden.
/// </summary>
internal sealed class ToastCenter : IDisposable
{
    private readonly List<ToastWindow> _shown = new();
    private readonly Queue<ToastWindow> _waiting = new();
    private readonly DispatcherTimer _waitTimer = new() { Interval = TimeSpan.FromSeconds(5) };
    private WorkingArea? _area;

    internal ToastCenter()
    {
        _waitTimer.Tick += (_, _) => ShowWaiting();
    }

    internal ToastWindow Show(ToastContent content)
    {
        var toast = new ToastWindow(content);
        toast.Closed += (_, _) =>
        {
            _shown.Remove(toast);
            Arrange();
        };
        // Un aviso que cambia de contenido cambia de alto: los de encima tienen que moverse.
        toast.SizeChanged += (_, e) => { if (e.HeightChanged && _shown.Contains(toast)) Arrange(); };

        _waiting.Enqueue(toast);
        ShowWaiting();
        return toast;
    }

    private void ShowWaiting()
    {
        if (_waiting.Count == 0)
        {
            _waitTimer.Stop();
            return;
        }
        if (WindowsIsBusy())
        {
            _waitTimer.Start();
            return;
        }

        _waitTimer.Stop();
        // La pantalla del ratón al aparecer, no la principal: es donde está mirando quien usa la app.
        _area = MonitorEnumerator.MonitorContainingCursor()?.WorkArea ?? _area;
        while (_waiting.TryDequeue(out var toast))
        {
            // Fuera de la vista hasta conocer su alto real; Arrange la coloca en su sitio antes del
            // primer fotograma (sigue oculta por CloakUntilFirstFrame).
            toast.Left = -32000;
            toast.Top = -32000;
            _shown.Add(toast);
            toast.Show();
            toast.UpdateLayout();
        }
        Arrange();
    }

    private void Arrange()
    {
        if (_shown.Count == 0) return;

        var area = _area ?? new WorkingArea(
            SystemParameters.WorkArea.Left, SystemParameters.WorkArea.Top,
            SystemParameters.WorkArea.Width, SystemParameters.WorkArea.Height);
        var live = _shown.Where(toast => !WindowCloseAnimation.IsClosing(toast)).ToList();
        var places = ToastStack.Place(area, live.Count == 0 ? 0 : live[0].Width, live.Select(toast => toast.ActualHeight).ToList());

        for (int index = 0; index < live.Count; index++)
        {
            var toast = live[index];
            if (places[index] is { } place)
            {
                toast.Visibility = Visibility.Visible;
                toast.MoveTo(place.Left, place.Top);
            }
            else
            {
                // No cabe: espera sin cerrarse, y vuelve a salir cuando se cierre otro.
                toast.Visibility = Visibility.Hidden;
            }
        }
    }

    // --- ¿Es buen momento para avisar? ---------------------------------------------------------

    private enum UserNotificationState
    {
        NotPresent = 1,
        Busy = 2,
        RunningD3DFullScreen = 3,
        PresentationMode = 4,
        AcceptsNotifications = 5,
        QuietTime = 6,
        App = 7,
    }

    [DllImport("shell32.dll")]
    private static extern int SHQueryUserNotificationState(out UserNotificationState state);

    private static bool WindowsIsBusy()
    {
        if (SHQueryUserNotificationState(out var state) != 0) return false;
        return state is UserNotificationState.Busy
            or UserNotificationState.RunningD3DFullScreen
            or UserNotificationState.PresentationMode;
    }

    public void Dispose()
    {
        _waitTimer.Stop();
        foreach (var toast in _shown.ToList()) toast.Close();
        _shown.Clear();
        _waiting.Clear();
    }
}
