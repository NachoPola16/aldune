using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Aldune.Windowing;

/// <summary>
/// Cierre con fundido y encogimiento hasta el 95%, simétrico a la apertura. Es el mismo que ya tenían
/// las notas (<c>NoteWindow.OnClosingWithAnimation</c>); el gestor y Ajustes se abrían animados pero
/// desaparecían de golpe.
///
/// Un Close en WPF no se puede aplazar: se cancela el primero, se anima y se vuelve a cerrar al
/// terminar. Si la app se está cerrando WPF ignora la cancelación, así que nunca bloquea la salida.
/// </summary>
internal static class WindowCloseAnimation
{
    private static readonly TimeSpan Duration = TimeSpan.FromMilliseconds(140);
    private static readonly ConditionalWeakTable<Window, object> Animating = new();

    internal static void Attach(Window window) => window.Closing += OnClosing;

    /// <summary>Si la ventana ya está en su animación de cierre (a punto de desaparecer).</summary>
    internal static bool IsClosing(Window window) => Animating.TryGetValue(window, out _);

    private static void OnClosing(object? sender, CancelEventArgs e)
    {
        if (e.Cancel || sender is not Window window || window.Content is not UIElement content) return;
        if (IsClosing(window) || !SystemParameters.ClientAreaAnimation) return;

        Animating.Add(window, new object());
        e.Cancel = true;

        content.RenderTransformOrigin = new Point(0.5, 0.5);
        var scale = content.RenderTransform as ScaleTransform ?? new ScaleTransform(1, 1);
        content.RenderTransform = scale;

        var duration = new Duration(Duration);
        var ease = new QuadraticEase { EasingMode = EasingMode.EaseIn };
        var fade = new DoubleAnimation(content.Opacity, 0, duration) { EasingFunction = ease };
        fade.Completed += (_, _) => window.Close();

        content.BeginAnimation(UIElement.OpacityProperty, fade);
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(scale.ScaleX, 0.95, duration) { EasingFunction = ease });
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(scale.ScaleY, 0.95, duration) { EasingFunction = ease });
    }
}
