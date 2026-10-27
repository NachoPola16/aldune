namespace Aldune.Core;

/// <summary>
/// Cuándo abrir una nota lanza una pasada de sync: la misma regla que en Android (volver a primer plano), para que en los dos lados
/// se vea lo escrito en otro dispositivo antes de empezar a editar. No bloquea la edición: sin red no se podría escribir, y los
/// conflictos ya se guardan.
/// </summary>
public static class NoteOpenSync
{
    public static readonly TimeSpan MinGap = TimeSpan.FromSeconds(30);

    public static bool ShouldRun(bool syncConfigured, DateTimeOffset? lastAttempt, DateTimeOffset now, bool running) =>
        syncConfigured && !running && (lastAttempt is null || now - lastAttempt.Value > MinGap);
}
