using System.Diagnostics;

namespace MyKa.Spoofer.Diagnostics;

public sealed record ClientCandidate(string DirectoryName, string? FileVersion);
public sealed record InstallationScan(IReadOnlyList<ClientCandidate> Clients, IReadOnlyList<string> Problems);

public static class InstallationDiscovery
{
    // Discovery is intentionally limited to the default per-user desktop layout.
    // File presence is not publisher verification or a compatibility verdict.
    public static InstallationScan Read(string versionsDirectory)
    {
        var clients = new List<ClientCandidate>();
        var problems = new List<string>();
        if (!ExistsWithoutLink(versionsDirectory, directory: true, problems)) return new(clients, problems);
        try
        {
            foreach (var directory in Directory.EnumerateDirectories(versionsDirectory, "version-*"))
            {
                if (!ExistsWithoutLink(directory, directory: true, problems)) continue;
                var executable = Path.Combine(directory, "RobloxPlayerBeta.exe");
                if (!ExistsWithoutLink(executable, directory: false, problems)) continue;
                string? version = null;
                try { version = FileVersionInfo.GetVersionInfo(executable).FileVersion; }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException
                    or System.ComponentModel.Win32Exception)
                {
                    problems.Add("A client file's version could not be read.");
                }
                clients.Add(new(Path.GetFileName(directory), version));
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            problems.Add("The default Roblox installation directory could not be fully inspected.");
        }
        return new(clients.OrderBy(c => c.DirectoryName, StringComparer.OrdinalIgnoreCase).ToArray(), problems);
    }

    private static bool ExistsWithoutLink(string path, bool directory, List<string> problems)
    {
        try
        {
            var attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                problems.Add("A linked installation entry was skipped.");
                return false;
            }
            return ((attributes & FileAttributes.Directory) != 0) == directory;
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException) { return false; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            problems.Add("An installation entry could not be inspected.");
            return false;
        }
    }
}
