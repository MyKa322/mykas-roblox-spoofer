# Changelog

## 0.3.2 (2026-10-18)

### Interface

- Match System Identity to the terminal's dark palette, monospaced text and cyan selection.
- Divide the native workflow into Snapshots, Operations and Results, with persistent cancellation and close controls.
- Keep action buttons available in narrow windows and adjust minimum size, grid rows and tabs for larger fonts.
- Preserve snapshots on failed or invalid captures and allow cancellation followed by retry without restarting.
- Run probes and file/system work away from the UI thread, marshal review dialogs back to it and guard repeated disposal.
- Correct clipped tab captions, unthemed tab strips and unreadable disabled captions.

### Documentation and packaging

- Add a visual README with screenshots and GIFs generated from real views using fictional data.
- Add a detailed user guide, contributor notes, security reporting guidance and repository instructions for Codex.
- Add a Windows build workflow and reproducible local release commands.
- Publish verified Windows ZIPs and SHA-256 files through GitHub Releases after successful main-branch builds.

### Validation

The Release suite has 691 cases, including 15 native UI cases for layout, larger fonts, simulated scaling, long values, cancellation, failed captures and reopening. Physical DPI transitions, high-contrast mode and screen-reader navigation remain manual checks. Live sign-in, desktop handoff, privileged changes and recovery are not established by this suite.
