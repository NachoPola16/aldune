namespace Aldune.Core;

/// <summary>
/// El vínculo de una nota con un archivo del disco (spec, decisión 2). Es de este equipo: nunca viaja por
/// la sync, porque la ruta no significa nada en otro equipo. Las huellas dicen cómo estaban el archivo y la
/// nota la última vez que Aldune los puso de acuerdo; con ellas se sabe quién ha cambiado qué.
/// </summary>
public sealed record NoteFileLink(
    Guid NoteId,
    string Path,
    bool SyncEnabled,
    string? KnownHash,
    DateTimeOffset? KnownWriteTime,
    string? KnownTextHash);
