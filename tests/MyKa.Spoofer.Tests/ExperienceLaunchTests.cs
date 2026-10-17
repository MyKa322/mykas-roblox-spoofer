using MyKa.Spoofer.Accounts;
using Xunit;

namespace MyKa.Spoofer.Tests;

public sealed class ExperienceLaunchTests
{
    private const string Origin = "https://www.roblox.com";
    private const string Player = "roblox-player:1+launchmode:play+test:value";
    private static readonly RobloxIdentity Alice = new("1001", "alice", "Alice");

    private static ExperienceLaunch Ready()
    {
        var launch = new ExperienceLaunch(Alice.UserId, "1818");
        Assert.True(launch.BeginNavigation(RobloxIdentity.IdentityUrl));
        launch.ConfirmIdentity(Alice);
        Assert.True(launch.BeginNavigation(launch.PlaceUrl));
        launch.CompletePlace(launch.PlaceUrl + "/Test", true);
        return launch;
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("01")]
    [InlineData("-1")]
    [InlineData("+1")]
    [InlineData(" 1")]
    [InlineData("1 ")]
    [InlineData("1\r\n")]
    [InlineData("１")]
    [InlineData("18446744073709551616")]
    [InlineData("https://www.roblox.com/games/1818")]
    [InlineData("1818/../../login")]
    public void PlaceInputCannotBecomeAnArbitraryUrl(string? value)
        => Assert.False(ExperienceLaunch.ValidPlaceId(value));

    [Theory]
    [InlineData("1")]
    [InlineData("1818")]
    [InlineData("18446744073709551615")]
    public void CanonicalPlaceIdsBuildOnlyTheOfficialExperienceUrl(string id)
        => Assert.Equal(Origin + "/games/" + id, new ExperienceLaunch("1001", id).PlaceUrl);

    [Theory]
    [InlineData("https://www.roblox.com/games/1818")]
    [InlineData("https://www.roblox.com/games/1818/Test?locale=en_us")]
    [InlineData("https://www.roblox.com:443/games/1818/Test")]
    public void AllowsTheChosenPlaceAndItsSlug(string url) => Assert.True(Ready().IsPlacePage(url));

    [Theory]
    [InlineData("http://www.roblox.com/games/1818")]
    [InlineData("https://www.roblox.com:444/games/1818")]
    [InlineData("https://www.roblox.com.evil.test/games/1818")]
    [InlineData("https://evil@www.roblox.com/games/1818")]
    [InlineData("https://www.roblox.com/games/18180")]
    [InlineData("https://www.roblox.com/games/1818/../../login")]
    [InlineData("https://www.roblox.com/login")]
    [InlineData("file:///games/1818")]
    public void RejectsNavigationAwayAndRevokesTheAttempt(string url)
    {
        var launch = Ready();
        Assert.False(launch.BeginNavigation(url));
        Assert.False(launch.TryRequestHandoff(Player, Origin, launch.PlaceUrl, true));
        Assert.Equal(ExperienceResult.Failed, launch.Result);
    }

    [Fact]
    public void WrongAccountCannotEnablePlay()
    {
        var launch = new ExperienceLaunch("1001", "1818");
        Assert.Throws<InvalidOperationException>(() => launch.ConfirmIdentity(Alice with { UserId = "2002" }));
        Assert.False(launch.BeginNavigation(launch.PlaceUrl));
        Assert.False(launch.TryRequestHandoff(Player, Origin, launch.PlaceUrl, true));
    }

    [Fact]
    public void PlayNeedsBothIdentityAndCompletedPage()
    {
        var launch = new ExperienceLaunch("1001", "1818");
        Assert.False(launch.TryRequestHandoff(Player, Origin, launch.PlaceUrl, true));
        launch.ConfirmIdentity(Alice);
        Assert.False(launch.TryRequestHandoff(Player, Origin, launch.PlaceUrl, true));
        launch.CompletePlace(launch.PlaceUrl, false);
        Assert.False(launch.TryRequestHandoff(Player, Origin, launch.PlaceUrl, true));
    }

    [Fact]
    public void ReloadSuspendsPlayUntilItCompletes()
    {
        var launch = Ready();
        Assert.True(launch.BeginNavigation(launch.PlaceUrl));
        Assert.False(launch.TryRequestHandoff(Player, Origin, launch.PlaceUrl, true));
        launch.CompletePlace(launch.PlaceUrl, true);
        Assert.True(launch.TryRequestHandoff(Player, Origin, launch.PlaceUrl, true));
    }

    [Theory]
    [InlineData("https://evil.test", true)]
    [InlineData("", true)]
    [InlineData("http://www.roblox.com", true)]
    [InlineData("https://www.roblox.com:444", true)]
    [InlineData("https://www.roblox.com.evil.test", true)]
    [InlineData("https://www.roblox.com", false)]
    public void OriginAndUserGestureAreRequired(string origin, bool gesture)
    {
        var launch = Ready();
        Assert.False(launch.TryRequestHandoff(Player, origin, launch.PlaceUrl, gesture));
        Assert.Equal(ExperienceResult.Closed, launch.Result);
    }

    [Theory]
    [InlineData("file:///C:/test.exe")]
    [InlineData("roblox://placeId=1818")]
    [InlineData("roblox-player:")]
    [InlineData("roblox-player:1\n+secret")]
    [InlineData("roblox-player:1\"+secret")]
    [InlineData("roblox-player:1 secret")]
    [InlineData("roblox-player:1\u202e")]
    [InlineData(" https://www.roblox.com")]
    public void BlocksUnrelatedOrMalformedExternalRequests(string uri)
    {
        var launch = Ready();
        Assert.False(launch.TryRequestHandoff(uri, Origin, launch.PlaceUrl, true));
    }

    [Fact]
    public void OversizedProtocolDataIsBlocked()
        => Assert.False(ExperienceLaunch.IsPlayerUri("roblox-player:" + new string('a', 16384)));

    [Fact]
    public void SourceMustStillBeTheChosenPlace()
        => Assert.False(Ready().TryRequestHandoff(Player, Origin, "https://www.roblox.com/login", true));

    [Fact]
    public void OneHandoffSurvivesLaterErrorsAndCannotBeRetried()
    {
        var launch = Ready();
        Assert.True(launch.BeginNavigation(Player));
        Assert.True(launch.TryRequestHandoff(Player, Origin, launch.PlaceUrl, true));
        launch.Fail();
        launch.CompletePlace(launch.PlaceUrl, true);
        Assert.Equal(ExperienceResult.HandoffRequested, launch.Result);
        Assert.False(launch.BeginNavigation(launch.PlaceUrl));
        Assert.False(launch.TryRequestHandoff(Player, Origin, launch.PlaceUrl, true));
    }
}
