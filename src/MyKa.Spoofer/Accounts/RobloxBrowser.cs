using Microsoft.Web.WebView2.Core;

namespace MyKa.Spoofer.Accounts;

/// <summary>Restrictions shared by the login and experience windows.</summary>
internal static class RobloxBrowser
{
    public static void Restrict(CoreWebView2 core)
    {
        core.Settings.IsPasswordAutosaveEnabled = false;
        core.Settings.IsGeneralAutofillEnabled = false;
        core.Settings.IsWebMessageEnabled = false;
        core.Settings.AreHostObjectsAllowed = false;
        core.Settings.AreDevToolsEnabled = false;
        core.Settings.AreDefaultContextMenusEnabled = false;
        core.PermissionRequested += (_, e) => e.State = CoreWebView2PermissionState.Deny;
        core.DownloadStarting += (_, e) => e.Cancel = true;
        core.ServerCertificateErrorDetected += (_, e) => e.Action = CoreWebView2ServerCertificateErrorAction.Cancel;
        // Each window owns its navigation, new-window and external-protocol decisions.
    }

    public static void Show(Form form, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (BrowserLogin.RuntimeVersion() is null)
            throw new InvalidOperationException("Microsoft Edge WebView2 Runtime is required. Install it from Microsoft's official site.");
        using var registration = token.Register(() =>
        {
            try { if (form.IsHandleCreated && !form.IsDisposed) form.BeginInvoke((Action)(() => form.Close())); }
            catch (InvalidOperationException) { }
        });
        // Close even if cancellation arrived before the form acquired a handle.
        form.Shown += (_, _) => { if (token.IsCancellationRequested) form.Close(); };
        form.ShowDialog();
    }
}
