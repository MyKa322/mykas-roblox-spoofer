using System.Diagnostics;
using MyKa.Spoofer.Accounts;
using Xunit;

namespace MyKa.Spoofer.Tests;

public sealed class AccountStoreTests : IDisposable
{
    private static readonly RobloxIdentity Alice = new("1001", "alice", "Alice");
    private static readonly RobloxIdentity Bob = new("2002", "bob", "Bob");
    private static readonly string TestData =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "TestData")) + Path.DirectorySeparatorChar;

    private readonly string root = Path.Combine(AppContext.BaseDirectory, "TestData", Guid.NewGuid().ToString("N"));
    private readonly List<AccountStore> opened = [];

    private AccountStore Open()
    {
        var store = AccountStore.Open(root);
        opened.Add(store);
        return store;
    }

    private void CloseStores()
    {
        foreach (var store in opened) store.Dispose();
        opened.Clear();
    }

    private static SavedAccount SignIn(AccountStore store, RobloxIdentity identity, string? existingId = null)
        => store.CommitLogin(store.ReserveProfile(), identity, existingId);

    private void WriteMetadata(string content)
    {
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "accounts.json"), content);
    }

    private static string AccountJson(char id, char profile, string userId)
        => $$"""{"id":"{{new string(id, 32)}}","profileId":"{{new string(profile, 32)}}","userId":"{{userId}}","username":"user","displayName":"User","lastVerifiedUtc":"2026-10-15T00:00:00Z"}""";

    [Fact]
    public void NewStoreHasNoAccountsAndNothingPending()
    {
        var store = Open();
        Assert.Empty(store.Accounts);
        Assert.Equal(0, store.PendingCleanupCount);
    }

    [Fact]
    public void CommittedLoginPersistsAcrossReopen()
    {
        var saved = SignIn(Open(), Alice);
        CloseStores();

        var reopened = Open();

        var account = Assert.Single(reopened.Accounts);
        Assert.Equal(saved, account);
        Assert.True(AccountStore.ValidLocalId(account.Id));
        Assert.True(Directory.Exists(reopened.ProfilePath(account.ProfileId)));
        Assert.Equal(0, reopened.PendingCleanupCount);
    }

    [Fact]
    public void AccountAlreadySavedIsRejectedAndTheReservedProfileIsCleanedUp()
    {
        var store = Open();
        SignIn(store, Alice);
        var duplicate = store.ReserveProfile();

        Assert.Throws<InvalidOperationException>(() => store.CommitLogin(duplicate, Alice with { Username = "renamed" }));

        Assert.Single(store.Accounts);
        Assert.Equal(1, store.PendingCleanupCount);
        Assert.Equal(0, store.CleanupPending());
        Assert.False(Directory.Exists(store.ProfilePath(duplicate)));
    }

    [Fact]
    public void SignInAgainKeepsTheLocalIdAndRetiresTheOldProfile()
    {
        var store = Open();
        var first = SignIn(store, Alice);

        var second = SignIn(store, Alice with { DisplayName = "Alice Renamed" }, first.Id);

        Assert.Equal(first.Id, second.Id);
        Assert.NotEqual(first.ProfileId, second.ProfileId);
        Assert.Equal("Alice Renamed", Assert.Single(store.Accounts).DisplayName);
        Assert.Equal(1, store.PendingCleanupCount);
        var oldProfile = store.ProfilePath(first.ProfileId);
        Assert.Equal(0, store.CleanupPending());
        Assert.False(Directory.Exists(oldProfile));
        Assert.True(Directory.Exists(store.ProfilePath(second.ProfileId)));
    }

    [Fact]
    public void SignInAgainAsADifferentRobloxAccountChangesNothing()
    {
        var store = Open();
        var alice = SignIn(store, Alice);
        var profile = store.ReserveProfile();

        Assert.Throws<InvalidOperationException>(() => store.CommitLogin(profile, Bob, alice.Id));

        Assert.Equal(alice, Assert.Single(store.Accounts));
    }

    [Fact]
    public void RejectsAProfileThatWasNeverReserved()
    {
        var store = Open();
        var unreserved = Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(store.ProfilePath(unreserved));

        Assert.Throws<InvalidOperationException>(() => store.CommitLogin(unreserved, Alice));

        Assert.Empty(store.Accounts);
    }

    [Theory]
    [InlineData("0", "alice", "Alice")]
    [InlineData("007", "alice", "Alice")]
    [InlineData("1001", "", "Alice")]
    [InlineData("1001", "ali\u001bce", "Alice")]
    [InlineData("1001", "alice", "Ali‮ce")]
    public void RejectsAnInvalidVerifiedIdentity(string userId, string username, string displayName)
    {
        var store = Open();
        var profile = store.ReserveProfile();

        Assert.Throws<InvalidDataException>(() => store.CommitLogin(profile, new RobloxIdentity(userId, username, displayName)));

        Assert.Empty(store.Accounts);
    }

    [Fact]
    public void RemovingAnAccountDeletesItsBrowserProfileOnCleanup()
    {
        var store = Open();
        var alice = SignIn(store, Alice);
        var bob = SignIn(store, Bob);
        var profile = store.ProfilePath(alice.ProfileId);
        File.WriteAllText(Path.Combine(profile, "session-stand-in"), "x");

        store.Remove(alice.Id);

        Assert.Equal(bob, Assert.Single(store.Accounts));
        Assert.Equal(1, store.PendingCleanupCount);
        Assert.Equal(0, store.CleanupPending());
        Assert.False(Directory.Exists(profile));
        Assert.True(Directory.Exists(store.ProfilePath(bob.ProfileId)));
        Assert.Throws<ArgumentException>(() => store.Get(alice.Id));
    }

    [Fact]
    public void OnlyOneStoreCanBeOpenAtATime()
    {
        Open();

        Assert.Throws<IOException>(() => AccountStore.Open(root));

        CloseStores();
        Open();
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"schemaVersion":2,"accounts":[],"pendingProfiles":[]}""")]
    [InlineData("""{"schemaVersion":1,"accounts":[],"pendingProfiles":[],"extra":true}""")]
    [InlineData("""{"schemaVersion":1,"accounts":null,"pendingProfiles":[]}""")]
    [InlineData("""{"schemaVersion":1,"accounts":[],"pendingProfiles":["not-a-profile-id"]}""")]
    public void InvalidMetadataIsRejectedAndPreserved(string content)
    {
        WriteMetadata(content);

        Assert.Throws<InvalidDataException>(() => AccountStore.Open(root));

        Assert.Equal(content, File.ReadAllText(Path.Combine(root, "accounts.json")));
    }

    [Fact]
    public void LoadsWellFormedMetadata()
    {
        var first = AccountJson('a', 'c', "5");
        var second = AccountJson('b', 'd', "6");
        var pending = new string('e', 32);
        WriteMetadata($$"""{"schemaVersion":1,"accounts":[{{first}},{{second}}],"pendingProfiles":["{{pending}}"]}""");

        var store = Open();

        Assert.Equal(new[] { "5", "6" }, store.Accounts.Select(a => a.UserId));
        Assert.Equal(1, store.PendingCleanupCount);
    }

    [Theory]
    [InlineData('a', 'd', "6")]
    [InlineData('b', 'c', "6")]
    [InlineData('b', 'd', "5")]
    public void DuplicateIdentifiersInStoredMetadataAreRejected(char id, char profile, string userId)
    {
        var first = AccountJson('a', 'c', "5");
        var second = AccountJson(id, profile, userId);
        WriteMetadata($$"""{"schemaVersion":1,"accounts":[{{first}},{{second}}],"pendingProfiles":[]}""");

        Assert.Throws<InvalidDataException>(() => AccountStore.Open(root));
    }

    [Fact]
    public void MissingMetadataWithABackupIsNotReplacedWithAnEmptyStore()
    {
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "accounts.json.bak"), "{}");

        Assert.Throws<InvalidDataException>(() => AccountStore.Open(root));

        Assert.False(File.Exists(Path.Combine(root, "accounts.json")));
    }

    [Theory]
    [InlineData("")]
    [InlineData("..")]
    [InlineData("..\\..\\outside")]
    [InlineData("short")]
    [InlineData("ABCDEF0123456789ABCDEF0123456789")]
    public void ProfilePathRejectsIdsThatAreNotGeneratedLocalIds(string id)
        => Assert.Throws<ArgumentException>(() => Open().ProfilePath(id));

    [Fact]
    public void LinkedProfilesDirectoryIsRefusedAndNothingIsWrittenThroughIt()
    {
        var store = Open();
        var outside = Directory.CreateDirectory(root + "-outside").FullName;
        var link = Path.Combine(root, "profiles");
        CreateJunction(link, outside);
        try
        {
            Assert.Throws<IOException>(() => store.ReserveProfile());
            Assert.Empty(Directory.EnumerateFileSystemEntries(outside));
        }
        finally { Directory.Delete(link); }
    }

    [Fact]
    public void AWriteThatExceedsTheReadLimitPreservesTheReopenableMetadata()
    {
        // A compact, valid input below the read limit can exceed it when formatted on the next write.
        var pending = Enumerable.Range(0, 26000).Select(i => i.ToString("x32")).ToArray();
        var content = System.Text.Json.JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            accounts = Array.Empty<object>(),
            pendingProfiles = pending,
        });
        WriteMetadata(content);
        var store = Open();

        Assert.Throws<InvalidDataException>(() => store.ReserveProfile());

        Assert.Equal(content, File.ReadAllText(Path.Combine(root, "accounts.json")));
        Assert.Equal(pending.Length, store.PendingCleanupCount);
        CloseStores();
        Assert.Equal(pending.Length, Open().PendingCleanupCount);
        Assert.Empty(Directory.GetFiles(root, "*.tmp"));
    }

    private static void CreateJunction(string link, string target)
    {
        var start = new ProcessStartInfo("cmd.exe") { UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in new[] { "/c", "mklink", "/J", link, target }) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("cmd.exe did not start.");
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, "Could not create a directory junction for the test.");
    }

    public void Dispose()
    {
        CloseStores();
        foreach (var path in new[] { root, root + "-outside" })
        {
            var resolved = Path.GetFullPath(path);
            if (!resolved.StartsWith(TestData, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Test cleanup escaped its fixture directory.");
            if (Directory.Exists(resolved)) Directory.Delete(resolved, recursive: true);
        }
    }
}
