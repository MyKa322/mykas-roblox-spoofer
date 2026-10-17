using MyKa.Spoofer;
using MyKa.Spoofer.Diagnostics;
using MyKa.Spoofer.Terminal;
using MyKa.Spoofer.Tests.Support;
using Xunit;

namespace MyKa.Spoofer.Tests;

[Collection("Console")]
public sealed class DiagnosticsTests : IDisposable
{
    private readonly string fixtureRoot = Path.Combine(AppContext.BaseDirectory, "TestData", Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(new string[] { "identity", "inspect", "--plain" }, Command.Inspect, true)]
    [InlineData(new string[] { "--plain", "doctor" }, Command.Doctor, true)]
    [InlineData(new string[] { "logo" }, Command.Logo, false)]
    [InlineData(new string[] { "--help" }, Command.Help, false)]
    [InlineData(new string[] { }, Command.Menu, false)]
    public void ParsesOnlyTheImplementedCommands(string[] args, Command command, bool plain)
        => Assert.Equal(new CommandLine(command, plain), CommandLine.Parse(args));

    [Theory]
    [InlineData("identity", "apply")]
    [InlineData("doctor", "unexpected")]
    [InlineData("--plain", "--plain")]
    [InlineData("launch")]
    public void RejectsUnsupportedOrAmbiguousInput(params string[] args)
        => Assert.Throws<ArgumentException>(() => CommandLine.Parse(args));

    [Fact]
    public void DoesNotEchoUnknownArgumentsThatCouldContainSecrets()
    {
        const string secret = "sensitive-value";
        var error = Assert.Throws<ArgumentException>(() => CommandLine.Parse(["--cookie", secret]));
        Assert.DoesNotContain(secret, error.Message);
    }

    [Fact]
    public void RemovesControlAndBidiCharactersButPreservesReadableUnicode()
        => Assert.Equal("[red]Сетьname", TerminalUi.SafeText("[red]\u001bСеть\r\n\u202ename"));

    [Fact]
    public void MissingDefaultLocationIsNotAnInstalledClient()
    {
        var result = InstallationDiscovery.Read(Path.Combine(fixtureRoot, "missing"));
        Assert.Empty(result.Clients);
        Assert.Empty(result.Problems);
    }

    [Fact]
    public void IgnoresStudioOtherFoldersAndDirectoriesNamedLikeExecutables()
    {
        Directory.CreateDirectory(Path.Combine(fixtureRoot, "version-studio"));
        File.WriteAllText(Path.Combine(fixtureRoot, "version-studio", "RobloxStudioBeta.exe"), "");
        Directory.CreateDirectory(Path.Combine(fixtureRoot, "unrelated"));
        File.WriteAllText(Path.Combine(fixtureRoot, "unrelated", "RobloxPlayerBeta.exe"), "");
        Directory.CreateDirectory(Path.Combine(fixtureRoot, "version-directory", "RobloxPlayerBeta.exe"));
        Assert.Empty(InstallationDiscovery.Read(fixtureRoot).Clients);
    }

    [Fact]
    public void FilePresenceRemainsAnUnverifiedCandidateWithoutInventingAVersion()
    {
        Directory.CreateDirectory(Path.Combine(fixtureRoot, "version-test"));
        File.WriteAllText(Path.Combine(fixtureRoot, "version-test", "RobloxPlayerBeta.exe"), "");
        var candidate = Assert.Single(InstallationDiscovery.Read(fixtureRoot).Clients);
        Assert.Equal("version-test", candidate.DirectoryName);
        Assert.Null(candidate.FileVersion);
    }

    [Fact]
    public void IncompleteDiscoveryDoesNotClaimTheClientIsAbsent()
    {
        var previous = Console.Out;
        using var output = new StringWriter();
        try
        {
            Console.SetOut(output);
            var result = new TerminalUi(true).Doctor(new DiagnosticReport("Windows", "X64", ".NET",
                new InstallationScan([], ["Access denied"]), DiagnosticFixture.Environment));
            Assert.Equal(1, result);
            Assert.Contains("installation presence could not be determined", output.ToString());
            Assert.DoesNotContain("no client candidate found", output.ToString());
        }
        finally { Console.SetOut(previous); }
    }

    public void Dispose()
    {
        var expectedParent = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "TestData")) + Path.DirectorySeparatorChar;
        var resolved = Path.GetFullPath(fixtureRoot);
        if (!resolved.StartsWith(expectedParent, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Test cleanup escaped its fixture directory.");
        if (Directory.Exists(resolved)) Directory.Delete(resolved, recursive: true);
    }
}
