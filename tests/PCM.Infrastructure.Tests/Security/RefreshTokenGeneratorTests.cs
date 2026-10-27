using PCM.Infrastructure.Security;

namespace PCM.Infrastructure.Tests.Security;

public sealed class RefreshTokenGeneratorTests
{
    [Fact]
    public void Create_returns_a_url_safe_token_and_its_64_char_hex_hash()
    {
        var (raw, hash) = RefreshTokenGenerator.Create();

        Assert.Equal(86, raw.Length);                                  // 64 bytes in Base64Url, no padding
        Assert.DoesNotContain(raw, c => c is '+' or '/' or '=');
        Assert.Equal(64, hash.Length);                                 // fits RefreshToken.TokenHash char(64)
        Assert.Matches("^[0-9a-f]{64}$", hash);
        Assert.Equal(hash, RefreshTokenGenerator.Hash(raw));
    }

    [Fact]
    public void Tokens_are_unique()
    {
        var tokens = Enumerable.Range(0, 100).Select(_ => RefreshTokenGenerator.Create().RawToken).ToHashSet();
        Assert.Equal(100, tokens.Count);
    }
}
