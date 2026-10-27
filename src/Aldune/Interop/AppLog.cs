using Aldune.Core;

namespace Aldune.Interop;

/// <summary>
/// Registro de errores inesperados (<c>logs\errores.log</c> junto a los datos): lo que una tarea en segundo plano (sync, recordatorios,
/// archivos vinculados, sonidos) captura para no cerrar la aplicación. Solo escribe cuando algo falla, y sin contenido de notas.
/// Sin <see cref="Log"/> (las sondas y los tests no lo configuran) no hace nada.
/// </summary>
internal static class AppLog
{
    internal static DiagnosticLog? Log { get; set; }

    internal static void Write(string category, string message) => Log?.Write(category, message);
}
