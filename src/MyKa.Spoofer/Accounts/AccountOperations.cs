namespace MyKa.Spoofer.Accounts;

public sealed record AccountListing(IReadOnlyList<SavedAccount> Accounts, int PendingCleanup);

/// <summary>Runs the sign-in window for one profile and returns the identity Roblox confirmed, or null if cancelled.</summary>
public delegate RobloxIdentity? LoginRunner(string profileDirectory, string? expectedUserId, CancellationToken cancellation);

public delegate ExperienceResult ExperienceRunner(string profileDirectory, SavedAccount account, string? placeId, CancellationToken cancellation);

/// <summary>
/// Account actions without any presentation. Each method reports what happened through the callback it is given and
/// returns an exit code: 0 done, 1 incomplete, 130 cancelled. Expected failures are thrown, as they always were.
/// </summary>
public interface IAccountOperations
{
    AccountListing List();

    int Add(Action<string> report);

    int SignInAgain(string accountId, Action<string> report);

    int OpenExperience(string accountId, string? placeId, Action<string> report);

    /// <summary>Asks <paramref name="confirm"/> while the store is open, and removes only if it returns true.</summary>
    int Remove(string accountId, Func<SavedAccount, bool> confirm, Action<string> report);

    int CleanUp(Action<string> report);
}

public sealed class AccountOperations(CancellationToken cancellation, string? directory = null,
    LoginRunner? loginRunner = null, ExperienceRunner? experienceRunner = null) : IAccountOperations
{
    private readonly LoginRunner login = loginRunner ?? BrowserLogin.Run;
    private readonly ExperienceRunner experience = experienceRunner ?? BrowserExperience.Run;

    public AccountListing List()
    {
        using var store = Open();
        return new AccountListing(store.Accounts, store.PendingCleanupCount);
    }

    public int Add(Action<string> report) => SignIn(null, report);

    public int SignInAgain(string accountId, Action<string> report) => SignIn(accountId, report);

    public int OpenExperience(string accountId, string? placeId, Action<string> report)
    {
        cancellation.ThrowIfCancellationRequested();
        if (placeId is not null && !ExperienceLaunch.ValidPlaceId(placeId))
            throw new ArgumentException("Enter a positive numeric Roblox place ID, not a URL.");
        // Hold the account-operation lock until the browser is disposed, just as in sign-in.
        using var store = Open();
        var account = store.Get(accountId);
        var profile = store.ProfilePath(account.ProfileId);
        if (!Directory.Exists(profile))
            throw new InvalidOperationException("The saved browser profile is missing. Use Sign in again for this account.");
        var result = experience(profile, account, placeId, cancellation);
        // A cancelled token cannot undo a request already sent to the desktop.
        if (result == ExperienceResult.HandoffRequested)
        {
            report("Desktop handoff requested. RobloxPlayer launch and joined account are unverified.");
            report("Check RobloxPlayer before retrying. This uses the current Windows device and network.");
            return 0;
        }
        cancellation.ThrowIfCancellationRequested();
        report(result == ExperienceResult.Closed
            ? "Experience window closed without a desktop handoff."
            : "The experience request did not complete. Check the browser message and sign in again if needed.");
        return result == ExperienceResult.Closed ? 130 : 1;
    }

    public int Remove(string accountId, Func<SavedAccount, bool> confirm, Action<string> report)
    {
        using var store = Open();
        var account = store.Get(accountId);
        if (!confirm(account)) return 130;
        cancellation.ThrowIfCancellationRequested();
        // The profile becomes ineligible for use first; deleting its files is retried until the browser lets go.
        store.Remove(account.Id);
        report("Local account removed.");
        return FinishCleanup(store, report);
    }

    public int CleanUp(Action<string> report)
    {
        using var store = Open();
        var remaining = store.CleanupPending();
        report(remaining == 0
            ? "Unfinished profile cleanup is complete."
            : $"{remaining} browser profile(s) still need cleanup. Close login windows and retry accounts cleanup.");
        return remaining == 0 ? 0 : 1;
    }

    private int SignIn(string? accountId, Action<string> report)
    {
        using var store = Open();
        var existing = accountId is null ? null : store.Get(accountId);
        cancellation.ThrowIfCancellationRequested();
        var profile = store.ReserveProfile();
        try
        {
            var identity = login(store.ProfilePath(profile), existing?.UserId, cancellation);
            if (identity is null)
            {
                report("Sign-in cancelled. Saved accounts were not changed.");
                return 130;
            }
            cancellation.ThrowIfCancellationRequested();
            var saved = store.CommitLogin(profile, identity, existing?.Id);
            report($"Saved verified Roblox account: {saved.Username} (user {saved.UserId}).");
            report("Local account ID: " + saved.Id);
            return 0;
        }
        finally { FinishCleanup(store, report); }
    }

    private static int FinishCleanup(AccountStore store, Action<string> report)
    {
        var remaining = store.CleanupPending();
        if (remaining == 0) return 0;
        report($"{remaining} browser profile(s) have cleanup pending. Use accounts cleanup after the browser closes.");
        return 1;
    }

    private AccountStore Open() => AccountStore.Open(directory ?? AccountStore.DefaultDirectory);
}
