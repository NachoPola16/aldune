using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Aldune.Interop;

namespace Aldune.Windowing;

/// <summary>
/// Autoscroll clásico (el de Chrome, los lectores de PDF o Word): un clic central fija un punto de
/// origen y, desde ahí, la vista se desplaza sola en la dirección del cursor — arriba, abajo y, si la
/// superficie lo permite, izquierda y derecha — más rápido cuanto más lejos esté del origen. Otro clic
/// central lo enciende y lo apaga; cualquier otro clic, Esc, la rueda o mover la barra a mano lo apagan.
///
/// La posición del cursor se sondea con <see cref="NativeMethods.GetCursorScreenPosition"/>, no con la
/// API de ratón de WPF: mientras el modo está activo el cursor suele estar FUERA de la ventana (ese es
/// todo el punto del modo) y WPF solo informa de la posición cuando la ventana recibe mensajes, así que
/// con ella el autoscroll se quedaría congelado en la última posición conocida.
///
/// No se usa ningún hook global: el clic central sí llega a una ventana aunque no se active —las no
/// activables como el dock (<c>WS_EX_NOACTIVATE</c>) reciben entrada de ratón, solo no reciben el foco—
/// y el seguimiento del cursor no necesita eventos, basta sondear Win32 cada tick.
/// </summary>
internal sealed class AutoScrollManager
{
    /// <summary>Zona muerta alrededor del origen, en DIPs: dentro de ella no hay desplazamiento.</summary>
    private const double DeadZone = 10;

    /// <summary>Distancia en DIPs a partir de la cual la velocidad alcanza el tope.</summary>
    private const double FullSpeedDistance = 160;

    /// <summary>Desplazamiento máximo por tick, en DIPs.</summary>
    private const double MaxStepPerTick = 16;

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(33) };
    private readonly Panel _host;
    private readonly ScrollViewer? _scrollViewer;
    private readonly TextBox? _textBox;
    private Border? _originMarker;
    private Point _origin;
    private bool _buttonsWereDown = true;

    internal AutoScrollManager(Panel host, ScrollViewer? scrollViewer, TextBox? textBox)
    {
        if (scrollViewer is null && textBox is null)
            throw new ArgumentException("Hay que dar una superficie desplazable.", nameof(host));

        _host = host;
        _scrollViewer = scrollViewer;
        _textBox = textBox;
        _timer.Tick += OnTick;

        host.PreviewMouseDown += OnHostPreviewMouseDown;
        host.PreviewKeyDown += OnHostPreviewKeyDown;
        host.MouseWheel += OnHostMouseWheel;
    }

    internal bool IsActive => _originMarker is not null;

    internal void Toggle()
    {
        if (IsActive) Stop();
        else Start();
    }

    private void Start()
    {
        if (!CanScroll()) return;

        var glyph = (FrameworkElement?)Application.Current.TryFindResource("AutoScrollOriginGlyph");
        if (glyph is null) return;

        _origin = Mouse.GetPosition(_host);
        _originMarker = new Border
        {
            Width = 26,
            Height = 26,
            IsHitTestVisible = false,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(_origin.X - 13, _origin.Y - 13, 0, 0),
            Background = (Brush)Application.Current.Resources["AlduneRaisedBrush"],
            CornerRadius = new CornerRadius(13),
            Opacity = 0.92,
            Child = glyph
        };
        _host.Children.Add(_originMarker);

        if (_host.Cursor is null)
        {
            // Si la superficie ya fija su cursor (IBeam del texto, mano de las casillas) no se toca:
            // restaurarlo al apagar el modo no es fiable.
            _host.Cursor = Cursors.ScrollAll;
        }

        _buttonsWereDown = true;
        _timer.Start();
    }

    internal void Stop()
    {
        _timer.Stop();

        if (_originMarker is not null)
        {
            _host.Children.Remove(_originMarker);
            _originMarker = null;
        }

        _host.ClearValue(FrameworkElement.CursorProperty);
        _buttonsWereDown = true;
    }

    private void OnHostPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Middle) Toggle();
        else if (IsActive) Stop();
    }

    private void OnHostPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && IsActive) Stop();
    }

    private void OnHostMouseWheel(object sender, MouseWheelEventArgs e)
    {
        // La rueda manda sobre el modo: girarla con el autoscroll activo lo apaga y deja pasar el giro.
        if (IsActive) Stop();
    }
    private void OnTick(object? sender, EventArgs e)
    {
        // El primer botón que se suelte tras arrancar ya no cuenta (es el clic central que encendió el
        // modo). El siguiente que se pulse — sea donde sea en la pantalla — lo apaga.
        bool anyDown = NativeMethods.IsAnyMouseButtonDown();
        if (_buttonsWereDown && !anyDown) _buttonsWereDown = false;
        else if (!_buttonsWereDown && anyDown) { Stop(); return; }

        var cursor = CursorPositionInHost();
        double stepX = ScrollStepFor(cursor.X - _origin.X);
        double stepY = ScrollStepFor(cursor.Y - _origin.Y);
        if (stepX == 0 && stepY == 0) return;

        ScrollBy(stepX, stepY);
    }

    /// <summary>La posición del cursor en las coordenadas de la ventana, sondeada por Win32.</summary>
    private Point CursorPositionInHost()
    {
        var screen = NativeMethods.GetCursorScreenPosition();
        var source = PresentationSource.FromVisual(_host);
        double scale = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        return _host.PointFromScreen(new Point(screen.X / scale, screen.Y / scale));
    }

    private static double ScrollStepFor(double offset)
    {
        double beyond = Math.Abs(offset) - DeadZone;
        if (beyond <= 0) return 0;

        return Math.Sign(offset) * Math.Min(beyond / FullSpeedDistance, 1) * MaxStepPerTick;
    }

    private bool CanScroll()
    {
        if (_scrollViewer is not null)
            return _scrollViewer.ScrollableHeight > 0 || _scrollViewer.ScrollableWidth > 0;

        return _textBox is not null && _textBox.ExtentHeight > _textBox.ViewportHeight;
    }

    private void ScrollBy(double stepX, double stepY)
    {
        if (_scrollViewer is not null)
        {
            if (stepY != 0) _scrollViewer.ScrollToVerticalOffset(_scrollViewer.VerticalOffset + stepY);
            if (stepX != 0) _scrollViewer.ScrollToHorizontalOffset(_scrollViewer.HorizontalOffset + stepX);
        }
        else if (_textBox is not null && stepY != 0)
        {
            _textBox.ScrollToVerticalOffset(_textBox.VerticalOffset + stepY);
        }
    }
}

