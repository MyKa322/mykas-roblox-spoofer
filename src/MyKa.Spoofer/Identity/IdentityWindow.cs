using MyKa.Spoofer.Branding;
using MyKa.Spoofer.Terminal;

namespace MyKa.Spoofer.Identity;

/// <summary>One responsive window for observation, reviewed operations, and recoverable results.</summary>
internal sealed class IdentityWindow : Form
{
    private readonly DataGridView table = new()
    {
        Name = "identityValues", Dock = DockStyle.Fill, ReadOnly = true,
        AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false,
        RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false,
        ScrollBars = ScrollBars.Both, AccessibleName = "System identity before and after values",
    };
    private readonly TabControl tabs = new IdentityTabs { Name = "identityTabs", Dock = DockStyle.Fill, DrawMode = TabDrawMode.OwnerDrawFixed };
    private readonly TabPage snapshotsTab = new("Snapshots") { Name = "snapshotsTab", Padding = new Padding(10) };
    private readonly TabPage operationsTab = new("Operations") { Name = "operationsTab", Padding = new Padding(10) };
    private readonly TabPage resultsTab = new("Results") { Name = "resultsTab", Padding = new Padding(10) };
    private readonly TextBox results = new()
    {
        Name = "operationResults", Dock = DockStyle.Fill, Multiline = true, ReadOnly = true,
        ScrollBars = ScrollBars.Vertical, WordWrap = true, AccessibleName = "Operation results and recovery information",
    };
    private readonly Label summary = new() { Name = "identityStatus", Dock = DockStyle.Fill, AutoSize = true, AccessibleName = "Current operation status" };
    private readonly Label snapshotTimes = new() { Name = "snapshotTimes", Dock = DockStyle.Fill, AutoSize = true };
    private readonly Label progress = new() { Name = "identityProgress", Dock = DockStyle.Fill, AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, AccessibleName = "Operation progress" };
    private readonly Button cancelWork = Button("cancelOperation", "Cancel current");
    private readonly Button close = Button("closeIdentity", "Close");
    private readonly Button compare = Button("compareSnapshots", "&Compare Before / After");
    private readonly Button export = Button("exportSnapshot", "Create &backup file");
    private readonly Button apply = Button("applyOperations", "&Apply selected (review first)");
    private readonly CheckBox machine = Choice("machineGuid", "MachineGuid");
    private readonly CheckBox profile = Choice("profileGuid", "HwProfileGuid (profile 0001)");
    private readonly CheckBox hardware = Choice("hardwareId", "Stored ComputerHardwareId");
    private readonly CheckBox mac = Choice("adapterAddresses", "All driver-supported MAC overrides");
    private readonly CheckBox dns = Choice("flushDns", "Flush DNS cache");
    private readonly CheckBox dhcp = Choice("renewDhcp", "Renew IPv4 DHCP leases");
    private readonly CheckBox cache = Choice("cleanCache", "Quarantine Roblox logs + http cache");
    private readonly CheckBox playerRegistry = Choice("cleanRegistry", "Clear backed-up HKCU RobloxPlayer settings");
    private readonly List<Button> actionButtons = [];
    private readonly CancellationTokenSource lifetime;
    private readonly Func<CancellationToken, Task<IdentitySnapshot>> readSnapshot;
    private readonly bool administrator;
    private readonly Font bodyFont = new("Consolas", 10);
    private readonly Font titleFont = new("Consolas", 15, FontStyle.Bold);
    private CancellationTokenSource? operation;
    private IdentitySnapshot? before;
    private IdentitySnapshot? after;
    private bool closingRequested;
    private bool resourcesDisposed;
    internal Task PendingWork { get; private set; } = Task.CompletedTask;
    internal bool IsWorking => operation is not null;

    internal IdentityWindow(CancellationToken token, Func<CancellationToken, Task<IdentitySnapshot>>? readSnapshot = null,
        bool? administrator = null)
    {
        lifetime = CancellationTokenSource.CreateLinkedTokenSource(token);
        this.readSnapshot = readSnapshot ?? WindowsIdentityInspection.Read;
        this.administrator = administrator ?? IdentityBackupFiles.IsAdministrator;
        Name = "systemIdentity";
        Text = "MyKa - System Identity";
        Font = bodyFont;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96, 96);
        Size = new Size(1180, 800);
        MinimumSize = new Size(760, 540);
        StartPosition = FormStartPosition.CenterScreen;
        AutoValidate = AutoValidate.Disable;
        var root = CreateLayout("identityLayout", 3);
        root.Padding = new Padding(16);
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var heading = new TableLayoutPanel { Name = "identityHeading", Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 1, RowCount = 2 };
        heading.Controls.Add(new Label { AutoSize = true, Dock = DockStyle.Fill, Text = LogoText.Title + "  v" + TerminalUi.Version, Font = titleFont }, 0, 0);
        heading.Controls.Add(new Label
        {
            Name = "identitySection", AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 4, 0, 12),
            Text = "[ SYSTEM IDENTITY ]  |  " + (this.administrator ? "Administrator" : "Standard user"),
        }, 0, 1);
        root.Controls.Add(heading, 0, 0);
        tabs.TabPages.AddRange([snapshotsTab, operationsTab, resultsTab]);
        tabs.DrawItem += IdentityTheme.DrawTab;
        tabs.FontChanged += (_, _) => SizeTabs();
        SizeTabs();
        BuildSnapshots();
        BuildOperations();
        resultsTab.Controls.Add(results);
        root.Controls.Add(tabs, 0, 1);
        root.Controls.Add(BuildFooter(), 0, 2);
        Controls.Add(root);
        CancelButton = close;
        close.Click += (_, _) => Close();
        cancelWork.Click += (_, _) =>
        {
            operation?.Cancel();
            summary.Text = "Cancellation requested. Waiting for the current step; this window will stay open.";
        };
        FormClosing += HandleClosing;
        IdentityTheme.Apply(this);
        Controls.Find("identitySection", true)[0].ForeColor = IdentityTheme.Accent;
        snapshotTimes.ForeColor = IdentityTheme.Dim;
        FontChanged += (_, _) => SizeForFont();
        UpdateActions();
        UpdateTimes();
        Record("Ready. Capture Before to begin.", "No changes are selected. Firmware identifiers remain inspect only. Results and recovery paths stay here until the window closes.");
    }

    private static TableLayoutPanel CreateLayout(string name, int rows) => new()
    {
        Name = name, Dock = DockStyle.Fill, ColumnCount = 1, RowCount = rows,
        ColumnStyles = { new ColumnStyle(SizeType.Percent, 100) },
    };

    private static Button Button(string name, string text) => new IdentityButton
    {
        Name = name, Text = text, AccessibleName = text.Replace("&", "", StringComparison.Ordinal),
        AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Fill,
        Padding = new Padding(10, 6, 10, 6), Margin = new Padding(4),
    };

    private static CheckBox Choice(string name, string text) => new IdentityCheckBox
    {
        Name = name, Text = text, AccessibleName = text, AutoSize = true, Margin = new Padding(4, 8, 12, 8),
    };

    private void BuildSnapshots()
    {
        var page = CreateLayout("snapshotLayout", 3);
        page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        page.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var toolbar = new TableLayoutPanel { Name = "snapshotActions", Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 3, RowCount = 2 };
        for (var column = 0; column < 3; column++) toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3));
        var captureBefore = Button("captureBefore", "Capture &Before");
        var captureAfter = Button("captureAfter", "Capture &After");
        var load = Button("loadSnapshot", "&Load Before / backup");
        var restore = Button("restoreChanges", "&Restore changes");
        Button[] buttons = [captureBefore, captureAfter, compare, export, load, restore];
        for (var index = 0; index < buttons.Length; index++) toolbar.Controls.Add(buttons[index], index % 3, index / 3);
        Bind(captureBefore, "Capturing Before...", async token =>
        {
            var snapshot = await Task.Run(() => readSnapshot(token), token);
            token.ThrowIfCancellationRequested();
            IdentitySnapshots.Validate(snapshot);
            ShowSnapshot(snapshot, false);
            before = snapshot;
            after = null;
            Record("Before captured.", "Previous After was cleared. Export the Before backup if you want to keep it after closing this window.");
        });
        Bind(captureAfter, "Capturing After...", async token =>
        {
            var snapshot = await Task.Run(() => readSnapshot(token), token);
            token.ThrowIfCancellationRequested();
            IdentitySnapshots.Validate(snapshot);
            if (before is not null) ShowComparison(before, snapshot);
            else ShowSnapshot(snapshot, true);
            after = snapshot;
            Record("After captured.", "Unavailable observations never count as successful changes. Effective MAC and configured override remain separate observations.");
        });
        Bind(compare, "Comparing snapshots...", _ =>
        {
            if (before is null || after is null) throw new InvalidOperationException("Capture or load Before and capture After first.");
            ShowComparison(before, after);
            Record("Snapshots compared.", "Before and After values are displayed together. A stored setting change does not establish a different physical device.");
            return Task.CompletedTask;
        });
        Bind(export, "Exporting snapshot backup...", Export);
        Bind(load, "Loading Before...", LoadBefore);
        Bind(restore, "Preparing recovery...", async token =>
        {
            using var dialog = OpenFile();
            if (dialog.ShowDialog(this) != DialogResult.OK) { Record("Restore cancelled before any changes."); return; }
            var path = dialog.FileName;
            var result = await Task.Run(() => IdentityOperations.Restore(path, text => Confirm(text, token), token), token);
            ShowResult(result);
        });
        page.Controls.Add(toolbar, 0, 0);
        page.Controls.Add(snapshotTimes, 0, 1);
        foreach (var (name, minimum, weight) in new[]
        {
            ("Identifier", 160, 25f), ("Before", 180, 23f), ("After", 180, 23f),
            ("Result", 115, 12f), ("Scope / availability", 220, 30f),
        }) table.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = name, MinimumWidth = minimum, FillWeight = weight, SortMode = DataGridViewColumnSortMode.NotSortable,
        });
        page.Controls.Add(table, 0, 2);
        snapshotsTab.Controls.Add(page);
    }

    private void BuildOperations()
    {
        var page = CreateLayout("operationsLayout", 3);
        page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        page.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        page.Controls.Add(new Label
        {
            Name = "operationScope", Dock = DockStyle.Fill, AutoSize = true, Margin = new Padding(4, 6, 4, 12),
            Text = "Select operations, then review the proposed changes. A recovery backup is written before changes.\n"
                + (administrator ? "Adapter restart/reboot is manual. " : "Stored GUID and network changes require an elevated terminal. ")
                + "Close RobloxPlayer and Studio before cleanup. Public-IP routing and firmware changes are outside these tools.",
        }, 0, 0);
        var choices = new FlowLayoutPanel
        {
            Name = "operationChoices", Dock = DockStyle.Fill, AutoScroll = true,
            FlowDirection = FlowDirection.TopDown, WrapContents = false,
        };
        foreach (var box in Choices())
        {
            choices.Controls.Add(box);
            box.CheckedChanged += (_, _) => UpdateActions();
        }
        page.Controls.Add(choices, 0, 1);
        Bind(apply, "Preparing selected operations...", async token =>
        {
            var selected = SelectedOptions();
            var result = await Task.Run(() => IdentityOperations.Apply(selected, text => Confirm(text, token), token), token);
            ShowResult(result);
        });
        page.Controls.Add(apply, 0, 2);
        operationsTab.Controls.Add(page);
    }

    private TableLayoutPanel BuildFooter()
    {
        var footer = new TableLayoutPanel { Name = "identityFooter", Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 4, RowCount = 2, Padding = new Padding(0, 10, 0, 0) };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var column = 0; column < 3; column++) footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footer.Controls.Add(summary, 0, 0);
        footer.SetColumnSpan(summary, 4);
        var viewResults = Button("viewResults", "Results");
        viewResults.Click += (_, _) => tabs.SelectedTab = resultsTab;
        footer.Controls.Add(progress, 0, 1);
        footer.Controls.Add(viewResults, 1, 1);
        footer.Controls.Add(cancelWork, 2, 1);
        footer.Controls.Add(close, 3, 1);
        return footer;
    }

    private CheckBox[] Choices() => [machine, profile, hardware, mac, dns, dhcp, cache, playerRegistry];
    private IdentityOptions SelectedOptions() => new(machine.Checked, mac.Checked, dns.Checked, dhcp.Checked,
        cache.Checked, playerRegistry.Checked, profile.Checked, hardware.Checked);

    private void Bind(Button button, string workingMessage, Func<CancellationToken, Task> action)
    {
        actionButtons.Add(button);
        button.Click += (_, _) => { if (!IsWorking) PendingWork = Work(workingMessage, action); };
    }

    private async Task Work(string workingMessage, Func<CancellationToken, Task> action)
    {
        if (IsWorking || closingRequested || IsDisposed || lifetime.IsCancellationRequested) return;
        using var current = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        operation = current;
        try
        {
            UpdateActions();
            summary.Text = workingMessage;
            await action(current.Token);
        }
        catch (OperationCanceledException) { Record("Operation cancelled. Window ready.", "Any completed system steps remain in effect. Previous snapshots are retained unless a result was returned. Read Results and inspect After before retrying."); }
        catch (Exception e)
        {
            var detail = e is InvalidDataException or InvalidOperationException or ArgumentException ? e.Message
                : WindowsIdentityInspection.Expected(e) ? "Check local access and permissions. Keep any recovery backup and capture After before retrying."
                : "Unexpected operation failure (" + e.GetType().Name + "). Controls are available again. Keep the recovery backup and inspect current state before retrying.";
            Record("Operation did not complete. Window ready.", detail, failure: true);
        }
        finally
        {
            operation = null;
            if (!IsDisposed)
            {
                UpdateTimes();
                UpdateActions();
                if (closingRequested && IsHandleCreated) BeginInvoke((Action)(() => Close()));
            }
        }
    }

    private void UpdateActions()
    {
        var idle = !IsWorking && !closingRequested;
        foreach (var button in actionButtons) button.Enabled = idle;
        compare.Enabled = idle && before is not null && after is not null;
        export.Enabled = idle && before is not null;
        foreach (var box in Choices()) box.Enabled = idle && (administrator || box == cache || box == playerRegistry);
        apply.Enabled = idle && SelectedOptions().Any;
        cancelWork.Enabled = IsWorking && !closingRequested;
        CancelButton = IsWorking ? cancelWork : close;
        progress.Text = closingRequested ? "[ CLOSING ]" : IsWorking ? "[ WORKING ]" : "[ READY ]";
        progress.ForeColor = IdentityTheme.Accent;
    }

    private void UpdateTimes()
    {
        snapshotTimes.Text = "Before: " + (before?.CapturedUtc.ToString("u") ?? "not captured")
            + "\nAfter:  " + (after?.CapturedUtc.ToString("u") ?? "not captured");
    }

    private void Record(string message, string? detail = null, bool failure = false)
    {
        if (IsDisposed || Disposing) return;
        summary.Text = message;
        summary.ForeColor = failure ? IdentityTheme.Warning : IdentityTheme.Foreground;
        results.AppendText($"[{DateTimeOffset.UtcNow:u}] {message}\r\n" + (detail is null ? "" : detail + "\r\n") + "\r\n");
        if (failure) tabs.SelectedTab = resultsTab;
    }

    private async Task Export(CancellationToken token)
    {
        var snapshot = before ?? throw new InvalidOperationException("Capture Before first.");
        using var dialog = new SaveFileDialog
        {
            Filter = "Identity snapshot (*.json)|*.json", FileName = "myka-before.snapshot.json",
            AddExtension = true, DefaultExt = "json", OverwritePrompt = false,
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) { Record("Backup export cancelled."); return; }
        var path = dialog.FileName;
        if (File.Exists(path)) throw new InvalidOperationException("Choose a new filename. Existing backup files are never overwritten.");
        await Task.Run(() => { token.ThrowIfCancellationRequested(); IdentityBackupFiles.WriteNew(path, snapshot); }, token);
        Record("Snapshot backup exported.", "The snapshot contains device identifiers. Apply selected separately creates the recovery backup for its actual settings and folders.");
    }

    private async Task LoadBefore(CancellationToken token)
    {
        using var dialog = OpenFile();
        if (dialog.ShowDialog(this) != DialogResult.OK) { Record("Loading Before cancelled."); return; }
        var path = dialog.FileName;
        var snapshot = await Task.Run(() =>
        {
            token.ThrowIfCancellationRequested();
            try { return IdentitySnapshots.Read(path); }
            catch (InvalidDataException) { return IdentityBackupFiles.Read(path).Before; }
        }, token);
        token.ThrowIfCancellationRequested();
        if (!string.Equals(snapshot.Computer, Environment.MachineName, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("This snapshot names another computer.");
        ShowSnapshot(snapshot, false);
        before = snapshot;
        after = null;
        Record("Before loaded.", "Capture After to compare it with this baseline.");
    }

    private static OpenFileDialog OpenFile() => new()
    {
        Filter = "Identity JSON (*.json)|*.json", CheckFileExists = true,
        InitialDirectory = Directory.Exists(IdentityBackupFiles.DirectoryPath) ? IdentityBackupFiles.DirectoryPath : "",
    };

    private void ShowResult(IdentityOperationResult? result)
    {
        if (result is null) { Record("Review cancelled. No changes applied."); return; }
        if (result.After is not null) ShowComparison(result.Before, result.After);
        else ShowSnapshot(result.Before, false);
        before = result.Before;
        after = result.After;
        Record("Operation returned. Inspect Results and actual values.", "Recovery backup: " + result.BackupPath
            + Environment.NewLine + string.Join(Environment.NewLine, result.Messages));
        tabs.SelectedTab = resultsTab;
    }

    private void ShowSnapshot(IdentitySnapshot snapshot, bool afterColumn)
    {
        IdentitySnapshots.Validate(snapshot);
        var rows = snapshot.Values.Select(value => new object[]
        {
            value.Label, afterColumn ? "" : value.Value ?? "Unavailable", afterColumn ? value.Value ?? "Unavailable" : "",
            value.Problem is null ? "Observed" : "Unavailable", value.Problem ?? value.Scope,
        }).ToArray();
        FillRows(rows);
    }

    private void ShowComparison(IdentitySnapshot first, IdentitySnapshot last)
    {
        var rows = IdentitySnapshots.Compare(first, last).Select(value => new object[]
        {
            value.Label, value.Before ?? "<not present>", value.After ?? "<not present>", value.Kind.ToString(), "Observed values only",
        }).ToArray();
        FillRows(rows);
    }

    private void FillRows(object[][] values)
    {
        table.SuspendLayout();
        try
        {
            table.Rows.Clear();
            foreach (var value in values) table.Rows.Add(value);
        }
        finally { table.ResumeLayout(); }
    }

    private void HandleClosing(object? sender, FormClosingEventArgs e)
    {
        if (!IsWorking) return;
        e.Cancel = true;
        closingRequested = true;
        operation!.Cancel();
        UpdateActions();
        summary.Text = "Closing after the current step. Any recovery backup remains available.";
    }

    private bool Confirm(string text, CancellationToken token)
    {
        if (token.IsCancellationRequested || IsDisposed || !IsHandleCreated) return false;
        if (InvokeRequired)
        {
            try { return (bool)Invoke((Func<bool>)(() => Confirm(text, token))); }
            catch (InvalidOperationException) { return false; }
        }
        using var dialog = new Form
        {
            Name = "identityReview", Text = "MyKa - Review system changes", Font = Font,
            Size = new Size(900, 620), MinimumSize = new Size(600, 400),
            StartPosition = FormStartPosition.CenterParent, AutoScaleMode = AutoScaleMode.Dpi,
        };
        var layout = CreateLayout("reviewLayout", 2);
        layout.Padding = new Padding(12);
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(new TextBox
        {
            Multiline = true, ReadOnly = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Both,
            WordWrap = false, Text = text, AccessibleName = "Changes to review",
        }, 0, 0);
        var actions = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2, RowCount = 1 };
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        var cancel = Button("cancelReview", "Cancel");
        cancel.DialogResult = DialogResult.Cancel;
        var accept = Button("acceptReview", "Apply reviewed operations");
        accept.DialogResult = DialogResult.OK;
        actions.Controls.Add(cancel, 0, 0);
        actions.Controls.Add(accept, 1, 0);
        layout.Controls.Add(actions, 0, 1);
        dialog.Controls.Add(layout);
        dialog.CancelButton = cancel;
        dialog.AcceptButton = cancel;
        IdentityTheme.Apply(dialog);
        using var registration = token.Register(() => CloseOnUi(dialog));
        dialog.Shown += (_, _) => { if (token.IsCancellationRequested) dialog.Close(); };
        return dialog.ShowDialog(this) == DialogResult.OK && !token.IsCancellationRequested;
    }

    private void SizeTabs() => tabs.ItemSize = new Size(TextRenderer.MeasureText("Operations", tabs.Font).Width + 28, tabs.Font.Height + 18);

    private void SizeForFont()
    {
        var scale = Math.Max(1, Font.SizeInPoints / bodyFont.SizeInPoints) * DeviceDpi / 96f;
        MinimumSize = new Size((int)Math.Ceiling(760 * scale), (int)Math.Ceiling(540 * scale));
    }

    private static void CloseOnUi(Form form)
    {
        try { if (form.IsHandleCreated && !form.IsDisposed) form.BeginInvoke((Action)(() => form.Close())); }
        catch (InvalidOperationException) { }
    }

    public static void Run(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        using var form = new IdentityWindow(token);
        using var registration = token.Register(() => CloseOnUi(form));
        form.Shown += (_, _) => { if (token.IsCancellationRequested) form.Close(); };
        form.ShowDialog();
    }

    protected override void Dispose(bool disposing)
    {
        var release = disposing && !resourcesDisposed;
        if (release)
        {
            resourcesDisposed = true;
            lifetime.Cancel();
            lifetime.Dispose();
        }
        base.Dispose(disposing);
        if (release) { bodyFont.Dispose(); titleFont.Dispose(); }
    }
}
