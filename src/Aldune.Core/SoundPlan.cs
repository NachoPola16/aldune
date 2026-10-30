namespace Aldune.Core;

/// <summary>Momentos de la app que pueden sonar. Cada uno lleva su propia elección (ver <see cref="SoundPlan"/>).</summary>
public enum SoundEvent
{
    Reminder,
    SyncDone,
    SyncFailed,
    NoteCreated,
    NoteArchived,
    NoteOpened,
    NoteClosed,
    TaskChecked,
    NoteDeleted,
    Typing,
    TextDeleted,
}

/// <summary>Qué suena. Del 1 al 5, sonidos del sistema (siguen el volumen y el tema de Windows); <see cref="File"/>
/// es un .wav que elige el usuario; del 7 al 12, los sonidos de papel y pluma de la app. Se guarda como número:
/// las opciones nuevas van siempre al final.</summary>
public enum SoundChoice
{
    None = 0,
    Asterisk = 1,
    Exclamation = 2,
    Beep = 3,
    Hand = 4,
    Question = 5,
    File = 6,
    PageTurn = 7,
    PaperFold = 8,
    PenTap = 9,
    PaperCrumple = 10,
    PenScratch = 11,
    Eraser = 12,
}

/// <summary>La elección de un evento. Se guarda en <c>AppSettings.Sounds</c>; la ruta del .wav es de este equipo.</summary>
public sealed class SoundSetting
{
    public SoundChoice Choice { get; set; }
    public string? FilePath { get; set; }
}

public enum SoundActionKind { None, System, File, Bundled }

/// <summary>Lo que hay que hacer sonar, ya validado: la capa WPF solo lo reproduce.</summary>
public sealed record SoundAction(SoundActionKind Kind, SoundChoice System = SoundChoice.None, string? Path = null)
{
    public static SoundAction Silence { get; } = new(SoundActionKind.None);
}

/// <summary>
/// Decide qué suena para cada evento (spec en el roadmap, §10). Apagado de fábrica: una app que vive en el borde
/// de la pantalla no suena sin pedirlo. Con el interruptor general encendido, solo las dos alertas (recordatorio
/// y fallo de sincronización) traen sonido de partida; el resto se activa evento a evento. Un .wav que falta, que
/// no lo es o que es enorme se queda en silencio, nunca en error.
/// </summary>
public static class SoundPlan
{
    /// <summary>Tope de un .wav propio: un aviso son unos segundos, y un archivo grande bloquearía al cargarlo.</summary>
    public const int MaxFileBytes = 5 * 1024 * 1024;

    public static IReadOnlyList<SoundEvent> All { get; } =
        [SoundEvent.Reminder, SoundEvent.SyncDone, SoundEvent.SyncFailed, SoundEvent.NoteCreated, SoundEvent.NoteArchived,
         SoundEvent.NoteOpened, SoundEvent.NoteClosed, SoundEvent.TaskChecked, SoundEvent.NoteDeleted, SoundEvent.Typing, SoundEvent.TextDeleted];

    /// <summary>Clave estable de cada evento en el archivo de ajustes: no se renombra aunque cambie el enum.</summary>
    public static string Id(SoundEvent soundEvent) => soundEvent switch
    {
        SoundEvent.Reminder => "reminder",
        SoundEvent.SyncDone => "syncDone",
        SoundEvent.SyncFailed => "syncFailed",
        SoundEvent.NoteCreated => "noteCreated",
        SoundEvent.NoteArchived => "noteArchived",
        SoundEvent.NoteOpened => "noteOpened",
        SoundEvent.NoteClosed => "noteClosed",
        SoundEvent.TaskChecked => "taskChecked",
        SoundEvent.NoteDeleted => "noteDeleted",
        SoundEvent.Typing => "typing",
        SoundEvent.TextDeleted => "textDeleted",
        _ => throw new ArgumentOutOfRangeException(nameof(soundEvent)),
    };

    public static SoundChoice DefaultChoice(SoundEvent soundEvent) => soundEvent switch
    {
        SoundEvent.Reminder => SoundChoice.Exclamation,
        SoundEvent.SyncFailed => SoundChoice.Hand,
        _ => SoundChoice.None,
    };

    /// <summary>La elección efectiva de un evento: la guardada o, si no hay, la de partida.</summary>
    public static SoundSetting SettingFor(AppSettings settings, SoundEvent soundEvent) =>
        settings.Sounds.TryGetValue(Id(soundEvent), out var saved) && saved is not null
            ? saved
            : new SoundSetting { Choice = DefaultChoice(soundEvent) };

    public static SoundAction Resolve(AppSettings settings, SoundEvent soundEvent)
    {
        if (!settings.SoundsEnabled) return SoundAction.Silence;

        var setting = SettingFor(settings, soundEvent);
        return setting.Choice switch
        {
            SoundChoice.None => SoundAction.Silence,
            SoundChoice.File => IsUsableFile(setting.FilePath) ? new SoundAction(SoundActionKind.File, Path: setting.FilePath) : SoundAction.Silence,
            var own when own >= SoundChoice.PageTurn && Enum.IsDefined(own) => new SoundAction(SoundActionKind.Bundled, own),
            var system when Enum.IsDefined(system) => new SoundAction(SoundActionKind.System, system),
            _ => SoundAction.Silence,   // un número que no es de ningún sonido (archivo de otra versión)
        };
    }

    /// <summary>Los sonidos sugeridos. Solo los eventos de las notas y solo los que se oyen de vez en cuando: escribir y
    /// borrar texto suenan en cada pulsación y se activan a mano. Los avisos y la sync siguen con lo que el usuario tenga.</summary>
    public static IReadOnlyDictionary<SoundEvent, SoundChoice> PaperAndPen { get; } = new Dictionary<SoundEvent, SoundChoice>
    {
        [SoundEvent.NoteOpened] = SoundChoice.PageTurn,
        [SoundEvent.NoteClosed] = SoundChoice.PaperFold,
        [SoundEvent.TaskChecked] = SoundChoice.PenTap,
        [SoundEvent.NoteDeleted] = SoundChoice.PaperCrumple,
    };

    public static void ApplyPaperAndPen(AppSettings settings)
    {
        foreach (var (soundEvent, choice) in PaperAndPen)
            settings.Sounds[Id(soundEvent)] = new SoundSetting { Choice = choice };
    }

    /// <summary>Al encender los sonidos por primera vez no hay nada elegido para las notas: se aplican los sugeridos, para que
    /// encender el interruptor ya suene a algo. Si el usuario ya eligió algo para esos eventos, no se toca nada.</summary>
    public static bool ApplyPaperAndPenIfUnset(AppSettings settings)
    {
        if (PaperAndPen.Keys.Any(e => settings.Sounds.ContainsKey(Id(e)))) return false;
        ApplyPaperAndPen(settings);
        return true;
    }

    /// <summary>Volumen de la app (0-100 en los ajustes) como factor de 0 a 1. Afecta a los sonidos propios de la app;
    /// los del sistema y los .wav del usuario siguen el volumen de Windows.</summary>
    public static double VolumeFactor(AppSettings settings) => Math.Clamp(settings.SoundVolume, 0, 100) / 100.0;

    public static bool IsUsableFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        if (!string.Equals(Path.GetExtension(path), ".wav", StringComparison.OrdinalIgnoreCase)) return false;
        try
        {
            var info = new FileInfo(path);
            return info.Exists && info.Length <= MaxFileBytes;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return false;
        }
    }
}
