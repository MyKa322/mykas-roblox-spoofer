using MyKa.Spoofer.Accounts;
using MyKa.Spoofer.Diagnostics;
using MyKa.Spoofer.Identity;
using MyKa.Spoofer.Terminal;

namespace MyKa.Spoofer;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler cancel = (_, e) => { e.Cancel = true; cancellation.Cancel(); };
        Console.CancelKeyPress += cancel;
        try
        {
            Console.OutputEncoding = new System.Text.UTF8Encoding(false);
            if (!Console.IsInputRedirected) Console.InputEncoding = new System.Text.UTF8Encoding(false);
            var options = CommandLine.Parse(args);
            var ui = new TerminalUi(options.Plain, cancellation.Token, options.Still);
            if (options.Command == Command.Help) { ui.Help(); return 0; }
            if (options.Command == Command.Logo) { ui.Logo(); return 0; }
            if (!OperatingSystem.IsWindows())
            {
                ui.Error("This application supports Windows only.");
                return 3;
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            if (options.Command == Command.Doctor) return ui.Doctor(SystemDiagnostics.Read());
            if (options.Command == Command.Inspect) return ui.Adapters(SystemDiagnostics.ReadAdapters());
            if (options.Command == Command.IdentitySnapshot)
            {
                var snapshot = WindowsIdentityInspection.Read(cancellation.Token).GetAwaiter().GetResult();
                IdentityBackupFiles.WriteNew(options.OutputPath!, snapshot);
                ui.Message("Identity snapshot saved locally. It contains device identifiers. No settings were changed.");
                return snapshot.Values.Any(v => v.Problem is not null) ? 1 : 0;
            }
            if (options.Command == Command.IdentityCompare)
            {
                var differences = IdentitySnapshots.Compare(IdentitySnapshots.Read(options.BeforePath!), IdentitySnapshots.Read(options.AfterPath!));
                foreach (var value in differences)
                    ui.Message($"{value.Kind} | {value.Label} | {value.Before ?? "<not present>"} -> {value.After ?? "<not present>"}");
                return differences.Any(v => v.Kind == DifferenceKind.Unavailable) ? 1 : 0;
            }
            if (options.Command == Command.IdentityManage)
            {
                if (!ui.Interactive) throw new ArgumentException("System identity changes require an interactive terminal. Use identity snapshot for read-only export.");
                IdentityWindow.Run(cancellation.Token);
                return 0;
            }
            if (options.Command is Command.AccountsList or Command.AccountsAdd or Command.AccountsLogin
                or Command.AccountsRemove or Command.AccountsCleanup or Command.AccountsPlay)
                return new AccountCommands(ui, cancellation.Token).Execute(options.Command, options.AccountId, options.PlaceId);
            if (!ui.Interactive)
            {
                ui.Error("The menu needs an interactive terminal. Use a command or --help.");
                return 2;
            }
            return ui.Menu();
        }
        catch (OperationCanceledException) { return 130; }
        catch (ArgumentException e) { Console.Error.WriteLine(TerminalUi.SafeText(e.Message)); return 2; }
        catch (InvalidDataException e) { Console.Error.WriteLine(TerminalUi.SafeText(e.Message)); return 1; }
        catch (InvalidOperationException e) { Console.Error.WriteLine(TerminalUi.SafeText(e.Message)); return 1; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            Console.Error.WriteLine("The operation could not complete. Close other account operations and check local access.");
            return 1;
        }
        finally { Console.CancelKeyPress -= cancel; }
    }
}
