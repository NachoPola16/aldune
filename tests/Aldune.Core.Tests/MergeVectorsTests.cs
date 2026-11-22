using System.Text.Json.Nodes;

namespace Aldune.Core.Tests;

/// <summary>
/// Shared vectors of the line merge (<c>tests/vectors/merge-vectors.json</c>): base, local, remote → merged text or conflict.
/// They are the contract of the Kotlin port in <c>android/sync</c>; the file is written by hand, so a change to the merge rules
/// shows up here before it reaches a client.
/// </summary>
public sealed class MergeVectorsTests
{
    private static string VectorsPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Aldune.slnx"))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("Aldune.slnx not found"), "tests", "vectors", "merge-vectors.json");
    }

    [Fact]
    public void LineMerge_MatchesTheSharedVectors()
    {
        var cases = JsonNode.Parse(File.ReadAllText(VectorsPath()))!["cases"]!.AsArray();
        Assert.True(cases.Count >= 16, $"Missing cases in merge-vectors.json: {cases.Count}.");
        foreach (var c in cases)
        {
            var name = (string)c!["name"]!;
            var result = LineMerge.Merge((string)c["base"]!, (string)c["local"]!, (string)c["remote"]!);
            Assert.True((bool)c["conflict"]! == result.Conflict, $"{name}: conflict");
            if (!result.Conflict) Assert.True((string)c["expectedText"]! == result.Text, $"{name}: expected [{c["expectedText"]}] but got [{result.Text}]");
        }
    }
}
