namespace Aldune.Core;

/// <summary>
/// Copies a stream with a real ceiling on the bytes written. A zip entry carries its own idea of its size and a damaged or hostile file can
/// lie about it, so what an entry is allowed to write is counted while it is written, not read from its header.
/// </summary>
internal static class BoundedCopy
{
    /// <exception cref="InvalidDataException">The source has more than <paramref name="maxBytes"/> bytes.</exception>
    public static void To(Stream from, Stream to, long maxBytes)
    {
        var buffer = new byte[81_920];
        long total = 0;
        int read;
        while ((read = from.Read(buffer, 0, buffer.Length)) > 0)
        {
            total += read;
            if (total > maxBytes) throw new InvalidDataException("The data is larger than the limit.");
            to.Write(buffer, 0, read);
        }
    }

    public static void ToFile(Stream from, string path, long maxBytes)
    {
        using var target = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        To(from, target, maxBytes);
    }
}
