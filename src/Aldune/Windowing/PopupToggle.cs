using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Aldune.Interop;

namespace Aldune.Windowing;

/// <summary>
/// Convierte un disparador en interruptor de su popup: si el popup está abierto y vuelves a pulsar
/// el disparador, se cierra y se queda cerrado, en vez de cerrarse y reabrirse.
///
/// El problema no está en el handler del disparador sino en el orden en que WPF reparte la entrada.
/// Con <c>StaysOpen="False"</c> el popup toma la captura del ratón (<c>Mouse.Capture</c> con
/// <c>CaptureMode.SubTree</c>) y se descarta EN EL PROPIO mouse-down, en cuanto detecta que el clic
/// cae fuera de su contenido — antes de que el evento llegue al disparador. Así que el handler del
/// disparador siempre ve <c>IsOpen=false</c> (el popup ya se ha cerrado) y vuelve a abrirlo: la
/// comprobación de "¿está abierto?" en el propio disparador no puede funcionar nunca.
///
/// La señal fiable es el paso de <see cref="Popup.IsOpen"/> a false: si en ese momento el puntero está
/// sobre el disparador y hay un botón del ratón pulsado, ese cierre lo ha provocado el propio
/// disparador, y el open que llega a continuación (en el mouse-up del mismo clic) hay que consumirlo,
/// no abrir.
///
/// No sirve el evento <see cref="Popup.Closed"/>, que es en lo que se apoyaba esto antes: con
/// <c>AllowsTransparency</c> y animación, WPF difiere la destrucción de la ventana del popup, y si el
/// mouse-up lo reabre antes, <c>Closed</c> no llega a dispararse nunca. Medido con clics físicos
/// (SendInput) sobre el "⋯" de la nota: IsOpen cae a false en el mouse-down con el botón pulsado,
/// el Click reabre en el mouse-up, y no hay ni un solo Closed entre medias.
///
/// La comprobación del puntero es por coordenadas y no por <c>IsMouseOver</c> a propósito: con la
/// captura del popup activa, WPF ya no sabe que el cursor está sobre el botón que se acaba de pulsar.
/// </summary>
internal sealed class PopupToggle
{
    /// <summary>
    /// Cuánto tiempo sigue contando un cierre como "lo ha hecho el disparador". Basta con cubrir el
    /// gesto completo (down → descarte → up) y que sea corto: si el usuario pulsa el disparador, suelta
    /// fuera y vuelve a pulsar, un cierre viejo no debe comerse un open genuino.
    /// </summary>
    private static readonly TimeSpan FreshCloseWindow = TimeSpan.FromMilliseconds(700);

    private readonly IsOpenWatcher _watcher;
    private UIElement? _trigger;
    private DateTime _closedByTriggerAt = DateTime.MinValue;

    internal PopupToggle(Popup popup)
    {
        // Un binding y no DependencyPropertyDescriptor.AddValueChanged: este último guarda el popup
        // en una tabla estática y mantendría viva cada ventana de nota cerrada.
        _watcher = new IsOpenWatcher(this);
        BindingOperations.SetBinding(_watcher, IsOpenWatcher.IsOpenProperty,
            new Binding(nameof(Popup.IsOpen)) { Source = popup, Mode = BindingMode.OneWay });
    }

    /// <summary>El disparador de este popup. Hay que fijarlo en cada apertura: el menú de una pestaña
    /// del dock tiene uno distinto según la pestaña que se haya pulsado.</summary>
    internal void SetTrigger(UIElement? trigger) => _trigger = trigger;

    private void OnPopupClosed()
    {
        bool buttonDown = Mouse.LeftButton == MouseButtonState.Pressed
            || Mouse.RightButton == MouseButtonState.Pressed;

        _closedByTriggerAt = _trigger is not null && buttonDown && IsCursorOver(_trigger)
            ? DateTime.UtcNow
            : DateTime.MinValue;
    }

    /// <summary>
    /// Lo llama el handler que abre el popup. Devuelve true cuando ese open es en realidad el que
    /// cierra: no hay que abrir, y queda en manos del llamante marcar el gesto como manejado.
    /// </summary>
    internal bool ShouldConsumeOpen()
    {
        bool consume = DateTime.UtcNow - _closedByTriggerAt < FreshCloseWindow;
        _closedByTriggerAt = DateTime.MinValue;
        return consume;
    }

    /// <summary>¿Está el cursor físico sobre el disparador? Convierte los bordes del elemento a
    /// píxeles de pantalla — <c>PointToScreen</c> ya los da en píxeles, y el tamaño hay que
    /// escalarlo con el DPI de ese elemento, igual que hace el propio dock al pasar el rect de una
    /// pestaña a <see cref="AppCoordinator.OpenOrActivateNote"/>.</summary>
    private bool IsCursorOver(UIElement trigger)
    {
        if (!trigger.IsVisible) return false;

        var topLeft = trigger.PointToScreen(new Point(0, 0));
        var dpi = VisualTreeHelper.GetDpi(trigger);
        var cursor = NativeMethods.GetCursorScreenPosition();
        double width = trigger.RenderSize.Width * dpi.DpiScaleX;
        double height = trigger.RenderSize.Height * dpi.DpiScaleY;

        return cursor.X >= topLeft.X && cursor.X < topLeft.X + width
            && cursor.Y >= topLeft.Y && cursor.Y < topLeft.Y + height;
    }

    /// <summary>Recibe por binding el IsOpen del popup y avisa al toggle cuando pasa a false, en el
    /// mismo instante en que WPF lo cierra (dentro del mouse-down que lo descarta).</summary>
    private sealed class IsOpenWatcher : DependencyObject
    {
        internal static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(
            "IsOpen", typeof(bool), typeof(IsOpenWatcher),
            new PropertyMetadata(false, (d, e) =>
            {
                if (!(bool)e.NewValue) ((IsOpenWatcher)d)._owner.OnPopupClosed();
            }));

        private readonly PopupToggle _owner;

        internal IsOpenWatcher(PopupToggle owner) => _owner = owner;
    }
}
