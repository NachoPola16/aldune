using Xunit;

namespace Aldune.Core.Tests;

public class OpenRequestInboxTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"aldune-inbox-{Guid.NewGuid():N}");

    public void Dispose() { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }

    [Fact]
    public void PostedPaths_AreDrainedOnceInOrder()
    {
        OpenRequestInbox.Post(_dir, [@"C:\a.md", @"Z:\apuntes\b.txt"]);
        OpenRequestInbox.Post(_dir, [@"C:\c.md"]);

        Assert.Equal(new[] { @"C:\a.md", @"Z:\apuntes\b.txt", @"C:\c.md" }, OpenRequestInbox.Drain(_dir));
        Assert.Empty(OpenRequestInbox.Drain(_dir));
    }

    [Fact]
    public void DrainingAMissingInbox_IsEmpty() => Assert.Empty(OpenRequestInbox.Drain(_dir));

    [Fact]
    public void BlankLines_AreIgnored()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "x.txt"), "\r\nC:\\a.md\r\n\r\n");
        Assert.Equal(new[] { @"C:\a.md" }, OpenRequestInbox.Drain(_dir));
    }
}
