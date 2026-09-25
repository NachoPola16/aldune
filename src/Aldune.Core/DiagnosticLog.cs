using System.Globalization;

namespace Aldune.Core;

/// <summary>
/// Registro de diagnóstico local y acotado: una línea con fecha por suceso, en un fichero que al
/// llenarse pasa a <c>.old</c> (solo se guarda uno), así que nunca ocupa más de dos veces
/// <c>maxBytes</c>. Sirve para fallos que no se reproducen a voluntad (ver docs/DOCK_DIAGNOSTICS.md):
/// lo que se escribe son estados de ventanas y pantallas, nunca contenido de notas.
///
/// Escribir nunca lanza: un diagnóstico que tumbara la app sería peor que el fallo que persigue.
/// </summary>
public sealed class DiagnosticLog
{
    private readonly string _path;
    private readonly long _maxBytes;
    private readonly Func<DateTimeOffset> _clock;
    private readonly object _gate = new();

    public DiagnosticLog(string path, long maxBytes = 256 * 1024, Func<DateTimeOffset>? clock = null)
    {
        _path = path;
        _maxBytes = maxBytes;
        _clock = clock ?? (() => DateTimeOffset.Now);
    }

    public string Path => _path;

    public void Write(string category, string message)
    {
        string line = _clock().ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)
            + " [" + category + "] "
            + message.Replace('\r', ' ').Replace('\n', ' ')
            + Environment.NewLine;

        lock (_gate)
        {
            try
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_path)!);
                var info = new FileInfo(_path);
                if (info.Exists && info.Length >= _maxBytes)
                    File.Move(_path, _path + ".old", overwrite: true);
                File.AppendAllText(_path, line);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
