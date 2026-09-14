namespace Aldune.Core;

public static class DatabaseCorruptionGuard
{
    private static readonly byte[] SqliteMagicHeader = "SQLite format 3\0"u8.ToArray();

    public static bool IsValidSqliteFile(string path)
    {
        if (!File.Exists(path)) return false;

        using var stream = File.OpenRead(path);
        if (stream.Length < SqliteMagicHeader.Length) return false;

        var header = new byte[SqliteMagicHeader.Length];
        var bytesRead = stream.Read(header, 0, header.Length);
        return bytesRead == header.Length && header.SequenceEqual(SqliteMagicHeader);
    }

    public static void BackupAndRemove(string path)
    {
        var backupPath = $"{path}.corrupt-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
        File.Copy(path, backupPath);
        File.Delete(path);
    }
}
