using MyKa.Spoofer.Terminal;

namespace MyKa.Spoofer.Accounts;

public sealed class AccountCommands(TerminalUi ui, CancellationToken cancellation)
{
    private readonly AccountOperations operations = new(cancellation);

    public int Execute(Command command, string? accountId = null, string? placeId = null)
    {
        if (command is Command.AccountsAdd or Command.AccountsLogin or Command.AccountsRemove or Command.AccountsPlay && !ui.Interactive)
            throw new ArgumentException("This account operation needs an interactive terminal.");

        switch (command)
        {
            case Command.AccountsList:
                using (var store = AccountStore.Open(AccountStore.DefaultDirectory)) ui.Accounts(store);
                return 0;
            case Command.AccountsCleanup:
                return operations.CleanUp(ui.Message);
            case Command.AccountsRemove:
                return operations.Remove(accountId!, ConfirmRemoval, ui.Message);
            case Command.AccountsLogin:
                return operations.SignInAgain(accountId!, ui.Message);
            case Command.AccountsPlay:
                return operations.OpenExperience(accountId!, placeId, ui.Message);
            default:
                return operations.Add(ui.Message);
        }
    }

    private bool ConfirmRemoval(SavedAccount account)
    {
        ui.Message($"Remove {account.Username} (Roblox user {account.UserId}) from this application?");
        ui.Message("This removes its local browser session. Other Roblox sessions are not revoked.");
        return ui.Choose("Remove local account?", ["Keep account", "Remove account"]) == "Remove account";
    }

    public void Menu()
    {
        while (true)
        {
            cancellation.ThrowIfCancellationRequested();
            var choice = ui.Choose("Accounts", ["List accounts", "Add account", "Open experience", "Sign in again",
                "Remove account", "Clean up unfinished profiles", "Back"]);
            if (choice is null or "Back") return;
            try
            {
                switch (choice)
                {
                    case "List accounts": Execute(Command.AccountsList); break;
                    case "Add account": Execute(Command.AccountsAdd); break;
                    case "Clean up unfinished profiles": Execute(Command.AccountsCleanup); break;
                    case "Sign in again":
                    case "Open experience":
                    case "Remove account":
                        var accounts = operations.List().Accounts;
                        if (accounts.Count == 0) { ui.Message("No saved accounts."); break; }
                        var labels = accounts.Select(a => $"{a.Username} | {a.UserId} | {a.Id}").ToArray();
                        var selected = ui.Choose("Select account", [.. labels, "Back"]);
                        var index = Array.IndexOf(labels, selected);
                        if (index >= 0) Execute(choice switch
                        {
                            "Sign in again" => Command.AccountsLogin,
                            "Open experience" => Command.AccountsPlay,
                            _ => Command.AccountsRemove
                        }, accounts[index].Id);
                        break;
                }
            }
            catch (InvalidDataException e) { ui.Error(e.Message); }
            catch (InvalidOperationException e) { ui.Error(e.Message); }
            catch (ArgumentException e) { ui.Error(e.Message); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                ui.Error("Account data could not be accessed. Close other account operations and check local permissions.");
            }
        }
    }
}
