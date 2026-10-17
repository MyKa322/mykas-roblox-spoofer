using MyKa.Spoofer.Accounts;
using Xunit;

namespace MyKa.Spoofer.Tests;

public sealed class RobloxIdentityTests
{
    private const string Url = RobloxIdentity.IdentityUrl;
    private const string ValidBody = """{"id":1234567890123,"name":"alice","displayName":"Alice"}""";

    [Fact]
    public void ParsesTheAuthenticatedUserResponse()
        => Assert.Equal(new RobloxIdentity("1234567890123", "alice", "Alice"), RobloxIdentity.Parse(Url, 200, ValidBody));

    [Fact]
    public void KeepsTheLargestUserIdExact()
    {
        var identity = RobloxIdentity.Parse(Url, 200, """{"id":18446744073709551615,"name":"a","displayName":"A"}""");
        Assert.Equal("18446744073709551615", identity.UserId);
    }

    [Theory]
    [InlineData("https://users.roblox.com/v1/users/authenticated?x=1", 200)]
    [InlineData("https://evil.example/v1/users/authenticated", 200)]
    [InlineData(Url, 401)]
    [InlineData(Url, 204)]
    public void RejectsAResponseThatIsNotAConfirmedIdentity(string url, int status)
        => Assert.Throws<InvalidDataException>(() => RobloxIdentity.Parse(url, status, ValidBody));

    [Theory]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("not json")]
    [InlineData("""{"id":0,"name":"a","displayName":"A"}""")]
    [InlineData("""{"id":-5,"name":"a","displayName":"A"}""")]
    [InlineData("""{"id":"12","name":"a","displayName":"A"}""")]
    [InlineData("""{"id":1.5,"name":"a","displayName":"A"}""")]
    [InlineData("""{"id":12,"name":null,"displayName":"A"}""")]
    [InlineData("""{"id":12,"name":"a","displayName":""}""")]
    [InlineData("""{"id":12,"name":"a\u001b[31m","displayName":"A"}""")]
    public void RejectsMalformedIdentityBodies(string body)
        => Assert.Throws<InvalidDataException>(() => RobloxIdentity.Parse(Url, 200, body));

    [Fact]
    public void RejectsAnOversizedBody()
        => Assert.Throws<InvalidDataException>(() => RobloxIdentity.Parse(Url, 200, ValidBody + new string(' ', 16_385)));

    [Theory]
    [InlineData("https://www.roblox.com/login")]
    [InlineData("https://roblox.com/")]
    [InlineData("https://www.roblox.com/home?returnUrl=%2F")]
    [InlineData(Url)]
    public void AllowsOnlyRobloxPagesAndTheIdentityEndpoint(string url)
        => Assert.True(RobloxIdentity.IsAllowedPage(url));

    [Theory]
    [InlineData("http://www.roblox.com/login")]
    [InlineData("https://www.roblox.com:8443/login")]
    [InlineData("https://user:pass@www.roblox.com/login")]
    [InlineData("https://www.roblox.com.evil.example/login")]
    [InlineData("https://evilroblox.com/login")]
    [InlineData("https://users.roblox.com/v1/users/authenticated?x=1")]
    [InlineData("https://users.roblox.com/v1/users/1")]
    [InlineData("https://accounts.google.com/")]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///C:/Windows/win.ini")]
    [InlineData("/login")]
    [InlineData("")]
    public void RejectsEveryOtherDestination(string url)
        => Assert.False(RobloxIdentity.IsAllowedPage(url));

    [Theory]
    [InlineData("1", true)]
    [InlineData("18446744073709551615", true)]
    [InlineData("0", false)]
    [InlineData("007", false)]
    [InlineData("+7", false)]
    [InlineData("-7", false)]
    [InlineData(" 7", false)]
    [InlineData("7 ", false)]
    [InlineData("\uFF11\uFF12", false)]
    [InlineData("18446744073709551616", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void UserIdsAreCanonicalPositiveDecimalText(string? value, bool expected)
        => Assert.Equal(expected, RobloxIdentity.ValidUserId(value));

    [Theory]
    [InlineData("alice", true)]
    [InlineData("\u0410\u043B\u0438\u0441\u0430", true)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("a\nb", false)]
    [InlineData("a\u200Eb", false)]
    [InlineData("a\u202Eb", false)]
    public void NamesExcludeControlAndDirectionalCharacters(string value, bool expected)
        => Assert.Equal(expected, RobloxIdentity.ValidName(value));

    [Fact]
    public void NamesAreLimitedTo128Characters()
    {
        Assert.True(RobloxIdentity.ValidName(new string('a', 128)));
        Assert.False(RobloxIdentity.ValidName(new string('a', 129)));
    }
}
