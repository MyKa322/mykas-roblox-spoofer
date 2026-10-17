using MyKa.Spoofer.Accounts;

namespace MyKa.Spoofer;

public enum Command { Menu, Help, Logo, Doctor, Inspect, AccountsList, AccountsAdd, AccountsLogin, AccountsRemove, AccountsCleanup, AccountsPlay, IdentityManage, IdentitySnapshot, IdentityCompare }

public sealed record CommandLine(Command Command, bool Plain, string? AccountId = null, bool Still = false, string? PlaceId = null,
    string? OutputPath = null, string? BeforePath = null, string? AfterPath = null)
{
    public static CommandLine Parse(string[] args)
    {
        var plain = false;
        var still = false;
        var words = new List<string>();
        foreach (var arg in args)
        {
            if (arg == "--plain")
            {
                if (plain) throw new ArgumentException("--plain was supplied more than once.");
                plain = true;
            }
            else if (arg == "--still")
            {
                if (still) throw new ArgumentException("--still was supplied more than once.");
                still = true;
            }
            else words.Add(arg);
        }

        string? accountId = null;
        string? placeId = null;
        string? output = null;
        string? before = null;
        string? after = null;
        var command = words.ToArray() switch
        {
            [] => Command.Menu,
            ["--help"] or ["help"] => Command.Help,
            ["logo"] => Command.Logo,
            ["doctor"] => Command.Doctor,
            ["identity", "inspect"] => Command.Inspect,
            ["identity", "manage"] => Command.IdentityManage,
            ["identity", "snapshot", "--output", var path] => WithOutput(path, out output),
            ["identity", "compare", "--before", var first, "--after", var last] => WithSnapshots(first, last, out before, out after),
            ["accounts", "list"] => Command.AccountsList,
            ["accounts", "add"] => Command.AccountsAdd,
            ["accounts", "cleanup"] => Command.AccountsCleanup,
            ["accounts", "login", "--account", var id] => WithAccount(Command.AccountsLogin, id, out accountId),
            ["accounts", "remove", "--account", var id] => WithAccount(Command.AccountsRemove, id, out accountId),
            ["accounts", "play", "--account", var id, "--place", var place]
                => WithPlace(id, place, out accountId, out placeId),
            ["accounts", "play", "--place", var place, "--account", var id]
                => WithPlace(id, place, out accountId, out placeId),
            _ => throw new ArgumentException("Unknown command or option. Use --help.")
        };
        return new CommandLine(command, plain, accountId, still, placeId, output, before, after);
    }

    private static Command WithOutput(string path, out string output)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Any(char.IsControl)) throw new ArgumentException("Choose a valid snapshot output path.");
        output = path;
        return Command.IdentitySnapshot;
    }

    private static Command WithSnapshots(string first, string last, out string before, out string after)
    {
        WithOutput(first, out before);
        WithOutput(last, out after);
        return Command.IdentityCompare;
    }

    private static Command WithPlace(string id, string place, out string accountId, out string placeId)
    {
        WithAccount(Command.AccountsPlay, id, out accountId);
        if (!ExperienceLaunch.ValidPlaceId(place)) throw new ArgumentException("Enter a positive numeric Roblox place ID, not a URL.");
        placeId = place;
        return Command.AccountsPlay;
    }

    private static Command WithAccount(Command command, string input, out string accountId)
    {
        if (!AccountStore.ValidLocalId(input)) throw new ArgumentException("Invalid local account ID. Use accounts list.");
        accountId = input;
        return command;
    }
}
