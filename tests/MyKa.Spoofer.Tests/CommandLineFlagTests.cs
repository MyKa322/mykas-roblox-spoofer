using Xunit;

namespace MyKa.Spoofer.Tests;

public sealed class CommandLineFlagTests
{
    [Fact]
    public void StillIsOffByDefault()
        => Assert.False(CommandLine.Parse(["logo"]).Still);

    [Theory]
    [InlineData(new string[] { "--still" }, Command.Menu)]
    [InlineData(new string[] { "--still", "logo" }, Command.Logo)]
    [InlineData(new string[] { "logo", "--still" }, Command.Logo)]
    public void StillCanAccompanyAnyCommand(string[] args, Command command)
        => Assert.Equal(new CommandLine(command, false, null, true), CommandLine.Parse(args));

    [Fact]
    public void StillAndPlainCombine()
        => Assert.Equal(new CommandLine(Command.Logo, true, null, true), CommandLine.Parse(["--plain", "--still", "logo"]));

    [Fact]
    public void StillCombinesWithAnAccountCommand()
    {
        const string id = "0123456789abcdef0123456789abcdef";

        Assert.Equal(new CommandLine(Command.AccountsLogin, false, id, true),
            CommandLine.Parse(["--still", "accounts", "login", "--account", id]));
    }

    [Fact]
    public void StillCannotBeRepeated()
        => Assert.Throws<ArgumentException>(() => CommandLine.Parse(["--still", "--still"]));

    [Fact]
    public void AMisspelledFlagIsAnUnknownCommandNotSilentlyIgnored()
        => Assert.Throws<ArgumentException>(() => CommandLine.Parse(["--stil"]));
}
