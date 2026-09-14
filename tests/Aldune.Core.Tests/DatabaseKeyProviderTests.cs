using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public class DatabaseKeyProviderTests
{
    [Fact]
    public void GenerateKey_Produces32Bytes()
    {
        var key = DatabaseKeyProvider.GenerateKey();
        Assert.Equal(32, key.Length);
    }

    [Fact]
    public void GenerateKey_ProducesDifferentKeysEachCall()
    {
        var first = DatabaseKeyProvider.GenerateKey();
        var second = DatabaseKeyProvider.GenerateKey();
        Assert.NotEqual(Convert.ToBase64String(first), Convert.ToBase64String(second));
    }

    [Fact]
    public void WrapThenUnwrap_RoundTrips()
    {
        var rawKey = DatabaseKeyProvider.GenerateKey();
        var wrapped = DatabaseKeyProvider.Wrap(rawKey);
        var unwrapped = DatabaseKeyProvider.Unwrap(wrapped);
        Assert.Equal(Convert.ToBase64String(rawKey), Convert.ToBase64String(unwrapped));
    }

    [Fact]
    public void Wrap_ProducesDifferentBytesThanRawKey()
    {
        var rawKey = DatabaseKeyProvider.GenerateKey();
        var wrapped = DatabaseKeyProvider.Wrap(rawKey);
        Assert.NotEqual(Convert.ToBase64String(rawKey), Convert.ToBase64String(wrapped));
    }
}
