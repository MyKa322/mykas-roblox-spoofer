# Contributing

Use a Windows x64 machine with the .NET 10 SDK and Windows PowerShell 5.1 or PowerShell 7. The SDK version policy is in [global.json](global.json). WebView2 Runtime is needed for account sign-in, but the automated tests use fixtures and do not sign in to Roblox.

## Set up and run

From the repository root:

```powershell
.\scripts\Build.ps1
dotnet run --project src/MyKa.Spoofer -c Release -- --still
```

The build script restores from the checked-in NuGet configuration, runs the Release suite, publishes to `.artifacts/publish`, verifies the published assembly and creates a ZIP with a SHA-256 file. Use `-NoRestore` only when the required packages are already restored:

```powershell
.\scripts\Build.ps1 -NoRestore
.\scripts\ValidateDocs.ps1
```

Each build starts with fresh publish output. If `.artifacts/publish` exists, the script preserves it in a unique `.artifacts/publish.previous-*` folder before publishing. Only the new output enters the ZIP; you can remove older build folders when you no longer need them.

Do not commit `bin`, `obj`, `.artifacts`, account profiles, snapshots or recovery files. No `.env` file or service credentials are required.

After a successful build on `main`, GitHub Actions publishes the project version when it has no existing release or tag. To prepare another release, update the application project's version and `TerminalUi.Version`, then add the matching changelog entry. The release job checks the ZIP checksum, creates a draft at the tested commit, verifies both uploaded assets and then publishes. Existing published versions stay unchanged. Pull requests cannot run the release job; only that job receives the temporary repository write token.

## Make a change

Open an issue for a substantial feature or change to storage, recovery or browser permissions. For a small fix, a pull request with a clear reproduction is enough. Create a branch, keep the change focused and describe the observable behavior before and after it.

Follow the existing C# style and nullable annotations. The application treats compiler warnings as errors. Keep domain rules in their owning layer: account persistence in `AccountStore`, browser restrictions in the browser windows and shared restrictions, identity target validation and recovery in `Identity`, and presentation state in the terminal pages or native window.

Preserve these rules:

- Save an account only after Roblox confirms its numeric identity and the user chooses Save. Re-login must match the saved account.
- Keep account operations exclusively locked, metadata writes atomic and bounded, and linked storage paths refused. A failed read must remain unavailable rather than appear to be an empty list.
- Keep browser navigation, new windows, downloads, permissions and desktop protocols bounded by the existing allow-lists and handoff state.
- Prepare and confirm identity operations, then persist a recovery file before the first change. Imported files cannot choose arbitrary registry keys or filesystem roots.
- Preserve later conflicting values and existing folders during restore. Cancellation can leave completed steps in place; report that state accurately.
- Keep native work off the UI thread. Return controls to a usable state after cancellation or failure, and keep prior snapshots available.
- Never describe a requested handoff as a verified Player launch or a stored setting as proof of device separation.

## Validate the behavior

Run `scripts/Build.ps1` before submitting a code change. Add a regression test when it can catch the reported behavior; avoid tests that merely repeat the implementation. Documentation and media changes also need `scripts/ValidateDocs.ps1`.

Native UI tests run on an STA thread with a persistent Windows message loop and cross-thread checks. Preserve that harness. Use injected probes and synthetic snapshots for window tests. Do not use real GUID/MAC changes, DHCP renewal, DNS flushing, cache cleanup or privileged restore to verify a UI change.

The screenshot and GIF generator in `tools/ReadmeMedia` uses the actual terminal views and native controls with fictional data. Keep that distinction in captions. Do not capture a real signed-in account or a hardware snapshot for public documentation.

Manual tests involving Roblox sign-in, desktop handoff or system changes need a separately prepared test environment. Record what you observed and what remains unverified. A passing unit suite does not establish client compatibility, network isolation or successful recovery on a real modified installation.

For rendering changes, report the terminal/window dimensions, Windows scaling, font size, motion setting and whether keyboard selection survives resize, cancellation and reopening. Test plain output where the change affects terminal behavior.

## Submit the pull request

Explain the problem, final behavior and validation. Include redacted screenshots or fixture media for visible UI changes. Identify any storage, permissions or recovery impact, and update the relevant documentation. Avoid unrelated formatting or generated-file changes.

Public bug reports must exclude browser profile files, cookies, passwords, authorization URLs, device identifiers and recovery backups. For a vulnerability, follow [SECURITY.md](SECURITY.md).

## Using Codex

Read [AGENTS.md](AGENTS.md) before editing. Give the agent a concrete reproduction and acceptance criteria. Review generated changes and their tests before opening a pull request. Keep live credentials and private machine observations out of prompts, logs and attachments.
