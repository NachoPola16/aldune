namespace Aldune.Core;

/// <summary>
/// The font of the note text (a local setting: it never travels with the sync and the sync format does not change). Stored as a number in
/// <c>settings.json</c>: do not reorder. Spec: <c>docs/specs/2026-11-04-note-font-choice-design.md</c>.
/// </summary>
public enum NoteFontChoice
{
    /// <summary>The system font, as always.</summary>
    System = 0,
    Serif = 1,
    /// <summary>Atkinson Hyperlegible, bundled with the app (SIL OFL).</summary>
    EasyToRead = 2,
    Monospace = 3,
    /// <summary>Windows only: a family installed on the PC, named in <see cref="AppSettings.NoteFontName"/>.</summary>
    Installed = 4,
    /// <summary>A .ttf/.otf file the person imported, kept in the app's own data folder (<see cref="AppSettings.NoteFontFile"/>).</summary>
    Imported = 5
}

public static class NoteFonts
{
    /// <summary>An unknown value (from a future version) loads as System, like every other enum in the settings.</summary>
    public static NoteFontChoice Normalize(NoteFontChoice value) => Enum.IsDefined(value) ? value : NoteFontChoice.System;

    /// <summary>
    /// The family name to hand to WPF for a choice; empty means the system font. An imported font is never resolved by name: it is loaded
    /// from its file, so a family name that happens to be installed cannot stand in for it.
    /// </summary>
    public static string WindowsFamily(NoteFontChoice choice, string? name) => Normalize(choice) switch
    {
        NoteFontChoice.Serif => "Georgia",
        NoteFontChoice.EasyToRead => "Atkinson Hyperlegible",
        NoteFontChoice.Monospace => "Consolas",
        NoteFontChoice.Installed => name?.Trim() ?? "",
        _ => ""
    };

    /// <summary>The looks that define their own font (terminal, XP, notebooks) keep it: the setting only applies where the look uses the system font.</summary>
    public static bool LookKeepsItsFont(string? skinNoteFont) => !string.IsNullOrEmpty(skinNoteFont);
}

public enum FontFileProblem { None = 0, Extension, TooLarge, NotAFont, NoName }

/// <summary>The outcome of <see cref="FontFileInspector.Inspect"/>: the family name when the file passes, the reason when it does not.</summary>
public readonly record struct FontFileInfo(bool IsValid, string? Family, FontFileProblem Problem);

/// <summary>
/// The check of an imported font file before it is copied into the data folder: extension, size, a real sfnt header and a readable family
/// name. It is not a full parser (the platform loader does the rest and a file that fails to load falls back to the system font): its job
/// is to refuse what is plainly not a font, and to read the name to list it by. Every offset is bounds-checked against the bytes.
/// </summary>
public static class FontFileInspector
{
    public const int MaxBytes = 10 * 1024 * 1024;
    private const int MaxNameLength = 100;

    public static FontFileInfo Inspect(byte[] data, string fileName)
    {
        var extension = Path.GetExtension(fileName);
        if (!extension.Equals(".ttf", StringComparison.OrdinalIgnoreCase) && !extension.Equals(".otf", StringComparison.OrdinalIgnoreCase))
            return Fail(FontFileProblem.Extension);
        if (data.Length > MaxBytes) return Fail(FontFileProblem.TooLarge);
        if (data.Length < 12) return Fail(FontFileProblem.NotAFont);

        // 00 01 00 00 (TrueType), 'OTTO' (CFF) and 'true' (old Apple). A collection ('ttcf') holds many fonts and is not accepted.
        uint version = U32(data, 0);
        if (version != 0x00010000 && version != 0x4F54544F && version != 0x74727565) return Fail(FontFileProblem.NotAFont);

        int tables = U16(data, 4);
        if (tables is 0 or > 256 || 12 + 16L * tables > data.Length) return Fail(FontFileProblem.NotAFont);

        long nameOffset = -1, nameLength = 0;
        for (int i = 0; i < tables; i++)
        {
            int record = 12 + 16 * i;
            if (data[record] == 'n' && data[record + 1] == 'a' && data[record + 2] == 'm' && data[record + 3] == 'e')
            {
                nameOffset = U32(data, record + 8);
                nameLength = U32(data, record + 12);
                break;
            }
        }
        if (nameOffset < 0) return Fail(FontFileProblem.NoName);
        if (nameOffset + nameLength > data.Length || nameLength < 6) return Fail(FontFileProblem.NotAFont);

        int table = (int)nameOffset;
        int count = U16(data, table + 2);
        long stringBase = (long)table + U16(data, table + 4);
        if (6L + 12 * count > nameLength) return Fail(FontFileProblem.NotAFont);

        // The typographic family (16) is the one people know; the legacy family (1) carries the weight in some files («Roboto Light»).
        foreach (var (platform, nameId) in new[] { (3, 16), (3, 1), (1, 16), (1, 1) })
        {
            for (int i = 0; i < count; i++)
            {
                int record = table + 6 + 12 * i;
                if (U16(data, record) != platform || U16(data, record + 6) != nameId) continue;
                int length = U16(data, record + 8);
                long start = stringBase + U16(data, record + 10);
                if (start + length > data.Length || start + length > nameOffset + nameLength) continue;
                var text = platform == 3
                    ? System.Text.Encoding.BigEndianUnicode.GetString(data, (int)start, length)
                    : System.Text.Encoding.Latin1.GetString(data, (int)start, length);
                text = text.Trim();
                if (text.Length is 0 or > MaxNameLength || text.Any(char.IsControl)) continue;
                return new FontFileInfo(true, text, FontFileProblem.None);
            }
        }
        return Fail(FontFileProblem.NoName);
    }

    private static FontFileInfo Fail(FontFileProblem problem) => new(false, null, problem);
    private static int U16(byte[] d, int i) => (d[i] << 8) | d[i + 1];
    private static uint U32(byte[] d, int i) => ((uint)d[i] << 24) | ((uint)d[i + 1] << 16) | ((uint)d[i + 2] << 8) | d[i + 3];
}
