using Xunit;

namespace MyKa.Spoofer.Tests;

public sealed class AccountCommandLineTests
{
    private const string Id = "0123456789abcdef0123456789abcdef";

    [Fact]
    public void ParsesPlaceAndAccountInEitherOrderWithGlobalFlags()
    {
        var expected = new CommandLine(Command.AccountsPlay, true, Id, true, "1818");
        Assert.Equal(expected, CommandLine.Parse(["--plain", "accounts", "play", "--account", Id, "--place", "1818", "--still"]));
        Assert.Equal(expected, CommandLine.Parse(["--still", "accounts", "play", "--place", "1818", "--account", Id, "--plain"]));
    }

    [Theory]
    [InlineData("https://www.roblox.com/games/1818")]
    [InlineData("secret-ticket")]
    [InlineData("0")]
    [InlineData("01")]
    public void InvalidPlaceValuesAreNotEchoed(string place)
    {
        var error = Assert.Throws<ArgumentException>(() => CommandLine.Parse(["accounts", "play", "--account", Id, "--place", place]));
        Assert.DoesNotContain(place, error.Message);
    }

    [Theory]
    [InlineData(new string[] { "accounts", "list" }, Command.AccountsList)]
    [InlineData(new string[] { "accounts", "add" }, Command.AccountsAdd)]
    [InlineData(new string[] { "accounts", "cleanup" }, Command.AccountsCleanup)]
    public void ParsesAccountCommandsThatNeedNoAccountId(string[] args, Command command)
        => Assert.Equal(new CommandLine(command, false), CommandLine.Parse(args));

    [Theory]
    [InlineData("login", Command.AccountsLogin)]
    [InlineData("remove", Command.AccountsRemove)]
    public void ParsesAccountCommandsThatNameALocalAccount(string verb, Command command)
        => Assert.Equal(new CommandLine(command, false, Id), CommandLine.Parse(["accounts", verb, "--account", Id]));

    [Fact]
    public void PlainCanAccompanyAnAccountCommand()
        => Assert.Equal(new CommandLine(Command.AccountsList, true), CommandLine.Parse(["accounts", "--plain", "list"]));

    [Theory]
    [InlineData("accounts")]
    [InlineData("accounts", "list", "extra")]
    [InlineData("accounts", "login")]
    [InlineData("accounts", "login", "--account")]
    [InlineData("accounts", "login", Id)]
    [InlineData("accounts", "login", "--account", Id, "--account", Id)]
    [InlineData("accounts", "login", "--account", "0123456789abcdef0123456789abcde")]
    [InlineData("accounts", "remove", "--account", "ABCDEF0123456789ABCDEF0123456789")]
    [InlineData("accounts", "remove", "--account", "../../outside")]
    [InlineData("accounts", "add", "--account", Id)]
    [InlineData("accounts", "delete", "--account", Id)]
    [InlineData("accounts", "play", "--account", Id)]
    [InlineData("accounts", "play", "--place", "1818")]
    [InlineData("accounts", "play", "--account", Id, "--place", "1818", "--place", "1818")]
    public void RejectsMalformedAccountCommands(params string[] args)
        => Assert.Throws<ArgumentException>(() => CommandLine.Parse(args));

    [Fact]
    public void DoesNotEchoAMalformedAccountIdThatCouldBeASecret()
    {
        const string secret = "sensitive-value";
        var error = Assert.Throws<ArgumentException>(() => CommandLine.Parse(["accounts", "login", "--account", secret]));
        Assert.DoesNotContain(secret, error.Message);
    }
}
