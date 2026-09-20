namespace Aldune.Core;

/// <summary>
/// La combinación de teclas del atajo global, guardable y con nombre legible.
///
/// Vive en Core y no en la capa de ventanas porque es un ajuste que se persiste, y porque montar
/// el nombre que se enseña ("Ctrl + Alt + N") es lógica pura que conviene tener bajo test: la
/// interfaz no debe poder anunciar una combinación distinta de la que de verdad se registra.
///
/// Los valores son los de Win32 (<c>MOD_*</c> y códigos de tecla virtual), no los de WPF, porque
/// quien los consume es <c>RegisterHotKey</c>. Traducir en la frontera evita guardar en disco un
/// enum de WPF que luego habría que mapear en cada lectura.
/// </summary>
public sealed record HotkeyBinding(uint Modifiers, uint Key)
{
    public const uint ModAlt = 0x0001;
    public const uint ModControl = 0x0002;
    public const uint ModShift = 0x0004;
    public const uint ModWin = 0x0008;

    /// <summary>
    /// Ctrl + Shift + N. Se eligió sobre Ctrl+Alt+N porque esa segunda es una combinación muy
    /// disputada: en muchos teclados y utilidades de fabricante está tomada por controles de
    /// multimedia, y Windows no comparte atajos — se lo queda el primero que lo pide.
    /// </summary>
    public static HotkeyBinding Default => new(ModControl | ModShift, 0x4E);

    /// <summary>
    /// Ctrl + Alt + H: ocultar o devolver el dock. Tiene que ser un atajo y no solo una fila de la
    /// bandeja porque el caso que lo pide es un vídeo a pantalla completa, donde la bandeja tampoco
    /// está a la vista. Ctrl+Alt+H (H de hide) no lo usa Windows para nada y es fácil de recordar;
    /// se descartan las combinaciones con Win para no pisar atajos del sistema.
    /// </summary>
    public static HotkeyBinding DockToggleDefault => new(ModControl | ModAlt, 0x48);

    /// <summary>
    /// Si la combinación es registrable. Sin al menos un modificador, un atajo global se tragaría
    /// esa tecla en todo el sistema, que es justo lo que no debe hacer una app de notas.
    /// </summary>
    public bool IsValid => Modifiers != 0 && Key != 0;

    /// <summary>
    /// El nombre que se enseña. El orden es el convencional de Windows (Ctrl, Alt, Shift, Win) y
    /// no el de los bits, para que se lea como lo escribiría cualquier documentación.
    /// </summary>
    public string DisplayName
    {
        get
        {
            if (!IsValid) return "sin asignar";

            var parts = new List<string>(4);
            if ((Modifiers & ModControl) != 0) parts.Add("Ctrl");
            if ((Modifiers & ModAlt) != 0) parts.Add("Alt");
            if ((Modifiers & ModShift) != 0) parts.Add("Shift");
            if ((Modifiers & ModWin) != 0) parts.Add("Win");
            parts.Add(KeyName(Key));
            return string.Join(" + ", parts);
        }
    }

    private static string KeyName(uint key) => key switch
    {
        >= 0x30 and <= 0x39 => ((char)key).ToString(),               // 0-9
        >= 0x41 and <= 0x5A => ((char)key).ToString(),               // A-Z
        >= 0x70 and <= 0x87 => "F" + (key - 0x6F),                   // F1-F24
        0x20 => "Espacio",
        0x2D => "Insert",
        0x2E => "Supr",
        0xBD => "-",
        0xBB => "+",
        // Cualquier otra: se enseña el código en vez de mentir con un nombre inventado.
        _ => $"0x{key:X2}"
    };
}
