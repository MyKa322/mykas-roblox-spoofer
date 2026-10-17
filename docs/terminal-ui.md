# Terminal interface specification

This document includes the broader target interface. Version 0.3 ships Accounts, Diagnostics, Adapters and Identity tabs, with a fallback menu that also offers System identity and the logo. P in Accounts opens the [experience handoff window](desktop-launch.md). The Identity tab lists available tools and limits; Enter or O opens [system identity, comparison and recovery](system-identity.md). S in Adapters and `identity manage` open the same window. Read-only snapshot/compare commands support files. Environment coordination remains proposed under [ADR-0002](adr/0002-account-execution-environments.md). See [all shipped commands](user-guide.md#commands). The application name is MyKa's Roblox Spoofer; ASCII uses the straight apostrophe.

## Branding and layout

The wordmark is a 3D model, not a fixed picture: bold eight-row block letters with thickness, lit from the upper left, and ray-cast by a small renderer (`src/MyKa.Spoofer/Branding`). Every logo comes from that one model, so the colour and plain versions cannot drift apart. The [plain logo asset](../assets/logo.txt) is a snapshot of the plain version, and a test fails if it goes out of date. The exact mixed-case title appears with every logo.

**Symbols.** Each kind of surface has its own symbol so the letters read as solid objects. Colour terminals use `█` for front faces in an aqua-to-violet gradient with a lighter raised edge and darker recessed edge, `▒` for extruded side walls, `▓` for tops, and `░` for the shadow the letters cast on an invisible wall behind them. Half blocks `▀` and `▄` give edges twice the vertical detail. Plain output uses only printable ASCII: `#` for faces, `/` for walls, `=` for tops, and `.` for shadow, as the old art did.

**Motion.** The logo above the tabs turns continuously when there is room. One turn takes nine seconds; it lingers while facing the viewer, whips round the back, and levels its tilt as it goes edge-on so no corner leaves the frame. It redraws about twenty times a second and reads keys between frames, so selection is never blocked. The `logo` command spins once for under three seconds, settles on a resting pose, and any key skips the spin. Motion is limited to the logo: there is no boot delay and nothing waits on an animation. `--still` starts at the resting pose. Space toggles motion in the tabbed screen. The logo stays still while the shortcut guide is open, and the loop avoids idle redraws when no animation is visible. The motion is off in plain output, in redirected output, with `NO_COLOR` or `TERM=dumb`, and with `--plain`.

**Size.** The tabbed layout keeps one terminal row spare and at least 12 content rows before showing art. The full stack needs a window of at least 72 × 38; the compact wordmark with its subtitle needs 55 × 28. Below that, show only `MyKa's Roblox Spoofer`. The usable minimum is 50 × 12; smaller windows show a resize notice and accept only quit keys. Check size before consuming input, and preserve the selected tab on resize. In plain output the menu omits art, and the explicit `logo` command prints the plain art, which is "MYKA'S" with the subtitle beneath. The logo must never push the first usable menu offscreen.

Suggested styling: a bold title, neutral body text, a highlighted selection bar. Always include state words alongside color. Honor `NO_COLOR` and `--plain`; use ASCII borders and characters throughout plain output.

The shipped tabbed screen, beneath the logo when space permits:

~~~text
MyKa's Roblox Spoofer v0.3.2
Login browser: WebView2 ready

  1 Accounts    2 Diagnostics    3 Adapters    4 Identity
━━━━━━━━━━━━━━──────────────────────────────────────────
  ACCOUNTS  ·  none saved

  No saved accounts yet.
  Press A to sign in to Roblox in a separate browser window.

  A add account   C clean up   R reload
  1-4 tabs  F1 help  Space pause  Esc quit
~~~

The status line names the state in words (`WebView2 ready` or `WebView2 unavailable`), not only in colour. Left/Right, Tab/Shift+Tab, and 1-4 switch tabs. Accounts uses A to add, S or Enter to sign in again, D to ask for removal, C to clean up, R to reload, and Up/Down to select. Removal is modal: Y removes and any other key keeps the account. The account name and full numeric ID stay visible even at the minimum window size. Diagnostics and Network adapters use R to refresh and scrolling keys to browse. Results remain inside the current tab; there is no separate report pause. Each key is followed by a redraw before the next queued key is handled. The alternate buffer is restored on exit or failure. Standalone report commands use a labelled rule and aligned fields; plain output keeps `Label: value`.

F1 or `?` opens a scrollable shortcut guide over the current tab. Esc, F1, or `?` closes it without reloading the page or changing its selection. Page actions and tab switching are contained while the guide is open. Space toggles animation without restarting. Compact footers always show help, motion, and quit; the full guide lists the remaining navigation keys. Tab labels lose their extra padding when needed so all four names fit at 50 columns.

Identity shows a scrollable capability list with separate sections for stored settings, network tools, scoped cleanup, inspect-only observations and unimplemented features. Its Open system identity tools action stays visible at the minimum height, including after an opening failure. Visiting or scrolling the tab performs no system reads or writes. Enter or O opens the existing native window only after the terminal has drawn its opening message; queued keys are discarded when that window closes.

The native System Identity window shares the terminal's monospaced branding, dark palette and cyan selection. Snapshots holds capture, comparison, backup and recovery actions; Operations holds a scrollable selection list with Apply pinned below it; Results keeps messages and recovery paths until the window closes. Its footer always exposes Results, Cancel current and Close with a written ready/working state. Esc cancels current work, then closes the window when idle. Failed captures keep existing data and allow a retry. See the [native UI review](review-2026-10-18.md) for the layout and lifecycle checks.

A failed account read is an unavailable state, not “none saved.” Show the error with scrollable details and R to retry; account actions remain inactive until the list loads. The plain numbered menu remains available with `--plain` for users who prefer sequential output or use a screen reader. This is an alternative presentation, not a claim of screen-reader compatibility across terminals.

Proposed full-product overview after environment feasibility passes:

~~~text
MyKa's Roblox Spoofer
Environment A    Account: Main    Client: Not checked
Environment B    Account: Test    Client: Not checked
Environment C    Account: Third   Client: Not checked
Network separation: Not checked

> Accounts
  Environments
  Launch experience
  Sessions
  Diagnostics
  Exit
~~~

This is a target view, not the current menu. During feasibility, run the existing application separately inside each environment and use the official browser for manual launch. A combined overview is useful only after a deployment and coordination path work.

## Primary journeys

### Accounts and launch

1. Select the execution environment and sign in there. The browser and its saved profile belong to that environment.
2. Verify and save the returned Roblox identity through the current native controls. A host account record does not automatically sign in a guest.
3. Select an account and numeric place ID. Show the environment, actual client build, and readiness observations.
4. Show network evidence separately. Distinct public egress remains unverified unless measured for the relevant browser and Player traffic.
5. Launch only through a proven path inside the selected environment. Do not redirect guest launch requests to the host.
6. Display a result for each account. A failure in one environment preserves the others.

The current UI keeps one active page and one confirmation at a time. Account actions share `AccountOperations` with CLI commands. The sign-in instruction is drawn before the modal browser opens; queued browser-time keys are discarded afterward. Preserve these boundaries.

### Environments and sessions

Keep environment readiness, browser login, client process, and game join status separate:

~~~text
SESSIONS
Environment  Account  Client          Join
A            Main     Client running  Unknown
B            Test     Starting        Unknown
C            Third    Unverified      Unknown

Client running requires verified process ownership.
Join status stays Unknown until independently observed.
~~~

The environment label is a local reference. It does not certify a new hardware identity. A readiness row shows when and on which configuration it was checked. A disconnected environment shows stale or unknown status, never a fabricated successful stop.

Offer Close only for a verified owned client in the selected environment. Do not close every Roblox process or power down an entire guest as an account action. Preserve account selection and place input after recoverable validation failures.

### Device and network observations

Keep the current Adapters page read-only. The Diagnostics page now includes timestamped edition, CPU, RAM, virtualization, and display-adapter observations from the same report as `doctor`. It shows incomplete probes explicitly; R collects a fresh report and resets scrolling. Player compatibility and public egress remain untested. See the [preflight guide](environment-preflight.md). Future network evidence must include its measurement scope. Use `Not checked`, `Unavailable`, or an observed result; do not display "Roblox unlinked", "Undetected", or a single "HWID spoofed" badge.

[ADR-0002](adr/0002-account-execution-environments.md) replaces the earlier host identity plan/apply/restore workflow for this requirement. Those commands are unimplemented and are no longer part of the active target interface. Environment provisioning uses the chosen platform's tools; a profile label is not an Apply action.

## Proposed command surface

Shipped commands remain those documented in [README](../README.md). Do not advertise new command syntax before the environment selection and launch path are implemented.

Future launch and session actions must identify their environment and use the same validation as interactive actions. The first deployment runs one app locally in each environment, which supplies that context without a remote command API. A local account ID must never be interpreted in another environment's store.

A launch action remains under supervision instead of claiming success on protocol dispatch. Keep one managed Player per environment initially. A second local invocation must not race the existing session owner.

`--plain` changes presentation only. Do not accept passwords, cookies, raw launch URLs, arbitrary process arguments, or a flag that skips readiness validation. Read-only commands support redirected output; actions requiring confirmation must fail clearly without an interactive terminal.

Future launch success means a verified client start, not a joined game. A timeout with an uncertain outcome is not success and must not trigger an automatic second launch. Existing version 0.2 exit codes stay unchanged.

## Interaction rules

- Enter selects, Esc returns, and Ctrl+C cancels the pending action. A launch already dispatched may have an uncertain outcome; cancellation does not prove the client stopped.
- At narrow widths, replace tables with labelled records. Keep plain numbered choices and explicit state words alongside colour.
- Show observed progress such as "Waiting for Roblox login" or "Checking environment". Do not invent percentage completion.
- Escape external text so account labels cannot inject control characters or terminal markup.
- Errors identify the account and environment. Authentication failures offer sign-in; uncertain process ownership disables targeted control.
- Exiting explains whether the local client remains running. Losing coordination with another environment must not terminate its session.
