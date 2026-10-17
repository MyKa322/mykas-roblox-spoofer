# MyKa's Roblox Spoofer

Version 0.3.2. Windows x64 desktop/terminal app. C# / .NET 10, Spectre.Console, WinForms and WebView2. No application server or listening port.

## Purpose

Manage verified Roblox browser profiles, request a bounded experience-page handoff, inspect the local Windows environment and offer reviewed identity maintenance with recovery files. Separate browser profiles do not isolate RobloxPlayer or public egress.

## Setup and commands

Use Windows x64, .NET SDK 10 and Windows PowerShell 5.1 or PowerShell 7. WebView2 Runtime is needed for actual account sign-in. Run from the repository root:

```powershell
.\scripts\Build.ps1
.\scripts\Build.ps1 -NoRestore
.\scripts\ValidateDocs.ps1
.\scripts\VerifyRelease.ps1
dotnet run --project src/MyKa.Spoofer -c Release -- --still
dotnet run --project src/MyKa.Spoofer -c Release -- --help
```

`Build.ps1` restores, tests, publishes, verifies the published assembly and creates the release ZIP and SHA-256 file. `-NoRestore` requires an existing restore. `VerifyRelease.ps1` requires the tested and published outputs. Artifacts belong under `.artifacts`, not in source control. No `.env` file or service secrets are needed.

## Architecture

```text
src/MyKa.Spoofer/
  Program.cs, CommandLine.cs  Entry point, cancellation and command validation
  Terminal/                  CLI reports and Spectre.Console presentation
  Terminal/Tabs/             Input loop, tab model, bounded layout and pages
  Branding/                  Shared ray-cast logo, palette and motion
  Accounts/                  Browser windows, verified identities and local store
  Diagnostics/               Read-only runtime and environment observations
  Identity/                  Snapshots, fixed operations, backups and native UI
tests/MyKa.Spoofer.Tests/     xUnit, fixtures and native window lifecycle tests
tools/ReadmeMedia/           Fixture-based screenshots and GIF generation
scripts/                    Build, documentation and release verification
docs/                       Guides, architecture, reviews and translated READMEs
```

`Program` runs on an STA thread and routes CLI or terminal actions. `TabsApp` owns input/redraw order; pages use `AccountOperations`, diagnostics and `IdentityWindow`. Domain code owns persistence, validation and recovery. The native identity window runs blocking work away from the UI thread.

## Key files

- `Accounts/AccountStore.cs`: exclusive operation lock, bounded schema, atomic metadata and profile cleanup.
- `Accounts/BrowserLogin.cs`, `RobloxBrowser.cs`: identity verification and browser restrictions.
- `Accounts/BrowserExperience.cs`, `ExperienceLaunch.cs`: one validated desktop handoff attempt.
- `Terminal/Tabs/TabsApp.cs`, `TabsModel.cs`: input ordering, modal state and resize behavior.
- `Identity/IdentityWindow.cs`, `IdentityTheme.cs`: native presentation, cancellation and lifecycle.
- `Identity/IdentityOperations.cs`: fixed targets, review, durable backup and conservative restore.
- `Identity/IdentityBackup.cs`, `IdentitySnapshot.cs`: bounded imports, private exports and comparison.

Paths above are relative to `src/MyKa.Spoofer/`. See `docs/architecture.md` for proposed environment work; proposals are not shipped features.

## Invariants

- Keep credentials on Roblox pages. Do not read cookies/password fields, log authorization or broaden browser navigation and protocol permissions.
- Save only the verified numeric Roblox identity after user confirmation; re-login must match. Keep account locks, atomic writes, size/schema checks and linked-path refusal.
- A failed account read is unavailable, not empty. A requested handoff is not a verified Player or joined account. Do not replay uncertain launches.
- Identity targets are fixed in code. Validate imports and prepare/review operations; save recovery before the first mutation. Preserve later conflicting values and existing folders during restore.
- Keep configured GUID/MAC values separate from effective hardware identity. Unavailable observations cannot count as successful changes.
- Preserve queued-key/redraw ordering, undersized-window action blocking and terminal text escaping.
- UI failure or cancellation must restore usable controls and preserve prior snapshots. Native UI tests need an STA thread with a persistent Windows message loop.
- Never use live GUID/MAC changes, DHCP renewal, DNS flushing, Roblox cleanup or privileged recovery as a UI test. Use injected probes and fictional fixtures.

## Working practices

Extend the responsible layer. Avoid new providers or services without a demonstrated need. Run the smallest relevant proof, then the build script for code changes and the docs validator for prose/media changes. Test data must not touch real profiles.

Read `CONTRIBUTING.md` and `SECURITY.md`. Do not commit profiles, snapshots, recovery files, device identifiers, `bin`, `obj` or `.artifacts`. Keep documentation claims bounded by actual observations; do not promise unlinkability, client compatibility or unimplemented isolation.
