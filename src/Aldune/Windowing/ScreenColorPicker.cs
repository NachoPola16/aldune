using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Aldune.Resources;

namespace Aldune.Windowing;

/// <summary>
/// An eyedropper: a nearly invisible window over every screen that shows the colour under the cursor in a small chip and takes it on a click
/// (Esc or a right click cancels). The colour is read from the screen itself, so it works over any program. The overlay is hidden for the
/// instant of the reading, so it never tints what is read.
/// </summary>
internal sealed class ScreenColorPicker : Window
{
    private const uint SWP_NOACTIVATE = 0x0010;
    private static readonly IntPtr HWND_TOPMOST = new(-1);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

    private readonly Border _chip;
    private readonly Border _swatch;
    private readonly TextBlock _hex;
    private readonly TextBlock _hint;

    /// <summary>The colour that was taken, or null if the user cancelled.</summary>
    public Color? Picked { get; private set; }

    private ScreenColorPicker()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        // Alpha 1, not 0: a fully transparent window lets the mouse through to whatever is below it.
        Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));
        ShowInTaskbar = false;
        Topmost = true;
        ResizeMode = ResizeMode.NoResize;
        Cursor = Cursors.Cross;

        _swatch = new Border { Width = 26, Height = 26, CornerRadius = new CornerRadius(4), BorderBrush = Brushes.White, BorderThickness = new Thickness(1.5) };
        _hex = new TextBlock { Foreground = Brushes.White, FontSize = 13, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
        _hint = new TextBlock { Text = Strings.PickScreenHint, Foreground = new SolidColorBrush(Color.FromRgb(200, 196, 208)), FontSize = 11, Margin = new Thickness(0, 6, 0, 0) };
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(_swatch);
        row.Children.Add(_hex);
        var stack = new StackPanel();
        stack.Children.Add(row);
        stack.Children.Add(_hint);
        _chip = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(235, 24, 20, 32)), CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 8, 12, 8),
            IsHitTestVisible = false, Child = stack, Visibility = Visibility.Collapsed
        };
        var canvas = new Canvas();
        canvas.Children.Add(_chip);
        Content = canvas;

        SourceInitialized += (_, _) =>
        {
            // Physical pixels of the whole virtual screen (every monitor), whatever the DPI of each one.
            var area = System.Windows.Forms.SystemInformation.VirtualScreen;
            SetWindowPos(new WindowInteropHelper(this).Handle, HWND_TOPMOST, area.X, area.Y, area.Width, area.Height, SWP_NOACTIVATE);
        };
        MouseMove += (_, _) => FollowCursor();
        MouseLeftButtonDown += (_, e) => { e.Handled = true; Take(); };
        MouseRightButtonDown += (_, e) => { e.Handled = true; DialogResult = false; };
        KeyDown += (_, e) => { if (e.Key == Key.Escape) { e.Handled = true; DialogResult = false; } };
        Loaded += (_, _) => { Focus(); FollowCursor(); };
    }

    /// <summary>Lets the user pick a colour from the screen; null if cancelled.</summary>
    public static Color? Pick(Window owner)
    {
        var picker = new ScreenColorPicker { Owner = owner };
        return picker.ShowDialog() == true ? picker.Picked : null;
    }

    private static Color ReadAt(System.Drawing.Point point)
    {
        using var bitmap = new System.Drawing.Bitmap(1, 1);
        using (var graphics = System.Drawing.Graphics.FromImage(bitmap))
            graphics.CopyFromScreen(point.X, point.Y, 0, 0, new System.Drawing.Size(1, 1));
        var pixel = bitmap.GetPixel(0, 0);
        return Color.FromRgb(pixel.R, pixel.G, pixel.B);
    }

    private void FollowCursor()
    {
        var cursor = System.Windows.Forms.Cursor.Position;
        var color = ReadAt(cursor);
        _swatch.Background = new SolidColorBrush(color);
        _hex.Text = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        // The chip sits beside the cursor (down and to the right, or the other way near an edge) so it never covers the pixel being read.
        var at = PointFromScreen(new Point(cursor.X, cursor.Y));
        _chip.Visibility = Visibility.Visible;
        _chip.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double x = at.X + 22, y = at.Y + 22;
        if (x + _chip.DesiredSize.Width > ActualWidth) x = at.X - 22 - _chip.DesiredSize.Width;
        if (y + _chip.DesiredSize.Height > ActualHeight) y = at.Y - 22 - _chip.DesiredSize.Height;
        Canvas.SetLeft(_chip, Math.Max(0, x));
        Canvas.SetTop(_chip, Math.Max(0, y));
    }

    private void Take()
    {
        var cursor = System.Windows.Forms.Cursor.Position;
        // Out of the way for a moment, so the reading is of the screen and not of this window.
        Opacity = 0;
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(70) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            Picked = ReadAt(cursor);
            DialogResult = true;
        };
        timer.Start();
    }
}
