using MyKa.Spoofer.Accounts;
using Xunit;

namespace MyKa.Spoofer.Tests;

public sealed class AccountOperationsTests : IDisposable
{
    private static readonly RobloxIdentity Alice = new("1001", "alice", "Alice");
    private static readonly RobloxIdentity Bob = new("2002", "bob", "Bob");
    private static readonly string TestData =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "TestData")) + Path.DirectorySeparatorChar;

    private readonly string root = Path.Combine(AppContext.BaseDirectory, "TestData", Guid.NewGuid().ToString("N"));
    private readonly CancellationTokenSource cancellation = new();
    private readonly List<string> report = [];
    private readonly List<(string Profile, string? Expected)> logins = [];

    /// <summary>What the pretend sign-in window returns. Null means the window was closed without saving.</summary>
    private RobloxIdentity? signedIn;

    private Exception? signInFailure;
    private Action? duringSignIn;

    private AccountOperations Ops() => new(cancellation.Token, root, (profile, expected, _) =>
    {
        logins.Add((profile, expected));
        Assert.True(Directory.Exists(profile), "the profile folder should exist while the window is open");
        duringSignIn?.Invoke();
        if (signInFailure is not null) throw signInFailure;
        return signedIn;
    });

    private SavedAccount AddAlice()
    {
        signedIn = Alice;
        Assert.Equal(0, Ops().Add(_ => { }));
        return Assert.Single(Ops().List().Accounts);
    }

    private string[] ProfileFolders()
    {
        var profiles = Path.Combine(root, "profiles");
        return Directory.Exists(profiles) ? Directory.GetDirectories(profiles) : [];
    }

    [Fact]
    public void AddingSavesTheAccountRobloxConfirmed()
    {
        signedIn = Alice;

        var code = Ops().Add(report.Add);

        var listing = Ops().List();
        var account = Assert.Single(listing.Accounts);
        Assert.Equal(0, code);
        Assert.Equal(["Saved verified Roblox account: alice (user 1001).", "Local account ID: " + account.Id], report);
        Assert.Equal("1001", account.UserId);
        Assert.Equal("alice", account.Username);
        Assert.Equal(0, listing.PendingCleanup);
        Assert.Equal([Path.Combine(root, "profiles", account.ProfileId)], ProfileFolders());
        Assert.Equal((Path.Combine(root, "profiles", account.ProfileId), (string?)null), Assert.Single(logins));
    }

    [Fact]
    public void AddingAndClosingTheWindowChangesNothingAndLeavesNoProfileBehind()
    {
        signedIn = null;

        var code = Ops().Add(report.Add);

        Assert.Equal(130, code);
        Assert.Equal(["Sign-in cancelled. Saved accounts were not changed."], report);
        var listing = Ops().List();
        Assert.Empty(listing.Accounts);
        Assert.Equal(0, listing.PendingCleanup);
        Assert.Empty(ProfileFolders());
    }

    [Fact]
    public void AMissingLoginBrowserSurfacesAsAnErrorAndLeavesNoProfileBehind()
    {
        signInFailure = new InvalidOperationException("Microsoft Edge WebView2 Runtime is required.");

        var error = Assert.Throws<InvalidOperationException>(() => Ops().Add(report.Add));

        Assert.Contains("WebView2", error.Message);
        Assert.Empty(Ops().List().Accounts);
        Assert.Empty(ProfileFolders());
        Assert.Equal(0, Ops().List().PendingCleanup);
    }

    [Fact]
    public void AddingAnAccountThatIsAlreadySavedIsRefusedAndKeepsOnlyTheOriginalProfile()
    {
        var original = AddAlice();

        var error = Assert.Throws<InvalidOperationException>(() => Ops().Add(report.Add));

        Assert.Contains("already saved", error.Message);
        Assert.Equal(original, Assert.Single(Ops().List().Accounts));
        Assert.Equal([Path.Combine(root, "profiles", original.ProfileId)], ProfileFolders());
    }

    [Fact]
    public void SigningInAgainKeepsTheLocalIdAndRetiresTheOldProfile()
    {
        var original = AddAlice();
        signedIn = Alice with { DisplayName = "Alice Renamed" };

        var code = Ops().SignInAgain(original.Id, report.Add);

        var account = Assert.Single(Ops().List().Accounts);
        Assert.Equal(0, code);
        Assert.Equal(original.Id, account.Id);
        Assert.NotEqual(original.ProfileId, account.ProfileId);
        Assert.Equal("Alice Renamed", account.DisplayName);
        Assert.Equal("1001", logins[^1].Expected);
        Assert.Equal([Path.Combine(root, "profiles", account.ProfileId)], ProfileFolders());
        Assert.Equal(0, Ops().List().PendingCleanup);
    }

    [Fact]
    public void SigningInAgainAsADifferentRobloxAccountChangesNothing()
    {
        var original = AddAlice();
        signedIn = Bob;

        var error = Assert.Throws<InvalidOperationException>(() => Ops().SignInAgain(original.Id, report.Add));

        Assert.Contains("different Roblox account", error.Message);
        Assert.Equal(original, Assert.Single(Ops().List().Accounts));
        Assert.Equal([Path.Combine(root, "profiles", original.ProfileId)], ProfileFolders());
    }

    [Fact]
    public void SigningInAgainForAnUnknownAccountFailsBeforeAWindowOpens()
    {
        var before = logins.Count;

        Assert.Throws<ArgumentException>(() => Ops().SignInAgain(new string('a', 32), report.Add));

        Assert.Equal(before, logins.Count);
        Assert.Empty(ProfileFolders());
    }

    [Fact]
    public void RemovalDoesNothingUnlessConfirmed()
    {
        var original = AddAlice();
        SavedAccount? asked = null;

        var code = Ops().Remove(original.Id, account =>
        {
            asked = account;
            return false;
        }, report.Add);

        Assert.Equal(130, code);
        Assert.Equal(original, asked);
        Assert.Empty(report);
        Assert.Equal(original, Assert.Single(Ops().List().Accounts));
        Assert.Single(ProfileFolders());
    }

    [Fact]
    public void ConfirmedRemovalDeletesTheAccountAndItsBrowserProfile()
    {
        var original = AddAlice();

        var code = Ops().Remove(original.Id, _ => true, report.Add);

        Assert.Equal(0, code);
        Assert.Equal(["Local account removed."], report);
        Assert.Empty(Ops().List().Accounts);
        Assert.Empty(ProfileFolders());
    }

    [Fact]
    public void RemovingAnUnknownAccountFailsWithoutAsking()
    {
        var asked = false;

        Assert.Throws<ArgumentException>(() => Ops().Remove(new string('a', 32), _ =>
        {
            asked = true;
            return true;
        }, report.Add));

        Assert.False(asked);
    }

    [Fact]
    public void CleanUpWithNothingPendingSaysItIsComplete()
    {
        var code = Ops().CleanUp(report.Add);

        Assert.Equal(0, code);
        Assert.Equal(["Unfinished profile cleanup is complete."], report);
    }

    [Fact]
    public void CleanUpRemovesProfilesThatWereReservedButNeverUsed()
    {
        using (var store = AccountStore.Open(root)) store.ReserveProfile();
        Assert.Equal(1, Ops().List().PendingCleanup);
        Assert.Single(ProfileFolders());

        var code = Ops().CleanUp(report.Add);

        Assert.Equal(0, code);
        Assert.Equal(0, Ops().List().PendingCleanup);
        Assert.Empty(ProfileFolders());
    }

    [Fact]
    public void ListingReportsEverySavedAccountAndTheCleanupBacklog()
    {
        AddAlice();
        signedIn = Bob;
        Ops().Add(_ => { });
        using (var store = AccountStore.Open(root)) store.ReserveProfile();

        var listing = Ops().List();

        Assert.Equal(["alice", "bob"], listing.Accounts.Select(a => a.Username).Order());
        Assert.Equal(1, listing.PendingCleanup);
    }

    [Fact]
    public void ACancelledTokenStopsBeforeAnythingIsReserved()
    {
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() => Ops().Add(report.Add));

        Assert.Empty(logins);
        Assert.Empty(ProfileFolders());
    }

    [Fact]
    public void CancellingWhileTheWindowIsOpenSavesNothingAndCleansUp()
    {
        signedIn = Alice;
        duringSignIn = cancellation.Cancel;

        Assert.Throws<OperationCanceledException>(() => Ops().Add(report.Add));

        Assert.Empty(Ops().List().Accounts);
        Assert.Empty(ProfileFolders());
    }

    [Fact]
    public void OnlyOneOperationHoldsTheAccountDataAtATime()
    {
        signedIn = Alice;
        var blocked = false;
        duringSignIn = () =>
        {
            try { AccountStore.Open(root).Dispose(); }
            catch (IOException) { blocked = true; }
        };

        Ops().Add(_ => { });

        Assert.True(blocked, "a second opener should be refused while sign-in is in progress");
        AccountStore.Open(root).Dispose();
    }

    public void Dispose()
    {
        cancellation.Dispose();
        var resolved = Path.GetFullPath(root);
        if (!resolved.StartsWith(TestData, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Test cleanup escaped its fixture directory.");
        if (Directory.Exists(resolved)) Directory.Delete(resolved, recursive: true);
    }

    [Theory]
    [InlineData(ExperienceResult.HandoffRequested, 0)]
    [InlineData(ExperienceResult.Closed, 130)]
    [InlineData(ExperienceResult.Failed, 1)]
    public void ExperienceUsesSavedProfileWhileHoldingLockAndPreservesMetadata(ExperienceResult result, int code)
    {
        var account = AddAlice();
        var metadata = File.ReadAllBytes(Path.Combine(root, "accounts.json"));
        var called = false;
        var ops = new AccountOperations(cancellation.Token, root, experienceRunner: (profile, selected, place, token) =>
        {
            called = true;
            Assert.Equal(account, selected);
            Assert.Equal(Path.Combine(root, "profiles", account.ProfileId), profile);
            Assert.Equal("1818", place);
            Assert.Equal(cancellation.Token, token);
            Assert.Throws<IOException>(() => AccountStore.Open(root));
            return result;
        });
        Assert.Equal(code, ops.OpenExperience(account.Id, "1818", report.Add));
        Assert.True(called);
        Assert.Equal(metadata, File.ReadAllBytes(Path.Combine(root, "accounts.json")));
        Assert.Equal(account, Assert.Single(ops.List().Accounts));
        if (result == ExperienceResult.HandoffRequested) Assert.Contains("unverified", report[0]);
    }

    [Fact]
    public void ACancelledExperienceDoesNotOpenTheBrowser()
    {
        var account = AddAlice();
        cancellation.Cancel();
        var ops = new AccountOperations(cancellation.Token, root, experienceRunner: (_, _, _, _) => throw new Exception("Must not run"));
        Assert.Throws<OperationCanceledException>(() => ops.OpenExperience(account.Id, "1818", report.Add));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CancellationAfterTheBrowserReturnsPreservesAnyHandoff(bool handedOff)
    {
        var account = AddAlice();
        var ops = new AccountOperations(cancellation.Token, root, experienceRunner: (_, _, _, _) =>
        {
            cancellation.Cancel();
            return handedOff ? ExperienceResult.HandoffRequested : ExperienceResult.Closed;
        });
        if (handedOff) Assert.Equal(0, ops.OpenExperience(account.Id, "1818", report.Add));
        else Assert.Throws<OperationCanceledException>(() => ops.OpenExperience(account.Id, "1818", report.Add));
        Assert.Equal(account, Assert.Single(Ops().List().Accounts));
    }

    [Fact]
    public void MissingSavedProfileCannotSilentlyCreateANewSession()
    {
        var account = AddAlice();
        Directory.Delete(Path.Combine(root, "profiles", account.ProfileId)); // Empty test profile only.
        var ops = new AccountOperations(cancellation.Token, root, experienceRunner: (_, _, _, _) => throw new Exception("Must not run"));
        Assert.Throws<InvalidOperationException>(() => ops.OpenExperience(account.Id, "1818", report.Add));
    }

    [Fact]
    public void InvalidOrMissingSelectionNeverOpensTheBrowser()
    {
        var account = AddAlice();
        var ops = new AccountOperations(cancellation.Token, root, experienceRunner: (_, _, _, _) => throw new Exception("Must not run"));
        Assert.Throws<ArgumentException>(() => ops.OpenExperience(account.Id, "https://evil.test", report.Add));
        Assert.Throws<ArgumentException>(() => ops.OpenExperience(new string('0', 32), "1818", report.Add));
    }
}
