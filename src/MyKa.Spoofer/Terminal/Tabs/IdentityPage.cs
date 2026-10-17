using MyKa.Spoofer.Identity;

namespace MyKa.Spoofer.Terminal.Tabs;

/// <summary>A visible entry to the existing system window, with its available operations and limits.</summary>
internal sealed class IdentityPage(Action openTools) : ITabPage
{
    private readonly Scroller scroller = new();
    private PageMessage? message;
    private int total;
    private int bodyRows = 1;

    public string Title => "Identity";
    public bool IsModal => false;
    public string Hints => "Enter / O open tools   ↑ ↓ PgUp PgDn scroll";
    public void Activate() { }

    public PageResult Handle(ConsoleKeyInfo key)
    {
        if (key.Key == ConsoleKey.Enter || Hotkey.Is(key, 'o'))
        {
            message = new(Tone.Info, "Opening system identity tools...");
            return new PageResult(true, Open);
        }
        return scroller.Handle(key, total, bodyRows) ? PageResult.Redraw : PageResult.None;
    }

    public IReadOnlyList<Line> Render(int width, int rows)
    {
        var all = new List<Line>();
        foreach (var (title, description) in Sections)
        {
            if (all.Count > 0) all.Add(Line.Empty);
            all.Add(Line.Of(title, Looks.Strong));
            all.AddRange(Cells.Wrap(description, width).Select(text => Line.Of(text)));
        }
        var bottom = message is null ? [] : PageParts.MessageLines([message], width).ToList();
        bottom.Add(Line.Of("[Enter / O] Open system identity tools", Looks.Selected));
        var frame = PageFrame.Arrange(null, bottom, rows, minBody: 1);
        total = all.Count;
        bodyRows = frame.BodyRows;
        scroller.Clamp(total, bodyRows);
        var shown = all.Skip(scroller.Offset).Take(bodyRows).ToArray();
        return frame.Compose(PageParts.Heading("SYSTEM IDENTITY", PageParts.Position(scroller.Offset, shown.Length, total)), shown);
    }

    private void Open()
    {
        try
        {
            openTools();
            message = new(Tone.Info, "System tools closed. Press Enter to reopen.");
        }
        catch (Exception e) when (WindowsIdentityInspection.Expected(e))
        {
            message = new(Tone.Bad, "System tools could not open. Check desktop access and permissions, then press Enter to retry.");
        }
    }

    private static readonly (string Title, string Description)[] Sections =
    [
        ("SNAPSHOTS, COMPARE AND BACKUP", "Capture Before and After, compare actual observations, export or load a backup file, and restore recorded changes from a recovery backup."),
        ("STORED GUID AND MAC SETTINGS", "Review changes to existing MachineGuid, HwProfileGuid and ComputerHardwareId strings, and MAC overrides advertised by adapter drivers. Windows changes require elevation; effective MAC changes may need a restart."),
        ("NETWORK TOOLS", "Flush the DNS cache or renew IPv4 DHCP leases. DHCP can keep the same address. A new public IP is not guaranteed."),
        ("SCOPED ROBLOX CLEANUP", "Quarantine Roblox logs/http cache and clear the backed-up RobloxPlayer preference tree. Close Player and Studio first. Account profiles and installed versions are outside this cleanup."),
        ("INSPECT ONLY", "System UUID, baseboard and drive serials, volume serials, hardware WMI classes and monitor EDID. Firmware spoofing and volume/EDID rewriting are not implemented."),
        ("NOT IMPLEMENTED", "Independent concurrent computer environments, public-IP routing and malformed or hidden registry values. Stored settings do not establish Roblox account unlinkability."),
        ("REVIEW BEFORE APPLYING", "No changes are selected by default. Apply selected shows the plan and writes a recovery backup before changes. Privileged changes and recovery still need testing on a disposable Windows installation."),
    ];
}
