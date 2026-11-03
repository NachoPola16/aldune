using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace Aldune.Windowing;

/// <summary>
/// Copies a secret (the sync key, an invitation code) to the clipboard the way password managers do: kept out of the Windows clipboard history
/// (Win+V) and out of the cloud clipboard, and cleared after a minute if it is still there. A plain <c>Clipboard.SetText</c> left the
/// end-to-end key in the history for anyone with access to the session.
/// </summary>
internal static class SecretClipboard
{
    private static readonly TimeSpan ClearAfter = TimeSpan.FromSeconds(60);
    private static DispatcherTimer? _timer;

    private static string? _held;

    /// <summary>On exit: the timer dies with the app, so a secret still on the clipboard is cleared now.</summary>
    public static void ClearIfStillHeld()
    {
        if (_held is null) return;
        try { if (Clipboard.ContainsText() && Clipboard.GetText() == _held) Clipboard.Clear(); }
        catch (System.Runtime.InteropServices.COMException) { }
        _held = null;
    }

    public static void SetText(string text)
    {
        _held = text;
        var data = new DataObject();
        data.SetText(text);
        // The formats Windows documents for this: a DWORD of 1 / 0 as a memory stream.
        data.SetData("ExcludeClipboardContentFromMonitorProcessing", new MemoryStream(BitConverter.GetBytes(1)));
        data.SetData("CanIncludeInClipboardHistory", new MemoryStream(BitConverter.GetBytes(0)));
        data.SetData("CanUploadToCloudClipboard", new MemoryStream(BitConverter.GetBytes(0)));
        Clipboard.SetDataObject(data, copy: true);

        _timer?.Stop();
        _timer = new DispatcherTimer { Interval = ClearAfter };
        _timer.Tick += (_, _) =>
        {
            _timer?.Stop();
            try
            {
                // Only if the clipboard still holds the secret: something else the person copied since is not touched.
                if (Clipboard.ContainsText() && Clipboard.GetText() == text) Clipboard.Clear();
                _held = null;
            }
            catch (System.Runtime.InteropServices.COMException) { } // the clipboard is busy: leave it
        };
        _timer.Start();
    }
}
