using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Aldune.Interop;

namespace Aldune.Windowing;

/// <param name="Closes">Si pulsarla cierra el aviso. Descargar, por ejemplo, lo deja abierto si
/// falla para poder contar el error en el mismo sitio.</param>
internal sealed record ToastAction(string Label, Action Run, bool Primary = false, bool Closes = true);

/// <param name="AutoDismiss">Sin valor, el aviso se queda hasta que se cierra: los recordatorios no
/// tienen un Centro de actividades donde recuperarlos si se van solos.</param>
internal sealed record ToastContent(
    string Title,
    string Message,
    IReadOnlyList<ToastAction>? Actions = null,
    TimeSpan? AutoDismiss = null);

/// <summary>
/// Un aviso de Aldune en la esquina de la pantalla, con el aspecto de la app. Sustituye a los globos
/// de la bandeja (bienvenida, recordatorios) y a la ventana de actualización, que llevaba la barra de
/// título de Windows. No roba el foco (WS_EX_NOACTIVATE), no sale en Alt+Tab (WS_EX_TOOLWINDOW) y
/// pasar el ratón por encima detiene el cierre automático. Lo coloca <see cref="ToastCenter"/>.
/// </summary>
internal partial class ToastWindow : Window
{
    private static ImageSource? _logo;
    private readonly DispatcherTimer _dismissTimer = new();
    private bool _placed;

    internal ToastWindow(ToastContent content)
    {
        InitializeComponent();
        NativeMethods.CloakUntilFirstFrame(this);
        WindowCloseAnimation.Attach(this);
        Logo.Source = LoadLogo();
        _dismissTimer.Tick += (_, _) => Close();
        MouseEnter += (_, _) => _dismissTimer.Stop();
        MouseLeave += (_, _) => { if (_dismissTimer.Interval > TimeSpan.Zero) _dismissTimer.Start(); };
        Closed += (_, _) => _dismissTimer.Stop();
        SetContent(content);
    }

    /// <summary>Cambia el contenido sin cerrar el aviso (por ejemplo, "Buscando…" → resultado).</summary>
    internal void SetContent(ToastContent content)
    {
        TitleText.Text = content.Title;
        MessageText.Text = content.Message;
        AutomationProperties.SetName(this, content.Title);

        ActionsPanel.Children.Clear();
        var actions = content.Actions ?? [];
        ActionsPanel.Visibility = actions.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        foreach (var action in actions)
        {
            var button = new Button
            {
                Content = action.Label,
                Margin = new Thickness(8, 0, 0, 0),
                Style = (Style)FindResource(action.Primary ? "PrimaryColorDialogButtonStyle" : "ColorDialogButtonStyle"),
            };
            button.Click += (_, _) =>
            {
                action.Run();
                if (action.Closes) Close();
            };
            ActionsPanel.Children.Add(button);
        }

        _dismissTimer.Stop();
        _dismissTimer.Interval = content.AutoDismiss ?? TimeSpan.Zero;
        if (content.AutoDismiss is not null && !IsMouseOver) _dismissTimer.Start();
    }

    /// <summary>
    /// Coloca el aviso. La primera vez entra deslizándose desde el canto; después, si otro aviso lo
    /// empuja hacia arriba, se desplaza suave a su sitio nuevo.
    /// </summary>
    internal void MoveTo(double left, double top)
    {
        if (!_placed)
        {
            _placed = true;
            Left = left;
            Top = top;
            PlayEnterAnimation();
            return;
        }

        if (!SystemParameters.ClientAreaAnimation || !IsVisible)
        {
            Left = left;
            Top = top;
            return;
        }
        Left = left;
        var ease = new QuinticEase { EasingMode = EasingMode.EaseOut };
        BeginAnimation(TopProperty, new DoubleAnimation(Top, top, new Duration(TimeSpan.FromMilliseconds(200)))
        {
            EasingFunction = ease,
            FillBehavior = FillBehavior.Stop,
        });
        Top = top;
    }

    private void PlayEnterAnimation()
    {
        if (!SystemParameters.ClientAreaAnimation) return;

        var content = (UIElement)Content;
        var slide = new TranslateTransform(24, 0);
        content.RenderTransform = slide;
        content.Opacity = 0;
        var duration = new Duration(TimeSpan.FromMilliseconds(220));
        var ease = new QuinticEase { EasingMode = EasingMode.EaseOut };
        slide.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(24, 0, duration) { EasingFunction = ease });
        content.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duration) { EasingFunction = ease });
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        NativeMethods.ApplyRoundedCorners(hwnd);
        NativeMethods.MakeToolWindowNoActivate(hwnd);
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    /// <summary>El logo de la app, del mismo .ico que usa la bandeja. Se coge el fotograma más cercano
    /// a 64 px y se reduce: a 28 DIP con escalado del 200 % harían falta 56, y reducir queda nítido
    /// donde ampliar el de 32 se vería borroso.</summary>
    private static ImageSource? LoadLogo()
    {
        if (_logo is not null) return _logo;
        try
        {
            using var stream = typeof(ToastWindow).Assembly.GetManifestResourceStream("Aldune.aldune.ico");
            if (stream is null) return null;
            var decoder = new IconBitmapDecoder(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            _logo = decoder.Frames.OrderBy(frame => Math.Abs(frame.PixelWidth - 64)).First();
            _logo.Freeze();
        }
        catch (Exception ex) when (ex is System.IO.IOException or NotSupportedException or System.IO.FileFormatException)
        {
            _logo = null;
        }
        return _logo;
    }
}
