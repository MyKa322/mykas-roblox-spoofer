using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MyKa.Spoofer.Accounts;

public sealed record SavedAccount(string Id, string ProfileId, string UserId, string Username,
    string DisplayName, DateTimeOffset LastVerifiedUtc);
public sealed record AccountDocument(int SchemaVersion, SavedAccount[] Accounts, string[] PendingProfiles);

public sealed class AccountStore : IDisposable
{
    private const long MaxMetadataBytes = 1_048_576;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    private readonly string root;
    private readonly FileStream operationLock;
    private AccountDocument document;
    public IReadOnlyList<SavedAccount> Accounts => document.Accounts.ToArray();
    public int PendingCleanupCount => document.PendingProfiles.Length;

    public static string DefaultDirectory
    {
        get
        {
            var parent = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(parent)) throw new IOException("Local application data is unavailable.");
            return Path.Combine(parent, "MyKa", "RobloxSpoofer");
        }
    }

    public static AccountStore Open(string directory)
    {
        var root = Path.GetFullPath(directory);
        RejectLinkedAncestors(root);
        var info = Directory.CreateDirectory(root);
        var user = WindowsIdentity.GetCurrent().User ?? throw new IOException("The current Windows user is unavailable.");
        var acl = new DirectorySecurity();
        acl.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        foreach (var sid in new[] { user, new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null) })
            acl.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None, AccessControlType.Allow));
        info.SetAccessControl(acl);

        var lockPath = Path.Combine(root, "accounts.lock");
        RejectLinkedFile(lockPath);
        FileStream operationLock;
        try { operationLock = new(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException) { throw new IOException("Another account operation may be open. Close it and retry."); }
        try { return new AccountStore(root, operationLock); }
        catch { operationLock.Dispose(); throw; }
    }

    private AccountStore(string root, FileStream operationLock)
    {
        this.root = root;
        this.operationLock = operationLock;
        var file = Path.Combine(root, "accounts.json");
        RejectLinkedFile(file);
        RejectLinkedFile(file + ".bak");
        if (!File.Exists(file))
        {
            if (File.Exists(file + ".bak"))
                throw new InvalidDataException("Account metadata is missing but a backup exists. Restore it before continuing.");
            document = new(1, [], []);
            return;
        }
        if (new FileInfo(file).Length > MaxMetadataBytes) throw new InvalidDataException("Account metadata is too large.");
        try
        {
            document = JsonSerializer.Deserialize<AccountDocument>(File.ReadAllText(file), JsonOptions)
                ?? throw new JsonException();
            Validate(document);
        }
        catch (Exception e) when (e is JsonException or ArgumentException)
        {
            throw new InvalidDataException("Account metadata is invalid. The file was preserved; recover accounts.json from its backup.");
        }
    }

    public static bool ValidLocalId(string? value) => value is { Length: 32 }
        && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    public SavedAccount Get(string id) => document.Accounts.SingleOrDefault(a => a.Id == id)
        ?? throw new ArgumentException("That account was not found. Use accounts list.");

    public string ReserveProfile()
    {
        var id = Guid.NewGuid().ToString("N");
        Save(document with { PendingProfiles = [.. document.PendingProfiles, id] });
        Directory.CreateDirectory(ProfilePath(id));
        return id;
    }

    public string ProfilePath(string id)
    {
        if (!ValidLocalId(id)) throw new ArgumentException("Invalid local profile ID.");
        var parent = Path.Combine(root, "profiles");
        RejectLinkedAncestors(parent);
        var target = Path.GetFullPath(Path.Combine(parent, id));
        if (!target.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new IOException("Profile path is outside the application data directory.");
        RejectLinkedAncestors(target);
        return target;
    }

    public SavedAccount CommitLogin(string profileId, RobloxIdentity identity, string? existingId = null)
    {
        if (!document.PendingProfiles.Contains(profileId) || !Directory.Exists(ProfilePath(profileId)))
            throw new InvalidOperationException("The login profile was not reserved.");
        if (!RobloxIdentity.ValidUserId(identity.UserId) || !RobloxIdentity.ValidName(identity.Username)
            || !RobloxIdentity.ValidName(identity.DisplayName)) throw new InvalidDataException("Invalid verified identity.");

        var existing = existingId is null ? null : Get(existingId);
        if (existing is not null && existing.UserId != identity.UserId)
            throw new InvalidOperationException("This is a different Roblox account. The saved account was not changed.");
        if (document.Accounts.Any(a => a.UserId == identity.UserId && a.Id != existingId))
            throw new InvalidOperationException("This Roblox account is already saved. Use Sign in again for that account.");

        var account = new SavedAccount(existing?.Id ?? Guid.NewGuid().ToString("N"), profileId,
            identity.UserId, identity.Username, identity.DisplayName, DateTimeOffset.UtcNow);
        var pending = document.PendingProfiles.Where(id => id != profileId).ToList();
        if (existing is not null) pending.Add(existing.ProfileId);
        Save(new(1, [.. document.Accounts.Where(a => a.Id != existingId), account], pending.ToArray()));
        return account;
    }

    public void Remove(string id)
    {
        var account = Get(id);
        // Make the profile ineligible for use before attempting credential cleanup.
        Save(document with
        {
            Accounts = document.Accounts.Where(a => a.Id != id).ToArray(),
            PendingProfiles = [.. document.PendingProfiles, account.ProfileId]
        });
    }

    public int CleanupPending()
    {
        foreach (var id in document.PendingProfiles.ToArray())
        {
            var target = ProfilePath(id);
            var deleted = false;
            for (var attempt = 0; attempt < 5 && !deleted; attempt++)
            {
                try
                {
                    if (Directory.Exists(target))
                    {
                        RejectLinkedTree(target);
                        Directory.Delete(target, recursive: true);
                    }
                    deleted = true;
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    if (attempt < 4) Thread.Sleep(200);
                }
            }
            if (deleted) Save(document with { PendingProfiles = document.PendingProfiles.Where(p => p != id).ToArray() });
        }
        return PendingCleanupCount;
    }

    private void Save(AccountDocument next)
    {
        Validate(next);
        var file = Path.Combine(root, "accounts.json");
        var backup = file + ".bak";
        RejectLinkedAncestors(root);
        RejectLinkedFile(file);
        RejectLinkedFile(backup);
        var temporary = Path.Combine(root, "accounts." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(output, next, JsonOptions);
                if (output.Length > MaxMetadataBytes)
                    throw new InvalidDataException("Account metadata is too large. No data was changed.");
                output.Flush(flushToDisk: true);
            }
            if (File.Exists(file)) File.Replace(temporary, file, backup);
            else File.Move(temporary, file);
            document = next;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static void Validate(AccountDocument data)
    {
        if (data.SchemaVersion != 1) throw new InvalidDataException("Unsupported account data version. No data was changed.");
        if (data.Accounts is null || data.PendingProfiles is null) throw new InvalidDataException("Incomplete account metadata.");
        var ids = new HashSet<string>();
        var profiles = new HashSet<string>();
        var users = new HashSet<string>();
        foreach (var account in data.Accounts)
        {
            if (account is null || !ValidLocalId(account.Id) || !ValidLocalId(account.ProfileId)
                || !RobloxIdentity.ValidUserId(account.UserId) || !RobloxIdentity.ValidName(account.Username)
                || !RobloxIdentity.ValidName(account.DisplayName) || account.LastVerifiedUtc == default
                || !ids.Add(account.Id) || !profiles.Add(account.ProfileId) || !users.Add(account.UserId))
                throw new InvalidDataException("Invalid or duplicate account metadata. No data was changed.");
        }
        foreach (var id in data.PendingProfiles)
            if (!ValidLocalId(id) || !profiles.Add(id)) throw new InvalidDataException("Invalid pending profile metadata.");
    }

    private static void RejectLinkedFile(string path)
    {
        if ((File.Exists(path) || Directory.Exists(path)) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Linked account data is not supported.");
    }

    private static void RejectLinkedAncestors(string path)
    {
        for (var current = new DirectoryInfo(path); current is not null; current = current.Parent)
            if (current.Exists && (current.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Linked account directories are not supported.");
    }

    private static void RejectLinkedTree(string directory)
    {
        foreach (var entry in new DirectoryInfo(directory).EnumerateFileSystemInfos())
        {
            if ((entry.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Linked profile data was preserved.");
            if ((entry.Attributes & FileAttributes.Directory) != 0) RejectLinkedTree(entry.FullName);
        }
    }

    public void Dispose() => operationLock.Dispose();
}
