namespace Fanote.Core;

public sealed class AppSettings
{
    public byte[]? WrappedDatabaseKey { get; set; }

    /// <summary>
    /// Si el atajo global de crear nota debe registrarse al arrancar.
    ///
    /// Por defecto <c>true</c>, y eso importa para los ficheros de ajustes que ya existen: un
    /// settings.json escrito antes de que este campo existiera no lo trae, System.Text.Json deja
    /// entonces el valor por defecto de la propiedad, y sale activado — que es lo que se quiere.
    /// Si el defecto fuera false, actualizar la app apagaria el atajo en silencio.
    /// </summary>
    public bool GlobalHotkeyEnabled { get; set; } = true;

    /// <summary>
    /// Modificadores y tecla del atajo global, en valores Win32. Nulos significa "el de fabrica":
    /// asi un settings.json anterior a este campo sigue funcionando, y ademas cambiar el atajo por
    /// defecto en una version futura llega a quien nunca lo toco, sin pisar a quien si lo hizo.
    /// </summary>
    public uint? HotkeyModifiers { get; set; }
    public uint? HotkeyKey { get; set; }

    /// <summary>La combinacion efectiva, resolviendo los nulos al valor de fabrica.</summary>
    public HotkeyBinding Hotkey =>
        HotkeyModifiers is { } mods && HotkeyKey is { } key && new HotkeyBinding(mods, key).IsValid
            ? new HotkeyBinding(mods, key)
            : HotkeyBinding.Default;
}
