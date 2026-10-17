using MyKa.Spoofer.Accounts;
using MyKa.Spoofer.Terminal;
using Xunit;

namespace MyKa.Spoofer.Tests;

[Collection("Console")]
public sealed class AccountListingTests
{
    [Fact]
    public void ListingShowsTheVerifiedIdentityWithoutClaimingTheLoginIsStillValid()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "TestData", Guid.NewGuid().ToString("N"));
        var previous = Console.Out;
        using var output = new StringWriter();
        SavedAccount saved;
        try
        {
            using var store = AccountStore.Open(root);
            saved = store.CommitLogin(store.ReserveProfile(), new RobloxIdentity("1001", "alice", "Alice"));
            Console.SetOut(output);
            new TerminalUi(true).Accounts(store);
        }
        finally
        {
            Console.SetOut(previous);
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }

        var text = output.ToString();
        Assert.Contains("alice", text);
        Assert.Contains("1001", text);
        Assert.Contains(saved.Id, text);
        Assert.Contains("Current login status: not checked", text);
    }
}
