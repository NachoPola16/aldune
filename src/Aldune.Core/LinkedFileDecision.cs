namespace Aldune.Core;

public enum LinkedFileAction { None, Write, Reload, Conflict, Unavailable }

/// <summary>
/// La tabla de la decisión 4 de la spec. Lo que manda es la huella: si el disco tiene la que Aldune
/// conoce, nadie lo ha tocado y se puede escribir; si no, el archivo es del otro programa y lo de Aldune,
/// si había algo pendiente, va a una nota aparte. Nunca se escribe sobre una huella desconocida.
/// </summary>
public static class LinkedFileDecision
{
    public static LinkedFileAction Decide(string? knownFileHash, string? currentFileHash, bool hasPendingChanges)
    {
        if (currentFileHash is null) return LinkedFileAction.Unavailable;
        if (knownFileHash is not null && currentFileHash == knownFileHash)
            return hasPendingChanges ? LinkedFileAction.Write : LinkedFileAction.None;
        return hasPendingChanges ? LinkedFileAction.Conflict : LinkedFileAction.Reload;
    }
}
