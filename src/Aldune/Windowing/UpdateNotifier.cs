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
    private readonly ToastCenter _toasts;
    private ToastWindow? _toast;
    private bool _checking;
    private bool _manualRequested;
    private bool _disposed;

    internal UpdateNotifier(ToastCenter toasts)
    {
        _toasts = toasts;
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
            ShowMessage(new ToastContent(Strings.UpdateWindowTitle, Strings.UpdateChecking));
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
                    ShowAvailable(update.Version.ToString(update.Version.Revision == 0 ? 3 : 4), update.DownloadUri);
            }
            else if (_manualRequested)
            {
                ShowMessage(new ToastContent(Strings.UpdateWindowTitle, Strings.UpdateCurrentMessage,
                    AutoDismiss: TimeSpan.FromSeconds(6)));
            }
        }
        catch (OperationCanceledException) when (_disposed)
        {
            // Salir no es un error y nunca debe crear otra ventana.
        }
        catch (Exception)
        {
            // Red caída, límite de GitHub o JSON inválido: en automático se reintenta mañana.
            if (!_disposed && _manualRequested)
                ShowMessage(new ToastContent(Strings.UpdateWindowTitle, Strings.UpdateErrorMessage,
                    AutoDismiss: TimeSpan.FromSeconds(10)));
        }
        finally
        {
            _checking = false;
            _manualRequested = false;
        }
    }

    /// <summary>
    /// "Hay una versión nueva": se queda hasta que se responde (Descargar o Más tarde). Solo abre la
    /// página de la versión, nunca instala nada.
    /// </summary>
    private void ShowAvailable(string version, Uri downloadUri)
    {
        ToastContent content = null!;
        content = new ToastContent(Strings.UpdateAvailableTitle, Strings.UpdateAvailableMessage(version),
        [
            new ToastAction(Strings.UpdateLater, () => { }),
            new ToastAction(Strings.UpdateDownload, () =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo(downloadUri.AbsoluteUri) { UseShellExecute = true });
                    _toast?.Close();
                }
                catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
                {
                    // El error se cuenta en el mismo aviso, con los botones intactos para reintentar.
                    _toast?.SetContent(content with { Message = Strings.UpdateOpenErrorMessage });
                }
            }, Primary: true, Closes: false),
        ]);
        ShowMessage(content);
    }

    /// <summary>
    /// Un único aviso de actualizaciones a la vez: "Buscando…" se convierte en el resultado en el
    /// mismo sitio, en vez de apilar uno nuevo.
    /// </summary>
    private void ShowMessage(ToastContent content)
    {
        if (_toast is null)
        {
            var toast = _toasts.Show(content);
            _toast = toast;
            toast.Closed += (_, _) => { if (_toast == toast) _toast = null; };
        }
        else
        {
            _toast.SetContent(content);
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
        _toast?.Close();
        _toast = null;
    }
}
