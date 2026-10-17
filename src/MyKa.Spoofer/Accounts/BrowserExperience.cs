using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace MyKa.Spoofer.Accounts;

/// <summary>Opens one place using a saved browser profile; the website supplies the desktop launch request.</summary>
internal sealed class BrowserExperience : Form
{
    private readonly WebView2 browser = new() { Dock = DockStyle.Fill, Enabled = false };
    private readonly TextBox place = new() { Width = 190, MaxLength = 20, AccessibleName = "Roblox place ID" };
    private readonly Button open = new() { Text = "Check account and open", AutoSize = true, Enabled = false };
    private readonly Label status = new() { Dock = DockStyle.Fill, AutoEllipsis = true };
    private readonly System.Windows.Forms.Timer timeout = new() { Interval = 30_000 };
    private readonly string profileDirectory;
    private readonly SavedAccount account;
    private readonly CancellationToken cancellation;
    private readonly bool openOnReady;
    private ExperienceLaunch? attempt;
    private ulong? navigation;
    private ulong? externalNavigation;
    private bool failed;

    private BrowserExperience(string profileDirectory, SavedAccount account, string? placeId, CancellationToken token)
    {
        this.profileDirectory = profileDirectory;
        this.account = account;
        cancellation = token;
        openOnReady = placeId is not null;
        place.Text = placeId ?? "";
        Text = "MyKa - Open a Roblox experience";
        Width = 1050;
        Height = 800;
        MinimumSize = new System.Drawing.Size(780, 580);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(12) };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
        layout.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = $"Selected account: {account.Username} (user {account.UserId}).\nEnter the number after /games/ in the experience URL. This uses the current Windows environment."
        }, 0, 0);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill };
        var close = new Button { Text = "Close", AutoSize = true };
        close.Click += (_, _) => Close();
        actions.Controls.AddRange([new Label { Text = "&Place ID", AutoSize = true, Padding = new Padding(0, 6, 0, 0) }, place, open, close]);
        layout.Controls.Add(actions, 0, 1);
        layout.Controls.Add(browser, 0, 2);
        layout.Controls.Add(status, 0, 3);
        Controls.Add(layout);
        CancelButton = close;
        AcceptButton = open;
        status.Text = "Preparing the saved browser profile...";
        open.Click += (_, _) => Start();
        timeout.Tick += (_, _) => Fail("Roblox did not finish loading within 30 seconds. Close this window and check the connection.");
        Shown += Initialize;
    }

    public static ExperienceResult Run(string profileDirectory, SavedAccount account, string? placeId, CancellationToken token)
    {
        using var form = new BrowserExperience(profileDirectory, account, placeId, token);
        RobloxBrowser.Show(form, token);
        // Cancellation cannot retract a handoff already offered to Windows.
        if (form.attempt?.Result == ExperienceResult.HandoffRequested) return ExperienceResult.HandoffRequested;
        token.ThrowIfCancellationRequested();
        return form.failed ? ExperienceResult.Failed : form.attempt?.Result ?? ExperienceResult.Closed;
    }

    private async void Initialize(object? sender, EventArgs args)
    {
        try
        {
            cancellation.ThrowIfCancellationRequested();
            var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: profileDirectory);
            if (Unavailable) return;
            await browser.EnsureCoreWebView2Async(environment);
            if (Unavailable) return;
            var core = browser.CoreWebView2;
            RobloxBrowser.Restrict(core);
            core.NewWindowRequested += (_, e) => { e.Handled = true; status.Text = "Extra windows are blocked. Use Play on this experience page."; };
            core.NavigationStarting += NavigationStarting;
            core.NavigationCompleted += NavigationCompleted;
            core.LaunchingExternalUriScheme += Launching;
            core.ProcessFailed += (_, _) => Fail("The browser stopped. Close this window; check RobloxPlayer before trying again.");
            open.Enabled = true;
            status.Text = "Enter a place ID, then choose Check account and open.";
            if (openOnReady) Start();
        }
        catch (OperationCanceledException) { if (!IsDisposed) Close(); }
        catch (Exception e) when (BrowserError(e))
        {
            if (!Unavailable) Fail("The experience browser could not start. Check WebView2 Runtime and access to the account folder.");
        }
    }

    private bool Unavailable => IsDisposed || Disposing || cancellation.IsCancellationRequested;

    private void Start()
    {
        if (Unavailable || attempt is not null || browser.CoreWebView2 is null) return;
        if (!ExperienceLaunch.ValidPlaceId(place.Text))
        {
            status.Text = "Enter a positive numeric place ID, without spaces or a URL.";
            place.Focus();
            return;
        }
        attempt = new ExperienceLaunch(account.UserId, place.Text);
        open.Enabled = false;
        place.ReadOnly = true;
        status.Text = "Checking the saved browser account with Roblox...";
        try { browser.CoreWebView2.Navigate(RobloxIdentity.IdentityUrl); }
        catch (Exception e) when (BrowserError(e)) { Fail("Roblox could not be opened. Close this window and retry."); }
    }

    private void NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (Unavailable || attempt is null || !attempt.BeginNavigation(e.Uri))
        {
            e.Cancel = true;
            if (!Unavailable && attempt?.Result != ExperienceResult.HandoffRequested)
                Fail("This destination is outside the chosen experience. Close this window; use Sign in again if the login expired.");
            return;
        }
        if (ExperienceLaunch.IsPlayerUri(e.Uri)) { externalNavigation = e.NavigationId; return; }
        navigation = e.NavigationId;
        timeout.Stop();
        timeout.Start();
    }

    private async void NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (Unavailable || failed || attempt is null || attempt.Result == ExperienceResult.HandoffRequested
            || e.NavigationId == externalNavigation || e.NavigationId != navigation) return;
        try
        {
            if (!attempt.Verified)
            {
                if (!e.IsSuccess || e.HttpStatusCode != 200 || !RobloxIdentity.IsIdentityUrl(browser.CoreWebView2.Source))
                {
                    Fail("The saved login could not be verified. Close this window and use Sign in again for this account.");
                    return;
                }
                var encoded = await browser.CoreWebView2.ExecuteScriptAsync("document.body.innerText.slice(0, 16385)");
                if (Unavailable || failed || navigation != e.NavigationId || !RobloxIdentity.IsIdentityUrl(browser.CoreWebView2.Source)) return;
                var identity = RobloxIdentity.Parse(browser.CoreWebView2.Source, e.HttpStatusCode, JsonSerializer.Deserialize<string>(encoded) ?? "");
                if (identity.UserId != account.UserId)
                {
                    Fail("The browser is signed in to a different account. Close this window and use Sign in again. Saved account data was not changed.");
                    return;
                }
                attempt.ConfirmIdentity(identity);
                status.Text = $"Browser account checked: {identity.Username} (user {identity.UserId}). Opening place {attempt.PlaceId}...";
                browser.CoreWebView2.Navigate(attempt.PlaceUrl);
                return;
            }
            timeout.Stop();
            attempt.CompletePlace(browser.CoreWebView2.Source, e.IsSuccess && e.HttpStatusCode == 200);
            if (attempt.Result == ExperienceResult.Failed)
            {
                Fail("The chosen experience could not be loaded. Check its place ID and your account's access, then start a new attempt.");
                return;
            }
            browser.Enabled = true;
            status.Text = "Browser account checked. Click Play on the Roblox page to request a desktop launch. The account joined in Player must be checked there.";
        }
        catch (Exception error) when (BrowserError(error) || error is JsonException or InvalidDataException)
        {
            if (!Unavailable) Fail("The account or experience could not be verified. Close this window and use Sign in again if needed.");
        }
    }

    private void Launching(object? sender, CoreWebView2LaunchingExternalUriSchemeEventArgs e)
    {
        e.Cancel = true;
        if (Unavailable || failed || attempt is null) return;
        if (!attempt.TryRequestHandoff(e.Uri, e.InitiatingOrigin, browser.CoreWebView2.Source, e.IsUserInitiated))
        {
            if (attempt.Result != ExperienceResult.HandoffRequested)
                Fail("The desktop request was blocked. It must come from Play on the chosen Roblox page. Close this window to start a new attempt.");
            return;
        }
        timeout.Stop();
        e.Cancel = false; // WebView2 owns the normal external-app prompt and Windows protocol handoff.
        status.Text = "Desktop handoff requested. Respond to any browser prompt, then check RobloxPlayer yourself. Launch and joined account are unverified. Close this window when finished.";
    }

    private void Fail(string message)
    {
        timeout.Stop();
        failed = true;
        attempt?.Fail();
        open.Enabled = false;
        browser.Enabled = false;
        status.Text = attempt?.Result == ExperienceResult.HandoffRequested
            ? "Desktop handoff was already requested. Check RobloxPlayer before starting another attempt; launch and joined account are unverified."
            : message;
    }

    private static bool BrowserError(Exception e) => e is WebView2RuntimeNotFoundException
        or System.Runtime.InteropServices.COMException or IOException or UnauthorizedAccessException
        or System.Security.SecurityException or InvalidOperationException or ArgumentException;

    protected override void Dispose(bool disposing)
    {
        if (disposing) { timeout.Dispose(); browser.Dispose(); }
        base.Dispose(disposing);
    }
}
