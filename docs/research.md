# Research and feasibility

Initial research 2026-10-15; environment-boundary revision 2026-10-16. This is documentation research plus inspection of historical source code. No Roblox login, client launch, account modification, or hardware change was performed. Claims about the current client require the experiments below.

## Account identity and client launch

**Official account switching:** Roblox documents a web account switcher for up to five accounts, available to eligible users aged 13+ without parent privileges. That feature establishes a supported switching workflow, not simultaneous desktop clients. Roblox also says it may associate accounts belonging to the same user. Separate browser profiles therefore must not be described as making accounts unlinkable. [Roblox Account Switching](https://en.help.roblox.com/hc/en-us/articles/21037888001044-Account-Switching).

**Official OAuth:** Open Cloud OAuth grants access to scoped Roblox resources; OIDC identifies the user. Public clients use PKCE. The reviewed documentation does not establish a game-client login or multi-instance launch grant. A desktop application cannot assume that “Sign in with Roblox” provides either. [OAuth overview](https://create.roblox.com/docs/cloud/auth/oauth2-overview), [implementation guide](https://create.roblox.com/docs/cloud/auth/oauth2-develop).

**Historical implementation evidence:** The original Roblox Account Manager is archived. At revision `c2b13d146d5db609d499870145e8d2dff2506581`, its account code binds a session cookie to a user, requests temporary authentication tickets, and constructs native launch requests. Its manager separately handles a Windows singleton mutex. These are two mechanisms: account authentication and multiple processes. Neither constitutes hardware identity spoofing. The inspected code is evidence of a past approach, not proof of current compatibility or a dependency recommendation. [Account.cs](https://github.com/ic3w0lf22/Roblox-Account-Manager/blob/c2b13d146d5db609d499870145e8d2dff2506581/RBX%20Alt%20Manager/Classes/Account.cs), [AccountManager.cs](https://github.com/ic3w0lf22/Roblox-Account-Manager/blob/c2b13d146d5db609d499870145e8d2dff2506581/RBX%20Alt%20Manager/AccountManager.cs), [repository status](https://github.com/ic3w0lf22/Roblox-Account-Manager).

**Compatibility limits:** Roblox's anti-cheat support page warns about modified clients and interfering software, and describes restrictions for emulators and virtual machines. This prevents treating a VM, copied client, or historical singleton workaround as an automatically supported deployment. The design needs real current-client validation and must not promise protection from enforcement. [Roblox Anti-cheat Messages](https://en.help.roblox.com/hc/en-us/articles/24275616578708-Anti-cheat-Messages).

**Development testing:** Studio provides local server/client testing. That can validate an owned experience's test instrumentation; it cannot establish authentication or concurrency of separate production accounts. [Studio testing modes](https://create.roblox.com/docs/studio/testing-modes).

## Device identity

There is no single universal “HWID” established by the reviewed sources. Keep identifiers and observation methods explicit:

| Evidence | Architectural implication |
| --- | --- |
| Microsoft describes a software-configurable NIC address and advanced adapter properties | A driver-dependent MAC override is a candidate limited operation; verify the effective address, not just a saved setting |
| WMI exposes system UUID and disk serial properties as read-only | Reading them is supported; changing them through these properties is not |
| WebView2 supports separate browser profiles/data | Browser session isolation is available, but does not create separate physical hardware |

Sources: [NDIS network address](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/ndis/nf-ndis-ndisreadnetworkaddress), [adapter property discovery](https://learn.microsoft.com/en-us/powershell/module/netadapter/get-netadapteradvancedproperty), [adapter property updates](https://learn.microsoft.com/en-us/powershell/module/netadapter/set-netadapteradvancedproperty), [system UUID](https://learn.microsoft.com/en-us/windows/win32/cimwin32prov/win32-computersystemproduct), [disk serial](https://learn.microsoft.com/en-us/windows/win32/cimwin32prov/win32-diskdrive), [WebView2 profiles](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/multi-profile-support).

**Design inference:** a host-wide change is shared by clients on that host. An account-to-profile mapping cannot override this scope. An adapter address change alone does not guarantee a new public IP, replace board/disk identifiers, or prove a different Roblox device association. No reviewed primary source specifies a complete Roblox fingerprint or demonstrates that changing these values provides the desired result.

## Desktop Player and environment boundaries

Rechecked 2026-10-16 against the primary sources below. These are documentation findings, not measurements from a running Player.

| Primary source | Finding | Design consequence |
| --- | --- | --- |
| [Roblox installation and play](https://en.help.roblox.com/hc/en-us/articles/204473560-How-to-Install-and-Play-Roblox) | Desktop Play opens the installed Roblox application | The browser and native process must both stay inside the chosen environment |
| [Roblox privacy policy](https://en.help.roblox.com/hc/en-us/articles/115004630823-Roblox-Privacy-and-Cookie-Policy) | Describes collection of IP addresses and device identifiers | Public documentation establishes broad data categories, not a complete current Player fingerprint |
| [Roblox account switching](https://en.help.roblox.com/hc/en-us/articles/21037888001044-Account-Switching) | Says several factors inform account association | Different local values cannot prove that accounts are unrelated |
| [Roblox anti-cheat messages](https://en.help.roblox.com/hc/en-us/articles/24275616578708-Anti-cheat-Messages) | Documents VM rejection and mentions GPU passthrough | Test the exact guest/GPU/client combination before selecting a VM deployment |
| [Windows container isolation](https://learn.microsoft.com/en-us/virtualization/windowscontainers/manage-containers/hyperv-container) | Process isolation shares the host kernel; Hyper-V isolation uses a separate kernel | Namespace separation and full guest isolation are different; neither establishes Player compatibility |
| [Hyper-V NAT](https://learn.microsoft.com/en-us/windows-server/virtualization/hyper-v/setup-nat-network) | Multiple guest endpoints can use the host address through translation | Different guest IPs do not establish different public egress |
| [Windows system UUID](https://learn.microsoft.com/en-us/windows/win32/cimwin32prov/win32-computersystemproduct) | The read-only UUID observation comes from SMBIOS | An application-generated UUID or arbitrary registry value does not replace this hardware observation |

The privacy policy also states that experience creators are not given players' IP addresses. An owned test experience can confirm joined accounts and simultaneous presence, but it cannot serve as an IP or hardware-fingerprint oracle. Network evidence must come from authorized observations of the environment and its routing.

The proposed boundary is one complete Windows environment per active account, with its browser, local state, official Player, and measured network path. The [architecture](architecture.md) compares full VMs with separate physical devices. A guest OS can separate registry and disk state while still exposing common CPU or graphics characteristics. No reviewed source establishes a way to make every reported PC parameter independent.

Current code creates per-profile WebView2 folders and cancels external launches in `BrowserLogin`. Version 0.3's separate `BrowserExperience` checks a saved browser account and permits one bounded desktop handoff; Player process/account attribution is unverified. `InstallationDiscovery` only finds executable candidates. Diagnostics delegate platform observations to `EnvironmentInspection`. Fixed stored GUID/MAC changes and scoped cleanup now have local recovery files, documented in [system identity](system-identity.md). There is no VM provisioner, public-IP routing or firmware spoofing implementation.

### Limits of the proposed experiment

Separate these outcomes in the evidence:

- Account isolation: which verified user is saved and which user actually joined.
- Environment isolation: where the browser and Player execute and which state they own.
- Network observations: which routes and public addresses were measured, for which traffic, and when.
- Service-side association: unknown; local results do not establish Roblox's internal decision.

No interception of Roblox process memory or changes to its anti-cheat are needed to test the first three properties. A check of one HTTP request cannot establish the route of all game traffic. A shared NAT address is compatible with separate devices and must not be labelled as proof of shared hardware.

## Stack evidence

- [.NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy) lists .NET 10 as LTS. Use a supported servicing release at implementation time rather than freezing this document's date into a patch requirement.
- [Spectre.Console](https://spectreconsole.net/) supplies tables, keyboard prompts, and live displays. Use menu screens and separate progress views, avoiding unsupported assumptions about concurrent prompts inside live rendering.
- [WebView2 user data folders](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/user-data-folder) provide application-owned browsing state. This is a platform capability; Roblox login acceptance still needs testing.

C#/.NET 10 and Spectre.Console are used by the CLI, and WebView2 now hosts the account login window. That window has not been run against a real Roblox sign-in, so the account-login investigation below remains **not run**. This does not validate desktop launch, environment isolation, or network separation.

## Feasibility work before implementation

| Investigation | Required evidence | Failure consequence |
| --- | --- | --- |
| Account login | Separate profiles return different verified users; challenges and re-login complete normally | Revisit login integration |
| One complete environment | Official Player runs; browser, profile, protocol handler, and native process remain in that environment | Do not automate that deployment |
| VM compatibility, if chosen | Exact Windows/Player/hypervisor/GPU configuration launches and plays normally | Keep VM support unavailable; evaluate physical environments |
| Account-specific launch | The intended account joins an owned experience through the official flow inside its environment | Do not claim selected-account launching |
| Network scope | Observe relevant browser and Player routes, public IPv4/IPv6 where available, and route-failure behavior | Do not claim distinct or enforced egress |
| Three simultaneous environments | Three verified users remain present for 10 minutes; stopping A leaves B and C connected | Three-account requirement remains unfulfilled |
| Process attribution | Resolve actual game processes through bootstrap/update behavior; detect PID reuse and uncertain account mapping | Disable targeted stop and reattachment |
| Restart and update | Preserve account data; report unknown state without duplicate launches; retest changed configurations | Invalidate stale readiness evidence |

Record app, Windows, Roblox, and WebView2 versions; the environment and graphics configuration; time; procedure; observed outcome; and limits. Include network measurement scope when relevant. Retain only necessary redacted evidence. Every row remains **not run**.

The new sequence replaces same-host concurrency and host-identity mutation as the next feasibility work. Historical launch and adapter research above remains evidence about those narrower mechanisms, not an implementation commitment.

Start with one complete environment, then repeat for three. A successful process spawn is insufficient, and a clean local inventory does not demonstrate account unlinkability. After a Player, environment, or routing change, recheck the affected result before showing it as ready.
