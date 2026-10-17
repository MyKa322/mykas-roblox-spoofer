# Proposed architecture

Updated 2026-10-18. Version 0.3 implements account sign-in, verified local records, experience-page desktop handoff, local identity observations, reviewed stored GUID/MAC changes with recovery backups, scoped cleanup, the terminal interface and diagnostics. Player process/account attribution, firmware spoofing, environment management and public-IP routing remain unimplemented. See [current capabilities](../README.md).

[ADR-0002](adr/0002-account-execution-environments.md) revises the deployment model: each concurrently active account needs its own complete execution environment if separate machine state is required. It replaces the proposed host identity helper as the route to that requirement. C#/.NET 10, Spectre.Console, WebView2, and the existing account implementation remain suitable.

## Product contract

The requested outcome is three accounts running concurrently with separate machine and network environments. The application should let an owner select an account and experience, identify the environment that will run them, check its readiness, and see observed client status.

The testable contract is environment separation, correct account selection, and observed network routing. It cannot include a guarantee that Roblox considers the accounts unrelated. The reviewed public sources do not disclose its complete device fingerprint or association algorithm. [Evidence and limits](research.md#desktop-player-and-environment-boundaries).

Initial platform: Windows 11 x64 with the official desktop client. Keep the keyboard-friendly terminal and [3D ASCII branding](../assets/logo.txt). The first feasibility exercise uses one environment; the target acceptance exercise uses three.

## Browser and desktop boundary

On desktop, the website starts the installed Roblox Player through the operating system. Browser cookies choose a web session; they do not contain the native client or determine all of its machine observations. A browser profile on the host followed by a host protocol-handler launch still runs a host client.

A complete environment must contain the login browser, its persistent profile, the protocol handler, RobloxPlayer, Windows state, and the network path used by those processes. Starting the browser inside a guest and forwarding its launch URI to the host breaks that boundary.

Account sign-in and desktop handoff have separate windows:

| Code | Existing responsibility |
| --- | --- |
| [Program](../src/MyKa.Spoofer/Program.cs) | Parses commands and dispatches account or diagnostic operations |
| [AccountOperations](../src/MyKa.Spoofer/Accounts/AccountOperations.cs) | Reserves a local profile, runs sign-in, commits a verified account |
| [BrowserLogin](../src/MyKa.Spoofer/Accounts/BrowserLogin.cs) | Creates WebView2 with the reserved user-data folder; cancels external URI launches |
| [BrowserExperience](../src/MyKa.Spoofer/Accounts/BrowserExperience.cs) | Checks the saved browser identity, opens one place, permits one user-initiated Roblox desktop request; does not verify Player |
| [AccountStore](../src/MyKa.Spoofer/Accounts/AccountStore.cs) | Owns local metadata, account/profile binding, locking, and cleanup |
| [InstallationDiscovery](../src/MyKa.Spoofer/Diagnostics/InstallationDiscovery.cs) | Finds candidate RobloxPlayerBeta.exe files; does not launch or validate them |
| [SystemDiagnostics](../src/MyKa.Spoofer/Diagnostics/SystemDiagnostics.cs) | Reports runtime, installation, and adapter observations; calls the environment inspection |
| [EnvironmentInspection](../src/MyKa.Spoofer/Diagnostics/EnvironmentInspection.cs) | Reads edition, CPU, RAM, exposed virtualization flags, and session display-adapter names; preserves unavailable results |
| [IdentityOperations](../src/MyKa.Spoofer/Identity/IdentityOperations.cs) | Applies fixed reviewed Windows operations after a durable backup; preserves conflicting later values on restore |

The sign-in window keeps its existing restrictions. The experience window separately validates place, source, origin, gesture and single-attempt state. Its result is a requested handoff, not verified process/account ownership. See the [experience guide](desktop-launch.md).

## What an environment separates

The following describes platform boundaries, not a confirmed list of Roblox telemetry.

| Resource | Boundary and implication |
| --- | --- |
| Browser cookies and local storage | Separate WebView2 profiles separate this browser state |
| User files and per-user registry | Windows users can separate much of this state, while sharing the machine and kernel |
| Machine registry and OS installation state | An independent guest OS has its own state; a host registry edit still changes shared host state |
| Process memory | Processes have separate virtual address spaces already. This does not give them different hardware; guest memory isolation also does not invent different physical RAM |
| System UUID and hardware serials | These belong to hardware or the virtual hardware exposed by a hypervisor. Microsoft exposes the system UUID as a read-only SMBIOS observation, not a universal writable HWID |
| CPU/GPU characteristics | Guests can still expose common host characteristics. GPU passthrough exposes a physical device and its characteristics; supported device assignment still needs validation |
| MAC address and private IP | Local-link/network values; neither establishes a distinct public source address |
| Public IPv4/IPv6 | A property of the effective network route and translation. Distinct local interfaces or guest addresses can use the same internet egress |
| Roblox device/account association | Service-owned state. Local inspection cannot establish whether accounts have been linked or unlinked |

A fresh UUID in application metadata has no effect on these platform resources. Changing a host registry value, restarting a process, or clearing a browser profile cannot satisfy the full environment contract. There is no evidence here that changing RAM size or an arbitrary registry value changes Roblox's device decision.

## Deployment choice

| Option | Useful isolation | Limit for this product | Decision |
| --- | --- | --- | --- |
| Three browser profiles | Login state | Native clients still share their execution environment | Keep for account management only |
| Separate Windows users | User state | Machine resources and default network egress remain shared | Insufficient for distinct machines |
| Process-isolated containers | Some filesystem, registry, and network namespaces | Share the host kernel; desktop-client compatibility is unverified | Do not select as the Player runtime |
| Full Windows VMs | Guest OS, registry, disks, and virtual devices | Roblox compatibility, graphics, resource cost, and exposed host characteristics need testing | Candidate for a single-PC deployment |
| Separate physical Windows PCs | Separate OS installations and actual hardware | Additional machines and operating cost; a shared router can still mean shared public egress | Baseline for genuine device separation |

Hyper-V-isolated containers have a separate kernel, but that alone does not establish a usable Roblox desktop environment. A VM also does not automatically obtain a new public IP. See [Microsoft platform evidence](research.md#desktop-player-and-environment-boundaries).

Roblox's support page documents VM rejection and mentions GPU passthrough. That is a compatibility prerequisite to investigate, not proof that a particular hypervisor, GPU arrangement, or three-guest setup will work. Do not choose a hypervisor or promise a single-PC solution before checking the actual Windows edition, CPU virtualization support, RAM, GPU allocation, and current Player build.

Use existing hypervisor or physical-machine management tools for provisioning. No custom hypervisor, kernel driver, memory patcher, firmware writer, or anti-cheat modification belongs in this proposal. Host identity mutation no longer owns the isolation requirement.

## Components and ownership

The diagram describes the proposed three-environment layout. Coordination is manual during feasibility; no remote-control service is implemented or required for that first test.

~~~mermaid
flowchart TB
    User["User / proposed terminal coordinator"]
    User --> A["Environment A: Windows + browser/profile A + official Player A"]
    User --> B["Environment B: Windows + browser/profile B + official Player B"]
    User --> C["Environment C: Windows + browser/profile C + official Player C"]
    A --> EA["Measured egress A"]
    B --> EB["Measured egress B"]
    C --> EC["Measured egress C"]
    EA --> Roblox["Roblox services"]
    EB --> Roblox
    EC --> Roblox
~~~

Different egress labels must correspond to observed routing if network separation is claimed. The diagram does not prove different public IPs or different service-side identities.

| Owner | Responsibility |
| --- | --- |
| Existing terminal application in each environment | Account login, local storage, diagnostics; later, that environment's launch and process supervision |
| Windows or selected hypervisor | OS, device, disk, memory, and process boundaries |
| Network configuration for that environment | Routing and enforcement for its browser and Player traffic |
| User during feasibility | Provisioning, official browser launch, account confirmation, and evidence collection |
| Future coordinator, only after feasibility passes | Environment/account selection and status; credentials stay in the environment |

Reuse the same application inside each environment. Do not add a generic provider framework, agent protocol, or database before a proven deployment needs them. If a central coordinator is later justified, it should send bounded actions and receive observations through an authenticated channel; it must not copy session cookies or treat remote process IDs as local handles.

### Current implementation flow

`Program` owns cancellation on the STA thread. `TerminalUi` chooses full-screen tabs or a fallback menu. `TabsApp` owns the alternate screen, key ordering, and resize loop. `TabsModel` routes global keys; pages own their selection, messages, and confirmation state. `TabsLayout` and `TabsView` produce a bounded frame. Undersized windows accept no hidden page actions, and each queued key is separated by a redraw.

The model owns motion preference and the scrollable `HelpPage`; closing help restores the existing page. Account-read failures have an unavailable state with retry, so actions cannot proceed against an unknown list. Read-only pages and CLI reports share diagnostics and wording.

Account pages and CLI commands use `AccountOperations`. Browser actions exclusively lock `AccountStore`; sign-in commits verified metadata, while experience opening reuses the saved profile without rewriting metadata. Reads and writes share a 1 MiB limit. `BrowserLogin` owns native Verify/Save controls, and `BrowserExperience` owns the bounded handoff attempt. Both use shared browser restrictions.

## Account login and launch

### Authentication boundary

Each saved account has an application-owned browser profile. Users enter credentials and complete challenges on Roblox. The application does not read personal browser profiles, save passwords, or collect MFA codes. Current code reads only the authenticated identity response after the user chooses Verify, then saves after confirmation.

For environment isolation, sign in inside the intended environment. Existing host profiles stay local; they are not silently moved, cloned, or treated as guest credentials. Create fresh profiles in new environments and authenticate there. A reusable VM image must be prepared before account sign-in.

The first feasibility launch uses the official website inside the intended environment. The implemented experience window can request a handoff from the saved WebView2 profile after checking its account; that live flow still needs validation. A normal external browser has its own profile and requires its own sign-in. In either case, confirm which account actually joins Player.

An automated launch path remains unproven. Historical account managers used temporary tickets, but that is not a supported public launch contract. Open Cloud OAuth documentation does not establish desktop Player login. Any later integration must keep sensitive authorization inside the environment and outside logs, clipboard, and command reports. [Launch evidence](research.md#account-identity-and-client-launch).

### End-to-end path

1. Provision one independent Windows environment using existing platform tools. Install the official client and verify that it runs without client or security modifications.
2. Start the existing app there. Add the account through its usual Verify/Save flow; preserve duplicate-user and re-login checks.
3. Confirm the browser and native launch target are inside that same environment. Record the client build and graphics configuration.
4. Observe the environment's network path. If distinct egress is required and unverified, show it as unverified and leave launch unavailable for that claimed mode.
5. Launch one owned test experience through the official browser flow. Record the account actually observed in the experience.
6. Repeat in separate environments, then test A, B, and C together. One managed Player per environment is the initial rule; there is no need to depend on a same-host singleton workaround.
7. Verify each client's process and account association before offering targeted stop. Close A and confirm B and C remain active.

### Session state and concurrency

Proposed automated supervision uses separate environment, process, and join observations:

~~~text
Environment: Not checked / Ready / Unsupported / Unreachable
Client:      Idle -> Starting -> Client running -> Exited
                         |
                         +-> Failed / Unverified
Join:        Unknown / Joined / Disconnected
~~~

A started launcher does not prove a joined game. A process name or protocol-handler return value does not prove account ownership. If process attribution is ambiguous, disable targeted control.

Within each environment, serialize startup and allow one managed active account. Hold local ownership for the session lifetime. PID, creation time, executable identity, environment identity, and account correlation must all be revalidated after restart. A PID is meaningful only on its own machine and boot.

Never automatically replay an uncertain launch. A disconnected coordinator means status is unknown, not that the Player exited. Do not kill all Roblox processes or power off an environment to implement a normal account stop. Preserve other environments when one fails. Explain whether clients remain running when the local manager exits.

## Device and hardware identity

Inventory describes observed values and their scope. It must not display a synthetic "HWID changed" or "Roblox unlinked" result.

The host apply/restore workflow does not meet the simultaneous-environment requirement in [ADR-0002](adr/0002-account-execution-environments.md). Version 0.3 adds separate opt-in maintenance tools: fixed stored GUID replacements, driver-advertised MAC overrides, DNS/DHCP operations and scoped Roblox cleanup. They run in the current process, require elevation for Windows changes, and write recovery files before mutation. They do not provide separate computers. See [system identity and recovery](system-identity.md).

Environment provisioning belongs to the selected platform's management tools. Keep guest installations stable during a session. Do not reassign devices, restore snapshots, or replace environment state while a managed client is running. A snapshot can contain valid sessions; restoring or cloning it is not proof of a new device identity.

## Network scope and verification

Network separation is independent from guest isolation. Hyper-V NAT can route multiple guests through the host address; an upstream router or carrier NAT can further share the public address. Bridging a guest onto the same LAN does not establish a distinct internet source address.

A distinct-egress deployment needs separately provisioned outbound paths that produce distinct observed public addresses. The application should observe and report that property, not assume it from a VPN name, virtual NIC, MAC address, or private IP.

A browser proxy covers only traffic that uses it. Validate the Player's path too, including IPv4, IPv6 where available, and the transports it uses. A single HTTP IP-check result is evidence only for that request. Use authorized network observations for routing evidence; an owned Roblox experience cannot reveal Roblox's private fingerprint or account-linking result.

Record public-address observations with a timestamp and their measurement scope. If required routing becomes unavailable, network enforcement must prevent fallback outside the configured path; a periodic UI check cannot enforce this. Test that failure condition before advertising isolated routing. Where this behavior has not been demonstrated, keep the capability unavailable.

## Persistence, privacy, and failures

Keep the current versioned account JSON and browser storage in each environment under `%LOCALAPPDATA%/MyKa/RobloxSpoofer`. Preserve exclusive writes, atomic replacement, backups, schema checks, and refusal of linked paths. Browser profiles remain sensitive even when JSON contains no credentials.

The first feasibility record is a local document, not a new runtime schema. The implemented `doctor` report supplies timestamped environment observations through the same report renderer used by the Diagnostics tab. Follow the [environment preflight guide](environment-preflight.md) to collect a report and run the manual trial. Record an environment label, Windows and Player versions, test date, account confirmation, process observations, network result, and limits. Store only necessary observations; redact public addresses and identifiers in exported reports.

A future session record may include environment ID, account ID, operation ID, process identity, and last observation. An environment ID is an application reference, not a spoofed hardware UUID. Do not introduce metadata migration until this feature is implemented. Existing accounts are not automatically assigned to new machines.

Removing a profile remains a local operation and does not revoke remote Roblox sessions. Future managed sessions must close before profile removal. Failed cleanup stays pending. Logs must exclude passwords, cookies, authorization tickets, raw launch URLs, full hardware serials, and unsanitized HTTP bodies. Escape terminal control characters and markup.

## Implementation order and acceptance

| Stage | Observable completion |
| --- | --- |
| Existing account slice | Real sign-in and re-login work in separate profiles, with duplicate and mismatched users rejected; still awaiting manual verification |
| One environment | Official Player launches and joins with the intended account; browser and Player remain in that environment; exact platform/build recorded |
| Network requirement | Browser and Player routing is observed; distinct public egress is demonstrated if claimed, including loss-of-route behavior |
| Three environments | Three distinct accounts join the same owned test experience for a 10-minute smoke test; closing A leaves B and C connected |
| Recovery | Manager restart, client update, and environment/network failure produce truthful unknown or unavailable states without duplicate launches |
| Automation | Only after the above: implement one proven deployment path using the existing account/UI boundaries; add a coordinator only if manual operation demonstrates a need |

Automated tests can verify state transitions, repeated requests, mismatched account/environment selection, process identity, stale observations, redaction, and malformed stores when those features exist. They cannot validate Roblox compatibility or service-side unlinkability.

The first environment inspection is implemented and has been run locally. No guest deployment, Player trial, routing change, or hardware modification has been performed. Automated tests cover diagnostic behavior, not deployment compatibility. The immediate next experiment is one complete environment; the result determines whether a single-PC VM deployment is feasible or physical devices are required.
