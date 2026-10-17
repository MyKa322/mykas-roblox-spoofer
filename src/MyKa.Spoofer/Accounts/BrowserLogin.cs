using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace MyKa.Spoofer.Accounts;

public sealed class BrowserLogin : Form
{
    private readonly WebView2 browser = new() { Dock = DockStyle.Fill };
    private readonly Label address = new() { Dock = DockStyle.Fill, AutoEllipsis = true };
    private readonly Label status = new() { Dock = DockStyle.Fill, AutoEllipsis = true };
    private readonly Button verify = new() { Text = "Verify account", AutoSize = true, Enabled = false };
    private readonly Button save = new() { Text = "Save account", AutoSize = true, Enabled = false };
    private readonly Button login = new() { Text = "Back to sign in", AutoSize = true, Enabled = false };
    private readonly string profileDirectory;
    private readonly string? expectedUserId;
    private readonly CancellationToken cancellation;
    private bool verificationRequested;
    private ulong? verificationNavigation;
    private RobloxIdentity? verified;
    public RobloxIdentity? Result { get; private set; }
    public string? Failure { get; private set; }

    private BrowserLogin(string profileDirectory, string? expectedUserId, CancellationToken cancellation)
    {
        this.profileDirectory = profileDirectory;
        this.expectedUserId = expectedUserId;
        this.cancellation = cancellation;
        Text = "MyKa - Sign in to Roblox";
        Width = 1050;
        Height = 800;
        MinimumSize = new System.Drawing.Size(720, 560);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1, Padding = new Padding(12) };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(0, 8, 0, 0) };
        var cancel = new Button { Text = "Cancel", AutoSize = true };
        cancel.Click += (_, _) => Close();
        buttons.Controls.AddRange([login, verify, save, cancel]);
        layout.Controls.Add(address, 0, 0);
        layout.Controls.Add(browser, 0, 1);
        layout.Controls.Add(buttons, 0, 2);
        layout.Controls.Add(status, 0, 3);
        Controls.Add(layout);
        CancelButton = cancel;
        address.Text = "Opening Roblox...";
        status.Text = "Complete Roblox sign-in, then choose Verify account.";
        login.Click += (_, _) => NavigateToLogin();
        verify.Click += (_, _) => Verify();
        save.Click += (_, _) =>
        {
            if (verified is null || cancellation.IsCancellationRequested) return;
            Result = verified;
            DialogResult = DialogResult.OK;
            Close();
        };
        Shown += Initialize;
    }

    public static string? RuntimeVersion()
    {
        try { return CoreWebView2Environment.GetAvailableBrowserVersionString(); }
        catch (WebView2RuntimeNotFoundException) { return null; }
    }

    /// <summary>The runtime version for display. A broken install must not stop a status line from showing, so it reads as absent.</summary>
    public static string? TryRuntimeVersion()
    {
        try { return RuntimeVersion(); }
        catch (Exception e) when (e is DllNotFoundException or BadImageFormatException
            or System.Runtime.InteropServices.COMException or InvalidOperationException)
        {
            return null;
        }
    }

    public static RobloxIdentity? Run(string profileDirectory, string? expectedUserId, CancellationToken token)
    {
        using var form = new BrowserLogin(profileDirectory, expectedUserId, token);
        // All browser controls and the modal message loop live on the main STA thread.
        RobloxBrowser.Show(form, token);
        token.ThrowIfCancellationRequested();
        if (form.Failure is not null) throw new InvalidOperationException(form.Failure);
        return form.Result;
    }

    private async void Initialize(object? sender, EventArgs args)
    {
        try
        {
            cancellation.ThrowIfCancellationRequested();
            var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: profileDirectory);
            if (IsDisposed || Disposing || cancellation.IsCancellationRequested) return;
            await browser.EnsureCoreWebView2Async(environment);
            if (IsDisposed || Disposing || cancellation.IsCancellationRequested) return;
            var core = browser.CoreWebView2;
            RobloxBrowser.Restrict(core);
            core.NewWindowRequested += (_, e) => { e.Handled = true; status.Text = "Extra windows are blocked. Continue sign-in here."; };
            core.LaunchingExternalUriScheme += (_, e) => e.Cancel = true;
            core.NavigationStarting += NavigationStarting;
            core.NavigationCompleted += NavigationCompleted;
            core.SourceChanged += (_, _) =>
            {
                address.Text = Uri.TryCreate(core.Source, UriKind.Absolute, out var uri)
                    ? "Website: " + uri.GetLeftPart(UriPartial.Authority) : "Website: unavailable";
            };
            core.ProcessFailed += (_, _) => Fail("The login browser stopped. Close it and try signing in again.");
            login.Enabled = true;
            NavigateToLogin();
        }
        catch (OperationCanceledException) { if (!IsDisposed) Close(); }
        catch (Exception e) when (e is WebView2RuntimeNotFoundException or System.Runtime.InteropServices.COMException
            or IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            if (!IsDisposed && !Disposing)
            {
                Failure = "The login browser could not start. Check WebView2 Runtime and access to the account data folder.";
                Close();
            }
        }
    }

    private void NavigateToLogin()
    {
        if (browser.CoreWebView2 is null) return;
        verified = null;
        Result = null;
        verificationRequested = false;
        verificationNavigation = null;
        save.Enabled = false;
        verify.Enabled = true;
        browser.Enabled = true;
        status.Text = expectedUserId is null
            ? "Complete Roblox sign-in, then choose Verify account."
            : "Sign in to Roblox user ID " + expectedUserId + ", then choose Verify account.";
        browser.CoreWebView2.Navigate(RobloxIdentity.LoginUrl);
    }

    private void Verify()
    {
        if (browser.CoreWebView2 is null) return;
        browser.CoreWebView2.Stop();
        verified = null;
        save.Enabled = false;
        verify.Enabled = false;
        browser.Enabled = false;
        verificationRequested = true;
        verificationNavigation = null;
        status.Text = "Checking the signed-in account with Roblox...";
        browser.CoreWebView2.Navigate(RobloxIdentity.IdentityUrl);
    }

    private void NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (!RobloxIdentity.IsAllowedPage(e.Uri))
        {
            e.Cancel = true;
            Fail("This destination is outside the Roblox login flow.");
            return;
        }
        verified = null;
        save.Enabled = false;
        if (verificationRequested && RobloxIdentity.IsIdentityUrl(e.Uri))
            verificationNavigation = e.NavigationId;
        else
        {
            verificationRequested = false;
            verificationNavigation = null;
        }
    }

    private async void NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (IsDisposed || Disposing) return;
        if (!verificationRequested || verificationNavigation != e.NavigationId)
        {
            if (!e.IsSuccess) status.Text = "Roblox did not load. Check the connection and choose Back to sign in.";
            return;
        }
        verificationRequested = false;
        try
        {
            if (!e.IsSuccess || e.HttpStatusCode != 200 || !RobloxIdentity.IsIdentityUrl(browser.CoreWebView2.Source))
            {
                Fail(e.HttpStatusCode == 401
                    ? "Roblox has not confirmed a login. Choose Back to sign in."
                    : "Account verification did not complete. Choose Back to sign in and retry.");
                return;
            }
            // Read only this trusted HTTPS response body; never read cookies or login form fields.
            var encoded = await browser.CoreWebView2.ExecuteScriptAsync("document.body.innerText.slice(0, 16385)");
            if (IsDisposed || Disposing || cancellation.IsCancellationRequested) return;
            if (verificationNavigation != e.NavigationId || !RobloxIdentity.IsIdentityUrl(browser.CoreWebView2.Source)) return;
            var body = JsonSerializer.Deserialize<string>(encoded) ?? "";
            var identity = RobloxIdentity.Parse(browser.CoreWebView2.Source, e.HttpStatusCode, body);
            if (expectedUserId is not null && identity.UserId != expectedUserId)
            {
                Fail("This is a different Roblox account. The saved account has not changed.");
                return;
            }
            verified = identity;
            save.Enabled = true;
            status.Text = "Confirmed: " + identity.Username + " (user ID " + identity.UserId + "). Choose Save account.";
        }
        catch (Exception error) when (error is JsonException or InvalidDataException or InvalidOperationException
            or System.Runtime.InteropServices.COMException)
        {
            if (!IsDisposed && !Disposing) Fail("Roblox did not return a verifiable account. Choose Back to sign in.");
        }
    }

    private void Fail(string message)
    {
        verified = null;
        Result = null;
        verificationRequested = false;
        verificationNavigation = null;
        save.Enabled = false;
        verify.Enabled = false;
        browser.Enabled = true;
        status.Text = message;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) browser.Dispose();
        base.Dispose(disposing);
    }
}
