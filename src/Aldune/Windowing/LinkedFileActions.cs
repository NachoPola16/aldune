using System.Windows;

namespace Aldune.Windowing;

/// <summary>
/// Lo que se puede hacer con el archivo de una nota vinculada, igual desde el menú de la nota que desde
/// el de su pestaña en el dock.
/// </summary>
internal static class LinkedFileActions
{
    public static void OpenInEditor(string path)
    {
        // La ruta sale de la base de datos y se validó al vincular; se vuelve a comprobar para no lanzar nunca un .exe o un .lnk.
        if (!Aldune.Core.LinkedFileFormat.IsSupportedExtension(path)) return;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Sin programa asociado a la extensión: Windows ya lo explica en su propio diálogo.
        }
    }

    public static void ShowInExplorer(string path) =>
        System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"");

    public static void CopyPath(string path)
    {
        try
        {
            Clipboard.SetText(path);
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            // Otro programa tiene el portapapeles en ese instante; no merece un aviso.
        }
    }
}
