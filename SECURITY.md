# Security

## Reporting a vulnerability

If this repository has GitHub private vulnerability reporting enabled, use **Report a vulnerability** on its Security page. That option is suitable for sensitive reproduction details.

If the option is unavailable, open a public issue containing only a brief description of the affected area and a request for a private reporting channel. Do not publish exploit details, credentials or private files while arranging that channel. This project does not list a separate security email address or promise a response time.

Useful reports include the application version, Windows version, affected component, expected behavior and a minimal reproduction using synthetic data. Describe the impact without attaching a real browser profile or recovery file.

## Data to keep private

The application stores browser profiles and account metadata under `%LOCALAPPDATA%\MyKa\RobloxSpoofer`. Identity snapshots contain device identifiers and local IPs. Recovery files may also contain backed-up RobloxPlayer preferences, and quarantined cache folders remain beside their recovery file.

Do not publish passwords, cookies, verification codes, authenticated URLs, browser storage, full hardware identifiers or backup contents. Use fictional values in screenshots and remove identifying paths from logs. Removing a local account does not revoke Roblox sessions on other devices.

## Relevant boundaries

The application uses separate WebView2 profiles for browser state, restricts navigation and verifies an account's numeric Roblox identity before saving it. These profiles do not isolate RobloxPlayer or provide separate hardware or public IPs.

System Identity operates on the current Windows installation. It previews selected operations and writes a recovery file before changes. Restore preserves later conflicting settings and existing folders. Imported files are validated against fixed targets. DNS cache contents and DHCP leases cannot be restored, and cancellation does not undo steps that have already completed.

These controls have automated coverage. Live sign-in and handoff, privileged changes and recovery on an actually modified installation still need manual validation. See [system identity and recovery](docs/system-identity.md) for the supported scope and limits.
