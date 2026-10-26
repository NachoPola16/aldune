using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Aldune.Core;
using Aldune.Resources;

namespace Aldune.Windowing;

/// <summary>
/// Enseña el QR de un código de perfil. Lleva la clave de cifrado, así que: va siempre en negro sobre blanco (los lectores no leen bien un QR
/// claro sobre oscuro), avisa, no se guarda en ningún sitio y se cierra solo a los <see cref="LifetimeSeconds"/> segundos.
/// </summary>
internal sealed class ShareQrWindow : Window
{
    private const int LifetimeSeconds = 120;
    private const int TargetPixels = 340;

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly TextBlock _countdown = new();
    private int _remaining = LifetimeSeconds;

    public ShareQrWindow(Window owner, ShareQrResult qr)
    {
        if (qr.Modules is null) throw new ArgumentException("There is no QR to show.", nameof(qr));

        Owner = owner;
        Title = Strings.QrWindowTitle;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;
        ShowInTaskbar = false;
        SetResourceReference(BackgroundProperty, "AlduneGroundBrush");

        var panel = new StackPanel { Margin = new Thickness(24), Width = TargetPixels + 8 };
        panel.Children.Add(Text(Strings.QrWindowHint, 13, "AlduneTextStrongBrush", new Thickness(0, 0, 0, 12)));

        // Módulos de un número entero de píxeles y sin suavizado: con escalas fraccionarias aparecerían líneas entre módulos.
        int pixelsPerModule = Math.Max(2, TargetPixels / qr.Size);
        var image = new Image
        {
            Source = QrDrawing.ToImage(qr.Modules),
            Width = qr.Size * pixelsPerModule,
            Height = qr.Size * pixelsPerModule,
            Stretch = Stretch.Fill,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        RenderOptions.SetEdgeMode(image, EdgeMode.Aliased);
        panel.Children.Add(new Border { Background = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, Child = image });

        panel.Children.Add(Text(Strings.QrWindowWarning, 12, "AlduneHintBrush", new Thickness(0, 12, 0, 0)));
        _countdown.FontSize = 12;
        _countdown.Margin = new Thickness(0, 6, 0, 12);
        _countdown.SetResourceReference(TextBlock.ForegroundProperty, "AlduneHintBrush");
        UpdateCountdown();
        panel.Children.Add(_countdown);

        var close = new Button { Content = Strings.QrWindowClose, IsDefault = true, IsCancel = true, Padding = new Thickness(14, 6, 14, 6), HorizontalAlignment = HorizontalAlignment.Right };
        close.Click += (_, _) => Close();
        panel.Children.Add(close);
        Content = panel;

        _timer.Tick += (_, _) =>
        {
            _remaining--;
            if (_remaining <= 0) { Close(); return; }
            UpdateCountdown();
        };
        Loaded += (_, _) => _timer.Start();
        Closed += (_, _) => _timer.Stop();
    }

    private void UpdateCountdown() => _countdown.Text = Strings.QrWindowCountdown(_remaining);

    private static TextBlock Text(string text, double size, string brushKey, Thickness margin)
    {
        var block = new TextBlock { Text = text, FontSize = size, TextWrapping = TextWrapping.Wrap, Margin = margin };
        block.SetResourceReference(TextBlock.ForegroundProperty, brushKey);
        return block;
    }
}

/// <summary>La matriz de módulos como imagen vectorial: un cuadrado blanco y un rectángulo negro por módulo.</summary>
internal static class QrDrawing
{
    public static DrawingImage ToImage(bool[,] modules)
    {
        int size = modules.GetLength(0);
        var dark = new GeometryGroup();
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                if (modules[y, x]) dark.Children.Add(new RectangleGeometry(new System.Windows.Rect(x, y, 1, 1)));

        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing(Brushes.White, null, new RectangleGeometry(new System.Windows.Rect(0, 0, size, size))));
        group.Children.Add(new GeometryDrawing(Brushes.Black, null, dark));
        group.Freeze();
        var image = new DrawingImage(group);
        image.Freeze();
        return image;
    }
}
