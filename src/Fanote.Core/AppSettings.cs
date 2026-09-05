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
}
