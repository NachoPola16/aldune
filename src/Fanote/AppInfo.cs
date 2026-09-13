using System.Reflection;
using Fanote.Resources;

namespace Fanote;

/// <summary>Identidad visible de la aplicación, tomada de la versión del ensamblado publicado.</summary>
public static class AppInfo
{
    public static string Version =>
        Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "0.5.0";

    public static string DisplayVersion => $"{Strings.AppName} v{Version}";
}
