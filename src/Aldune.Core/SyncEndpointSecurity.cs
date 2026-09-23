using System.Net;
using System.Net.Sockets;

namespace Aldune.Core;

/// <summary>
/// ¿Viajaría el token del servidor (o la contraseña WebDAV) en claro por Internet? El contenido de
/// las notas va cifrado de extremo a extremo igualmente, pero con <c>http://</c> hacia un servidor
/// público cualquiera en el camino ve el token y puede leer, borrar o llenar el almacén. Dentro de la
/// red local (o de una VPN tipo Tailscale) <c>http://</c> es razonable, y es como se prueba.
/// </summary>
public static class SyncEndpointSecurity
{
    public static bool ExposesCredentials(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme != Uri.UriSchemeHttp) return false;
        return !IsLocalHost(uri);
    }

    /// <summary>Dominios reservados para redes privadas: no se resuelven en Internet.</summary>
    private static readonly string[] LocalOnlySuffixes = [".local", ".lan", ".home.arpa", ".internal"];

    private static bool IsLocalHost(Uri uri)
    {
        if (uri.IsLoopback) return true;

        // Primero como dirección IP: una IPv6 no lleva puntos y la regla de los nombres la tomaría
        // por un nombre de la red local.
        if (IPAddress.TryParse(uri.IdnHost.Trim('[', ']'), out var address))
            return IsPrivate(address);

        var host = uri.IdnHost;
        // Un nombre sin puntos ("nas") o de un dominio privado ("nas.local") solo existe en la red.
        return !host.Contains('.')
            || LocalOnlySuffixes.Any(suffix => host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsPrivate(IPAddress address)
    {
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
            return address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6UniqueLocal;

        var bytes = address.GetAddressBytes();
        return bytes[0] == 10
            || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
            || (bytes[0] == 192 && bytes[1] == 168)
            || (bytes[0] == 169 && bytes[1] == 254)
            || (bytes[0] == 100 && bytes[1] >= 64 && bytes[1] <= 127); // CGNAT: Tailscale y similares
    }
}
