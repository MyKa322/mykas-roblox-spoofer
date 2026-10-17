# User guide

MyKa's Roblox Spoofer 0.3.2 is a Windows terminal application with account tools, local diagnostics and a native System Identity window. This guide describes shipped behavior. The [architecture](architecture.md) also discusses independent execution environments that are not implemented.

The application interface and commands are in English. The [translated READMEs](../README.md) cover setup and everyday use in 15 other languages.

## Install and start

To build from source, install the .NET 10 SDK on Windows x64 and use Windows PowerShell 5.1 or PowerShell 7. Account sign-in also needs Microsoft Edge WebView2 Runtime and internet access. Run the following from the repository root:

```powershell
.\scripts\Build.ps1
.\.artifacts\publish\myka.exe
```

Keep every published file beside `myka.exe`. The build also creates `.artifacts/myka-0.3.2-win-x64.zip` with a `.zip.sha256` file. A machine that runs the package without the SDK needs the .NET 10 Desktop Runtime x64.

During development, you can run directly from source:

```powershell
dotnet run --project src/MyKa.Spoofer -c Release
```

The terminal menu requires interactive input and output. For pipes or files, use a command such as `doctor`, `accounts list` or `identity compare`.

## Navigate the terminal

The full-screen interface has Accounts, Diagnostics, Adapters and Identity tabs. Use Left/Right, Tab/Shift+Tab or 1-4 to switch. Each tab keeps its selection, report and messages while you visit another tab.

| Context | Controls |
| --- | --- |
| Accounts | Up/Down selects; Home/End selects first/last; PageUp/PageDown moves a page |
| Account actions | A adds; Enter or S signs in again; P opens an experience |
| Account maintenance | D or Delete asks to remove; C retries cleanup; R reloads |
| Removal confirmation | Y confirms; any other key keeps the account |
| Diagnostics and Adapters | R refreshes; arrows, PageUp/PageDown and Home/End scroll |
| Identity | Enter or O opens the native tools; arrows scroll the capability list |
| Adapters shortcut | S opens the same System Identity window |
| Help | F1 or ? opens the shortcut guide; F1, ? or Esc returns to the same page |
| Motion | Space pauses or resumes the logo; help pauses it temporarily |
| Exit | Esc or Q quits when no guide or confirmation is open; Ctrl+C cancels with code 130 |

`--plain` uses a numbered menu without colour or animation. Terminals that do not support the alternate screen use a selection menu. `--still` starts with logo motion disabled. `NO_COLOR`, `TERM=dumb` and redirected output select plain rendering. Explicit `logo` still prints its plain art when redirected.

A small window hides the logo before reducing the content area. Below 50 columns or 12 rows, page actions pause and Esc quits; hidden actions cannot run. Resizing preserves the selected tab.

## Add and manage accounts

1. Open Accounts and press A.
2. Sign in on the Roblox page in the separate browser window. Enter passwords and verification codes there, never in the terminal.
3. Choose Verify account. Check the returned username and numeric Roblox user ID.
4. Choose Save account.

The app reads Roblox's authenticated identity response after Verify and saves the account only after confirmation. Accounts are keyed by numeric Roblox user ID rather than username. Adding the same account twice is refused. Signing in again must return the saved account's numeric ID; a different identity leaves saved data unchanged.

Each saved account has a separate application-owned WebView2 profile. The login window permits HTTPS navigation within the Roblox allow-list and blocks extra windows, downloads, permission prompts and external-protocol launches. Password autosave is disabled. Sign-in methods that redirect to another provider's website are unsupported.

`accounts list` reports the last verified identity with `Current login status: not checked`. It does not confirm that the saved login still works. Use Sign in again when a profile has expired or is missing.

Removing an account deletes its local profile and does not revoke Roblox sessions elsewhere. If the browser still holds files, cleanup remains queued. Close login windows and retry with C or `accounts cleanup`.

## Open an experience

Select an account and press P, then enter a positive numeric Roblox place ID. The experience window reuses that account's saved WebView2 profile and checks its browser identity again. The account store remains locked until the window closes.

From the experience page, one user-initiated Play action may request the desktop Roblox protocol. The app does not retry an uncertain request. A requested handoff does not establish that RobloxPlayer started or that the intended account joined. Check the desktop client before trying again.

Browser profiles share the current Windows device and network. They do not provide separate native Player environments. Read the [handoff guide](desktop-launch.md) for the supported checks and the unverified boundary.

## Use System Identity

Press 4, then Enter or O, or run `identity manage`. The fallback terminal menu also offers System identity. The native window has three tabs:

| Tab | Contents |
| --- | --- |
| Snapshots | Before/After capture, comparison, JSON export, load and restore |
| Operations | Scrollable operation choices with a pinned Apply selected button |
| Results | Messages and recovery paths accumulated during the current window session |

Capture Before to inspect WMI identifiers, fixed-volume serials, available monitor EDID hashes, the fixed registry values, reported adapter MACs, configured overrides and local IPs. Missing or inaccessible observations remain unavailable. After capture appears beside Before with its own timestamp. Compare distinguishes Changed, Unchanged, Added, Removed and Unavailable; a failed observation does not count as a successful change.

Create backup file exports the Before snapshot. This is an observation file, not the recovery record for later mutations. Load Before / backup accepts a snapshot or recovery file so comparison can continue after restarting the app or Windows.

No operation is selected by default. Apply selected prepares a fresh snapshot and previews supported changes. After confirmation, it persists a recovery file before the first change. Supported operations include replacing existing valid `MachineGuid`, profile `0001`'s `HwProfileGuid` and `ComputerHardwareId` strings, and configuring eligible driver-advertised MAC overrides. Stored GUID and network operations require an elevated terminal. The app does not elevate itself, restart adapters or reboot Windows. Effective MAC changes may need a manual adapter restart or reboot.

Scoped cleanup quarantines only existing Roblox `logs` and `http` folders and can back up and clear `HKCU\SOFTWARE\Roblox\RobloxPlayer` preferences. It excludes installed versions, browser profiles, account-store data and unrelated files. Close RobloxPlayer and Studio before cleanup or restore; the app checks for clients without terminating them.

Keep the recovery JSON and its quarantine folders together. Restore preserves later conflicting values and refuses existing destination folders rather than merging or overwriting them. DHCP leases and flushed DNS cache contents cannot be recovered.

Cancel current, or Esc while busy, requests cancellation. Retry is available when the current step finishes. Close waits for cancellation/completion, and Esc closes an idle window. A failed capture preserves existing snapshots and opens Results. Cancellation does not reverse completed steps: keep the backup, read Results and capture After before another attempt.

These tools affect the current Windows installation. Firmware UUID/serial spoofing, disk/volume/EDID rewriting, hidden registry value creation, public-IP routing and independent concurrent environments are not implemented. DHCP renewal does not guarantee a different local or public IP. Read [system identity and recovery](system-identity.md) before using Apply or Restore.

## Commands

Examples below assume you have built the application. Replace `LOCAL_ACCOUNT_ID` with the 32-character local ID shown by `accounts list`; it is different from the numeric Roblox user ID. Replace `PLACE_ID` with the intended numeric place ID.

```powershell
.\.artifacts\publish\myka.exe --help
.\.artifacts\publish\myka.exe accounts add
.\.artifacts\publish\myka.exe accounts list
.\.artifacts\publish\myka.exe accounts login --account LOCAL_ACCOUNT_ID
.\.artifacts\publish\myka.exe accounts remove --account LOCAL_ACCOUNT_ID
.\.artifacts\publish\myka.exe accounts cleanup
.\.artifacts\publish\myka.exe accounts play --account LOCAL_ACCOUNT_ID --place PLACE_ID
```

`accounts add`, `login`, `remove` and the experience workflow require an interactive terminal. Account removal does not need WebView2 Runtime.

Read-only diagnostic and display commands:

```powershell
.\.artifacts\publish\myka.exe --plain doctor
.\.artifacts\publish\myka.exe identity inspect
.\.artifacts\publish\myka.exe logo
.\.artifacts\publish\myka.exe --still
```

Snapshot commands create private files and refuse to overwrite an existing export:

```powershell
.\.artifacts\publish\myka.exe identity manage
.\.artifacts\publish\myka.exe identity snapshot --output before.snapshot.json
.\.artifacts\publish\myka.exe identity snapshot --output after.snapshot.json
.\.artifacts\publish\myka.exe identity compare --before before.snapshot.json --after after.snapshot.json
```

Snapshot export returns 1 if any observation is unavailable while preserving useful observations in the file. Compare also returns 1 for unavailable comparisons. Those read-only commands support redirection; `identity manage` requires an interactive terminal.

## Data and privacy

Account profiles and versioned `accounts.json` metadata live under `%LOCALAPPDATA%\MyKa\RobloxSpoofer`. Windows permissions restrict the folder to the current user, SYSTEM and Administrators. The store rejects symbolic links and junctions, uses an exclusive operation lock and replaces metadata atomically with a `.bak` copy. Invalid or future-version data is rejected and left untouched.

The app does not read password fields or cookies. It reads one authenticated identity response after you choose Verify to obtain the numeric user ID, username and display name. Browser profiles still contain sensitive session state and should remain private.

Identity snapshots contain device identifiers and local IPs. Recovery files can contain stored settings and Player preferences. Quarantined cache data remains in folders beside the recovery JSON. Keep all of these out of public issues and screenshots. The app's built-in diagnostics do not collect passwords, cookies, board/disk serials or launch authorization; the separate identity snapshot does collect its documented device observations.

## The logo

The wordmark is a 3D model built from block letters with thickness, lit from the upper left and ray-cast by the renderer in `src/MyKa.Spoofer/Branding`. The terminal and standalone `logo` command use the same model.

Colour terminals use solid `█` front faces in an aqua-to-violet gradient, a raised/recessed bevel, `▒` side walls, `▓` tops and `░` shadow. Half blocks `▀` and `▄` add vertical edge detail. Plain ASCII uses `#` faces, `/` walls, `=` tops and `.` shadow; [the checked-in logo](../assets/logo.txt) is tested against the renderer.

In the tabbed interface, the wordmark rotates about once every nine seconds and redraws about twenty times a second. It lingers facing forward, turns faster around the back and levels out edge-on. `logo` spins once and settles; any key skips the spin. Space toggles motion in the tabbed view.

The layout reserves at least 12 content rows before showing the logo. The three-word stack requires 72 columns and 38 rows; the compact wordmark requires 55 columns and 28 rows. Smaller windows show the title alone. Plain output has no animation. See [terminal design](terminal-ui.md) for renderer and interaction details.

## If something goes wrong

| Symptom | What to try |
| --- | --- |
| Login browser is unavailable | Install or repair WebView2 Runtime; external sign-in providers remain unsupported |
| Accounts shows `unavailable` | Close other account operations and press R; do not replace damaged metadata with an empty file |
| Invalid or missing account metadata | Keep the original files and use the `accounts.json.bak` copy for recovery |
| Profile cleanup remains pending | Close login windows and run `accounts cleanup` |
| A snapshot fails | Read Results and retry; the previous snapshots remain available |
| Some identity values are unavailable | Check the reported scope/permissions; missing data is not evidence of a successful change |
| A stored MAC override differs from the effective MAC | Review the driver limit and capture After following any appropriate Windows adapter restart/reboot |
| An operation is cancelled or partially fails | Keep its recovery files, read Results and capture After before retrying |

`doctor` searches the default per-user desktop installation. It does not cover Store or custom installations, and a discovered executable is only a candidate. An incomplete check does not prove Roblox is missing. `doctor` also returns 1 when an environment observation is unavailable; a collected report does not establish VM support, GPU passthrough or distinct public IPs. See [environment preflight](environment-preflight.md).

For a UI report, include the application/Windows versions, window dimensions, text scaling and a redacted screenshot. Follow [security reporting](../SECURITY.md) for a vulnerability rather than posting private data.

## Exit codes

| Code | Meaning |
| --- | --- |
| 0 | Completed command or check |
| 1 | Incomplete or failed operation/check |
| 2 | Invalid command or a menu that lacks an interactive terminal |
| 3 | Unsupported operating system |
| 130 | Cancellation |

A completed diagnostic check does not establish that Roblox is installed or compatible.

## Verification and next steps

The Release suite has 691 cases, including 15 native UI cases. Coverage includes account persistence and concurrency, identity validation, fixed recovery targets, comparison, terminal rendering, modal key ordering, resizing, cancellation/retry and larger-font/scaling layouts. Tests use temporary folders and synthetic data. They do not touch real account profiles or apply live identity/network changes.

Real Roblox sign-in, desktop handoff, privileged changes and recovery on a modified installation still require manual validation. Physical monitor DPI transitions, high-contrast mode and screen-reader navigation need a manual pass as well. See the [UI review](review-2026-10-18.md) for the exact rendering evidence.

The browser starts the native Player, so separate browser profiles alone do not provide complete account environments. The proposed architecture considers full Windows VMs or separate physical machines, with compatibility and routing checked independently. It does not promise Roblox will consider accounts unrelated. The next feasibility step is a [single-environment trial](environment-preflight.md), before any three-account deployment claim.

For development commands and review expectations, see [CONTRIBUTING.md](../CONTRIBUTING.md). The current implementation uses C#/.NET 10, Spectre.Console and WinForms-hosted WebView2; no environment coordinator is shipped.
