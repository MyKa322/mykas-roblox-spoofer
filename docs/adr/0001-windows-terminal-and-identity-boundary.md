# ADR-0001: Windows terminal application with an isolated identity helper

**Date**: 2026-10-15  
**Status**: partially superseded by [ADR-0002](0002-account-execution-environments.md) on 2026-10-16  
**Deciders**: User supplied product requirements; implementation recommendation prepared by Codex.

The following records the original proposal. C#/.NET 10, Spectre.Console, and WebView2 are implemented. ADR-0002 replaces the unimplemented identity-helper and host-mutation direction for concurrent device separation.

## Context

The requested product combines Roblox multi-account sessions, device/hardware identity spoofing, and a friendly CLI with 3D ASCII branding. The repository was empty when this decision was proposed. Account login and terminal presentation do not need elevation, while some device changes do. Current Roblox concurrency and general hardware-spoofing feasibility are unverified.

## Decision

Propose C# on a supported .NET 10 servicing release, Spectre.Console, and WebView2 on Windows 11 x64. Keep account/session coordination in one ordinary application with small versioned JSON metadata; use a short-lived, narrowly authorized helper only for verified machine-level changes. Treat compatibility and observed identity changes as explicit capabilities, with no claimed hardware isolation from browser profiles or host-wide overrides.

## Alternatives considered

### Python with a terminal UI library

- Pros: rapid prototyping and approachable code.
- Cons: distribution and Windows browser/native integration require additional packaging and bridges.
- Why not: Windows login, process attribution, and recovery dominate this product more than scripting speed.

### Rust with a terminal UI library

- Pros: native deployment and strong memory-safety guarantees.
- Cons: more integration work for the proposed Windows browser and management surfaces.
- Why not: its benefits do not currently offset the implementation cost for this small Windows application.

### A browser/Electron application

- Pros: familiar graphical forms and web tooling.
- Cons: additional runtime/UI machinery for a terminal-first product.
- Why not: the requested primary experience is a CLI; a browser window is needed only for login.

### One permanently elevated application

- Pros: fewer process boundaries.
- Cons: browser content and account credentials share a process with machine-write privileges.
- Why not: the privilege boundary is necessary for the requested identity features.

## Consequences

### Positive

- A single implementation path owns commands and interactive actions.
- Existing terminal and browser components replace custom rendering/authentication UI.
- User state and privileged recovery state have clear ownership.

### Negative

- Windows-only scope and a WebView2 runtime requirement.
- A small administrator-protected helper requires secure installation and local IPC.
- JSON suits the small local store but requires disciplined atomic writes and schema checks.

### Risks

The historical Roblox launch and singleton approach may no longer work; run the current-client investigations before relying on it. Device overrides may be unsupported or have no relevant Roblox effect; ship only observed capability claims. General hardware mutation remains unresolved and is not replaced by MAC-only support.

Version 0.1 adopted C#/.NET 10 and Spectre.Console for a read-only diagnostic CLI. Version 0.2 adds WebView2 sign-in and local account records. The larger decision remains proposed: the identity helper is not implemented. No kernel driver or service is introduced. Revisit the stack if a verified integration cannot fit these boundaries.

## References

- [Architecture and acceptance](../architecture.md)
- [Evidence and research gates](../research.md)
- [Terminal contract](../terminal-ui.md)
