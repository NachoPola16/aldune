using System.Net;

namespace Aldune.Core;

/// <summary>
/// Whether an address sends credentials over plain http outside the home network (same rule as <c>isCleartextOutsideLan</c> on Android).
/// A LAN host, a *.local name, a single-label name and localhost are fine; a public name or address is not.
/// </summary>
public static class CleartextUrl
{
    public static bool IsOutsideLan(string? url)
    {
        if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri) || !uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase)) return false;
        var host = uri.Host.Trim('[', ']').ToLowerInvariant();
        if (host.Length == 0) return false;
        if (host == "localhost" || host == "::1" || host.EndsWith(".local", StringComparison.Ordinal) || (!host.Contains('.') && !host.Contains(':'))) return false;
        var parts = host.Split('.');
        if (parts.Length != 4 || !parts.All(part => byte.TryParse(part, out _))) return true;
        var a = byte.Parse(parts[0]);
        var b = byte.Parse(parts[1]);
        return !(a == 10 || a == 127 || (a == 192 && b == 168) || (a == 172 && b is >= 16 and <= 31) || (a == 169 && b == 254));
    }
}
