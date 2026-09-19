using System.ComponentModel;
using System.Diagnostics;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Aldune.Core;
using Aldune.Interop;
using Aldune.Resources;

namespace Aldune.Windowing;

/// <summary>Un único sondeo por app, independiente de los globos de recordatorio de notas.</summary>
internal sealed class UpdateNotifier : IDisposable
{
    private readonly HttpClient _client = new() { Timeout = TimeSpan.FromSeconds(15) };
    private readonly ReleaseUpdateChecker _checker;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(20) };
    private readonly HashSet<Version> _notified = new();
    private UpdateAvailableWindow? _window;
    private bool _checking;
    private bool _manualRequested;
    private bool _disposed;

    internal UpdateNotifier()
    {
        _checker = new ReleaseUpdateChecker(_client);
        _timer.Tick += OnTick;
        _timer.Start();
    }

    private async void OnTick(object? sender, EventArgs e)
    {
        _timer.Interval = TimeSpan.FromHours(24);
        await CheckAsync(manual: false);
    }

    internal void CheckManually() => _ = CheckAsync(manual: true);

    private async Task CheckAsync(bool manual)
    {
        if (_disposed) return;
        if (manual)
        {
            _manualRequested = true;
            ShowMessage(Strings.UpdateChecking);
        }
        if (_checking) return;
        _checking = true;
        try
        {
            var update = await _checker.CheckAsync(AppInfo.Version, _shutdown.Token);
            if (_disposed) return;
            if (update is not null)
            {
                var firstNotice = _notified.Add(update.Version);
                if (firstNotice || _manualRequested)
                    ShowMessage(Strings.UpdateAvailableMessage(update.Version.ToString(update.Version.Revision == 0 ? 3 : 4)), update.DownloadUri);
            }
            else if (_manualRequested)
            {
                ShowMessage(Strings.UpdateCurrentMessage);
            }
        }
        catch (OperationCanceledException) when (_disposed)
        {
            // Salir no es un error y nunca debe crear otra ventana.
        }
        catch (Exception)
        {
            // Red caída, límite de GitHub o JSON inválido: en automático se reintenta mañana.
            if (!_disposed && _manualRequested) ShowMessage(Strings.UpdateErrorMessage);
        }
        finally
        {
            _checking = false;
            _manualRequested = false;
        }
    }

    private void ShowMessage(string message, Uri? downloadUri = null)
    {
        if (_window is null)
        {
            // La pantalla del origen (ver MonitorEnumerator.MonitorContainingCursor), no la primaria:
            // `SystemParameters.WorkArea` devolvía siempre la primaria, así que pedir la comprobación
            // desde un dock de la pantalla secundaria hacía salir el aviso en la otra.
            var window = new UpdateAvailableWindow(MonitorEnumerator.MonitorContainingCursor()?.WorkArea);
            _window = window;
            window.Closed += (_, _) => { if (_window == window) _window = null; };
            window.SetMessage(message, downloadUri);
            window.Show(); // Modeless y ShowActivated=false: no interrumpe la escritura de notas.
        }
        else
        {
            _window.SetMessage(message, downloadUri);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
        _timer.Tick -= OnTick;
        _shutdown.Cancel();
        _client.Dispose();
        _shutdown.Dispose();
        _window?.Close();
        _window = null;
    }
}


/// <summary>Aviso modeless; solo abre la página de la versión, nunca instala nada.</summary>
internal sealed class UpdateAvailableWindow : Window
{
    private readonly TextBlock _message = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 20) };
    private readonly Button _download = new() { Content = Strings.UpdateDownload, MinWidth = 90, Padding = new Thickness(12, 7, 12, 7) };
    private readonly Button _close = new() { MinWidth = 90, Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(12, 0, 0, 0) };
    private Uri? _downloadUri;

    internal UpdateAvailableWindow(WorkingArea? targetArea)
    {
        Title = Strings.UpdateWindowTitle;
        Width = 380;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        ShowActivated = false;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Background = (Brush)FindResource("AlduneGroundBrush");
        Foreground = (Brush)FindResource("AlduneTextBrush");
        // Sin monitor de origen (no se pudo leer el cursor) se mantiene el rincón de siempre.
        var area = targetArea ?? new WorkingArea(
            SystemParameters.WorkArea.Left,
            SystemParameters.WorkArea.Top,
            SystemParameters.WorkArea.Width,
            SystemParameters.WorkArea.Height);
        PlaceInCorner(area);
        SizeChanged += (_, _) => PlaceInCorner(area);
        var panel = new StackPanel { Margin = new Thickness(22) };
        panel.Children.Add(new TextBlock
        {
            Text = Strings.AppName,
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 12)
        });
        panel.Children.Add(_message);
        _download.Foreground = _close.Foreground = Background;
        _download.Background = _close.Background = Foreground;
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(_download);
        buttons.Children.Add(_close);
        panel.Children.Add(buttons);
        Content = panel;
        _close.Click += (_, _) => Close();
        _download.Click += OnDownload;
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key != System.Windows.Input.Key.Escape) return;
            Close();
            e.Handled = true;
        };
    }

    /// <summary>
    /// Rincón inferior derecho del monitor dado, con el mismo aire que deja el resto de avisos. El
    /// alto real solo se conoce tras la primera medida (la ventana es <c>SizeToContent</c>), así que
    /// se llama también desde <c>SizeChanged</c>.
    /// </summary>
    private void PlaceInCorner(WorkingArea area)
    {
        double height = ActualHeight > 0 ? ActualHeight : 230;
        Left = Math.Max(area.X, area.X + area.Width - Width - 20);
        Top = Math.Max(area.Y, area.Y + area.Height - height - 20);
    }

    internal void SetMessage(string message, Uri? downloadUri)
    {
        _message.Text = message;
        _downloadUri = downloadUri;
        _download.Visibility = downloadUri is null ? Visibility.Collapsed : Visibility.Visible;
        _close.Content = downloadUri is null ? Strings.UpdateClose : Strings.UpdateLater;
    }

    private void OnDownload(object sender, RoutedEventArgs e)
    {
        if (_downloadUri is null) return;
        try
        {
            Process.Start(new ProcessStartInfo(_downloadUri.AbsoluteUri) { UseShellExecute = true });
            Close();
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            _message.Text = Strings.UpdateOpenErrorMessage;
        }
    }
}
