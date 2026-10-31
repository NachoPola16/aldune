using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using Aldune.Interop;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Image = System.Windows.Controls.Image;

namespace Aldune.Windowing;

/// <summary>
/// Fades a closing note as ONE piece: frame, border and shadow included.
///
/// A note is an opaque window (it keeps ClearType, see NoteWindow), and an opaque window cannot change its opacity: animating the
/// content only melts the content into the window background while the frame stays at full strength and then vanishes with a pop
/// (very visible with the XP look). Windows also refuses <c>WS_EX_LAYERED</c> on these WPF windows, so a layered fade is not an option.
/// Instead the window is photographed, hidden (cloaked) and a transparent window showing that photo fades and shrinks in its place.
/// If anything fails the caller just closes the window as before.
/// </summary>
internal static class ClosingGhost
{
    private const double ShadowRoom = 24;
    private const uint PW_RENDERFULLCONTENT = 0x2;

    /// <summary>
    /// Photographs <paramref name="source"/> as WPF draws it now: the window background under its content, at the screen's pixel density.
    /// (PrintWindow was tried first and returns only the background of a WPF window.) Null if there is nothing to draw.
    /// </summary>
    internal static BitmapSource? Capture(Window source)
    {
        if (source.Content is not FrameworkElement content || source.ActualWidth < 1 || source.ActualHeight < 1) return null;
        var dpi = VisualTreeHelper.GetDpi(source);
        double width = source.ActualWidth, height = source.ActualHeight;

        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawRectangle(source.Background, null, new Rect(0, 0, width, height));
            context.DrawRectangle(new VisualBrush(content) { Stretch = Stretch.None, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top },
                null, new Rect(0, 0, content.ActualWidth, content.ActualHeight));
        }

        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(width * dpi.DpiScaleX), (int)Math.Ceiling(height * dpi.DpiScaleY),
            96 * dpi.DpiScaleX, 96 * dpi.DpiScaleY, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>
    /// Builds the transparent window that stands in for <paramref name="source"/>: the photo, clipped to the real corner radius, with a
    /// soft shadow. Returned not shown, so a test can inspect it.
    /// </summary>
    internal static Window Build(Window source, BitmapSource photo)
    {
        double width = source.ActualWidth, height = source.ActualHeight;
        // Windows 11 rounds these windows with DWMWCP_ROUNDSMALL (4 px); the square look has no rounding.
        double radius = ThemeManager.IsSquare ? 0 : 4;

        var picture = new Image { Source = photo, Width = width, Height = height, Stretch = Stretch.Fill };
        RenderOptions.SetBitmapScalingMode(picture, BitmapScalingMode.HighQuality);
        picture.Clip = new RectangleGeometry(new Rect(0, 0, width, height), radius, radius);

        var shadow = new System.Windows.Controls.Border
        {
            Width = width, Height = height, CornerRadius = new CornerRadius(radius), Background = Brushes.Black,
            Effect = new DropShadowEffect { BlurRadius = 18, ShadowDepth = 4, Direction = 270, Color = Colors.Black, Opacity = 0.35 }
        };

        var root = new System.Windows.Controls.Grid
        {
            Margin = new Thickness(ShadowRoom), RenderTransformOrigin = new System.Windows.Point(0.5, 0.5),
            RenderTransform = new ScaleTransform(1, 1)
        };
        root.Children.Add(shadow);
        root.Children.Add(picture);

        return new Window
        {
            WindowStyle = WindowStyle.None, AllowsTransparency = true, Background = Brushes.Transparent, ShowInTaskbar = false,
            ShowActivated = false, Topmost = true, ResizeMode = ResizeMode.NoResize, Focusable = false, IsHitTestVisible = false,
            Left = source.Left - ShadowRoom, Top = source.Top - ShadowRoom,
            Width = width + ShadowRoom * 2, Height = height + ShadowRoom * 2, Content = root
        };
    }

    /// <summary>
    /// Replaces <paramref name="source"/> with the fading ghost and calls <paramref name="done"/> when it is gone. Returns false (and does
    /// nothing) when the window cannot be photographed.
    /// </summary>
    internal static bool Play(Window source, TimeSpan duration, Action done)
    {
        BitmapSource? photo;
        try { photo = Capture(source); }
        catch (Exception ex) when (ex is ExternalException or InvalidOperationException or ArgumentException or OverflowException) { return false; }
        if (photo is null) return false;

        var ghost = Build(source, photo);
        ghost.Show();
        // The ghost has to be on screen before the real window disappears, or there is a blink: cloak one render pass later.
        ghost.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render, new Action(() =>
        {
            NativeMethods.SetCloak(source, true);
            var scale = (ScaleTransform)((System.Windows.Controls.Grid)ghost.Content).RenderTransform;
            var length = new Duration(duration);
            var ease = new QuadraticEase { EasingMode = EasingMode.EaseIn };
            var fade = new DoubleAnimation(1, 0, length) { EasingFunction = ease };
            fade.Completed += (_, _) => { ghost.Close(); done(); };
            ghost.BeginAnimation(UIElement.OpacityProperty, fade);
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1, 0.95, length) { EasingFunction = ease });
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, 0.95, length) { EasingFunction = ease });
        }));
        return true;
    }
}
