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
    private readonly Func<string, CancellationToken, Task<ReleaseUpdate?>> _check;
    private readonly Action<Uri> _open;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(20) };
    private readonly HashSet<Version> _notified = new();
    private readonly ToastCenter _toasts;
    private ToastWindow? _toast;
    private bool _checking;
    private readonly Func<bool> _automaticChecksEnabled;
    private bool _manualRequested;
    private bool _disposed;
    private bool _availableShown;

    /// <summary>Cada cuánto se vuelve a mirar mientras el aviso de una versión nueva sigue sin responder.</summary>
    internal static readonly TimeSpan PendingRecheck = TimeSpan.FromHours(1);

    /// <param name="check">Solo para pruebas: sustituye a la consulta a GitHub.</param>
    /// <param name="open">Solo para pruebas: sustituye a abrir el navegador.</param>
    internal UpdateNotifier(ToastCenter toasts, Func<bool> automaticChecksEnabled,
        Func<string, CancellationToken, Task<ReleaseUpdate?>>? check = null, Action<Uri>? open = null)
    {
        _toasts = toasts;
        _automaticChecksEnabled = automaticChecksEnabled;
        _check = check ?? ((version, token) => new ReleaseUpdateChecker(_client).CheckAsync(version, token));
        _open = open ?? (uri => Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true }));
        _timer.Tick += OnTick;
        _timer.Start();
    }

    private async void OnTick(object? sender, EventArgs e)
    {
        _timer.Interval = TimeSpan.FromHours(24);
        // Se consulta en cada vuelta y no al construir: apagarlo en Ajustes vale sin reiniciar.
        if (!_automaticChecksEnabled()) return;
        await CheckAsync(manual: false);
        // Con un aviso sin responder, otra versión que salga mientras tanto tiene que reemplazarlo pronto, no a las 24 h.
        if (_availableShown && !_disposed) _timer.Interval = PendingRecheck;
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
            var update = await _check(AppInfo.Version, _shutdown.Token);
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
            new ToastAction(Strings.UpdateDownload, () => _ = OpenLatestAsync(downloadUri, content),
                Primary: true, Closes: false),
        ]);
        _availableShown = true;
        ShowMessage(content);
    }

    /// <summary>
    /// «Descargar»: antes de abrir la página se vuelve a mirar cuál es la última versión (con poco margen), porque puede
    /// haber salido otra desde que se enseñó el aviso; sin red o con error se abre la que enseñaba.
    /// </summary>
    private async Task OpenLatestAsync(Uri shown, ToastContent content)
    {
        var target = shown;
        try
        {
            using var limit = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token);
            limit.CancelAfter(TimeSpan.FromSeconds(5));
            if (await _check(AppInfo.Version, limit.Token) is { } latest)
            {
                _notified.Add(latest.Version);
                target = latest.DownloadUri;
            }
        }
        catch (Exception)
        {
            // Sin red o GitHub no responde: vale la página que enseñaba el aviso.
        }
        if (_disposed) return;
        try
        {
            _open(target);
            _toast?.Close();
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            // El error se cuenta en el mismo aviso, con los botones intactos para reintentar.
            _toast?.SetContent(content with { Message = Strings.UpdateOpenErrorMessage });
        }
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
            toast.Closed += (_, _) => { if (_toast == toast) { _toast = null; _availableShown = false; } };
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
