# ADR-0002: One execution environment per active account

**Date**: 2026-10-16  
**Status**: proposed; replaces the host identity-helper direction in ADR-0001 for concurrent device separation  
**Deciders**: User requested distinct machine and network environments; this record proposes the implementation boundary.

## Context

The existing application separates WebView2 account profiles. The user clarified that the native RobloxPlayer must also run with separate machine and network state for three concurrent accounts.

A browser profile does not contain the desktop process. Host-wide identifier changes affect shared resources, so rotating one host identity cannot provide three independent machine environments at the same time. The old identity-helper proposal does not meet this requirement.

## Decision

Treat the complete execution environment as the unit of deployment. Keep its browser, profile, protocol handler, official Player, OS state, and network path together. Begin with one active account and one managed Player per environment.

Retain C#/.NET 10, Spectre.Console, WebView2, and the current account-store boundaries. Run the existing application inside each environment during feasibility. Provision environments with existing platform tools and launch manually through the official website until a launch integration is validated.

Evaluate a full Windows VM for a single-PC deployment. Record the actual hardware and current Player result before selecting a hypervisor or building automation. Separate physical Windows machines provide a baseline for actual hardware separation. Neither option establishes distinct public egress by itself.

Roblox documents VM rejection and mentions GPU passthrough. Compatibility must be tested on the intended configuration. [Roblox support](https://en.help.roblox.com/hc/en-us/articles/24275616578708-Anti-cheat-Messages).

Measure network routing separately from device isolation. A distinct-egress claim requires evidence from the native client path as well as the browser. Local observations cannot prove that Roblox considers accounts unrelated.

## Alternatives considered

| Alternative | Reason it does not meet the full requirement |
| --- | --- |
| Browser profiles or Windows users | Separate some account state while retaining a shared machine |
| Host registry/UUID/MAC changes | Change shared state; do not create concurrent independent environments |
| Process-isolated containers | Share a kernel and have no demonstrated Player deployment here |
| Immediate multi-host coordinator | Adds credentials, transport, and failure handling before runtime feasibility is known |
| Custom hardware emulation or client patching | Adds unsupported machinery without evidence that it solves the observed requirement |

## Consequences

The first test requires no new service, privileged helper, or dependency. It can reuse the existing app on a separate machine or a provisioned guest. Its cost is greater OS, memory, storage, and graphics demand than browser-profile separation.

Current host account records remain valid and local. New environments require fresh sign-in; authenticated profiles and snapshots are not cloned as a migration strategy. Existing commands and storage schemas do not change in this documentation revision.

The identity helper and host apply/restore UI from ADR-0001 are removed from the active implementation path. Read-only adapter inspection remains. If centralized control becomes necessary after the three-environment test, design one authenticated control path with environment-local credentials and process ownership.

## Acceptance

1. One environment runs the official Player with the intended account and without redirecting the launch back to the host.
2. Three environments sustain three distinct accounts in an owned test experience for 10 minutes; stopping one leaves the others running.
3. Any claimed distinct egress is observed for the relevant browser and Player traffic, including route-failure behavior.
4. Unverified compatibility, process ownership, or network state is reported explicitly; no test result is labelled as Roblox account unlinking.

These are proposed tests, all unrun. This decision does not implement or demonstrate hardware spoofing.

## References

- [Architecture](../architecture.md)
- [Research and platform evidence](../research.md#desktop-player-and-environment-boundaries)
- [ADR-0001](0001-windows-terminal-and-identity-boundary.md)
