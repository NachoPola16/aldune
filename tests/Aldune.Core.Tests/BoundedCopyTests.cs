namespace Aldune.Core.Tests;

public class BoundedCopyTests
{
    [Fact]
    public void CopiesWhatFits()
    {
        using var from = new MemoryStream(new byte[1000]);
        using var to = new MemoryStream();
        BoundedCopy.To(from, to, 1000);
        Assert.Equal(1000, to.Length);
    }

    [Fact]
    public void StopsAtTheLimitEvenWhenTheSourceKeepsGoing()
    {
        using var endless = new EndlessStream();
        using var to = new MemoryStream();
        Assert.Throws<InvalidDataException>(() => BoundedCopy.To(endless, to, 10_000));
        Assert.True(to.Length <= 10_000 + 81_920, "wrote " + to.Length);   // at most one buffer past the limit
    }

    private sealed class EndlessStream : Stream
    {
        public override int Read(byte[] buffer, int offset, int count) { Array.Clear(buffer, offset, count); return count; }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
