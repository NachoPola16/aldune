namespace Aldune.Core.Tests;

/// <summary>
/// Guards the cause of the random "Cannot access a disposed object: SQLitePCL.sqlite3" failures: <c>SqliteConnection.ClearAllPools()</c> cuts
/// the connections other threads are using (parallel test classes, or the running app), so the Core code and its tests clear only the pool of
/// the database they own.
/// </summary>
public class NoGlobalPoolClearingTests
{
    [Fact]
    public void NothingCallsClearAllPools()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Aldune.slnx"))) root = root.Parent;
        Assert.NotNull(root);

        var offenders = new List<string>();
        foreach (var folder in new[] { Path.Combine("src", "Aldune.Core"), Path.Combine("tests", "Aldune.Core.Tests") })
            foreach (var file in Directory.EnumerateFiles(Path.Combine(root!.FullName, folder), "*.cs", SearchOption.AllDirectories))
            {
                if (Path.GetFileName(file) == nameof(NoGlobalPoolClearingTests) + ".cs") continue;   // this file spells the name it looks for
                if (file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) || file.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)) continue;
                int number = 0;
                foreach (var line in File.ReadLines(file))
                {
                    number++;
                    if (line.TrimStart().StartsWith("//") || !line.Contains("ClearAllPools(")) continue;
                    offenders.Add(Path.GetRelativePath(root.FullName, file) + ":" + number);
                }
            }
        Assert.True(offenders.Count == 0, "ClearAllPools() found (use TestPools or ClearPool on the owned database): " + string.Join(", ", offenders));
    }
}
