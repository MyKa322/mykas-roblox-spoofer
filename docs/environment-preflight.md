# Check one execution environment

The first implementation step adds read-only environment observations to `doctor` and the Diagnostics tab. Run the same check inside each proposed Windows host or guest. It helps collect the facts needed for a deployment choice; it does not provision a VM, change identifiers, launch Roblox, or measure public IPs.

## Run the report

From the project directory, using the published build:

~~~powershell
.\.artifacts\publish\myka.exe --plain doctor
~~~

To keep a local copy:

~~~powershell
.\.artifacts\publish\myka.exe --plain doctor > environment-report.txt
$LASTEXITCODE
~~~

The report contains device model descriptions and local software information. Review it before sharing. It does not include account cookies, passwords, system UUIDs, hardware serials, or public IPs.

In the application, open Diagnostics, use Up/Down or PageUp/PageDown to scroll, and press R for a new observation. Home and End move through the longer report. The timestamp records when collection started; available memory can change immediately afterward.

Exit code `0` means the included environment observations and installation-directory scan completed. It does not mean Roblox is installed, compatible, or ready to run. Code `1` means at least one of those checks is incomplete. Each unavailable field remains explicit, and the report preserves the other observations.

## Read the results

| Field | Meaning | Limit |
| --- | --- | --- |
| Windows edition | Edition reported by the local Windows installation | Does not prove the Hyper-V feature is installed or enabled |
| Processor | Model description exposed by Windows | In a guest this describes what that guest can see |
| CPUs for this app | Logical processors available to this application | Affinity or process limits can differ from total machine capacity |
| OS-visible RAM | Total and currently available physical memory, in GiB | This is an observation, not a reservation or a three-guest capacity estimate |
| Hypervisor flag | Whether CPUID reports a hypervisor | Does not distinguish a physical Windows host using virtualization from a guest |
| Firmware virt. | Virtualization reported available by the Windows processor-feature API | A negative result does not independently establish a disabled BIOS setting |
| SLAT | Second-level address translation exposed to the current Windows environment | An active hypervisor can affect visibility; do not conclude that the CPU lacks it |
| Display adapter | Adapter names returned for the current Windows session | Does not measure VRAM, enumerate assignable GPUs, or establish passthrough support |
| Observation status | Whether the included environment probes returned usable observations | `Collected` is not a compatibility verdict |
| Player test / Public egress | Explicitly untested properties | The diagnostic command does not exercise either |

`Unavailable` means the probe could not provide a value. A reported false CPU flag is shown as `Not exposed to this Windows environment`, so an unavailable observation is not confused with a negative result. The application does not elevate or change settings to complete the check.

## Trial sequence

1. Collect the host report and identify its Windows edition, available memory, and graphics hardware. Confirm the existing virtualization setup through its normal management tools. Do not infer GPU assignment support from the model name alone.
2. Choose one supported physical Windows environment or one full Windows guest whose graphics configuration can be validated. Use the platform's existing provisioning tools. The project has not selected or implemented a VM backend.
3. Run `doctor` inside that environment and retain the result with the Windows, Player, hypervisor, and GPU-driver versions used for the trial. A guest report must be collected in the guest.
4. Sign in with the intended account in the app and verify the saved identity. Use P in Accounts for the [saved-profile experience window](desktop-launch.md), or use the official website in a normal browser inside the same environment. The implemented handoff needs live validation. An external browser has its own login profile and needs a separate sign-in.
5. Confirm that the native Player runs in the chosen environment and that the intended account joined an owned test experience. Opening a page or starting a launcher is insufficient evidence.
6. Only after one environment works, repeat with environments A, B, and C. Keep three distinct accounts connected for 10 minutes. Close A and check that B and C remain connected.
7. Verify any claimed distinct network egress separately for relevant browser and Player traffic. Include route-loss behavior. A browser IP lookup alone does not establish the Player's route.

Stop a failed trial at its actual failure point and record the result. VM rejection, uncertain account selection, or unknown network routing must remain visible; none is a successful environment test. Local results cannot prove Roblox considers the accounts unrelated.

## Evidence to retain

Use one local record per environment and trial. The application does not yet store these trial results.

| Observation | Record |
| --- | --- |
| Environment and time | A local label, host or guest context, and UTC timestamp |
| Configuration | App, Windows, Player, WebView2, hypervisor if used, and graphics-driver versions |
| Login and launch | Intended account, identity actually observed in the experience, and launch result |
| Concurrent session test | Start/end times, three-account presence, and effect of stopping one client |
| Network test | Measurement scope, route, result, and what happened when that route was unavailable |
| Failure or unknown | Exact step, non-sensitive error, and remaining uncertainty |

Keep credentials and raw authorization URLs out of trial notes. Store local reports outside source control, for example under the ignored `.artifacts` directory. Redact public addresses and account identifiers in shared copies.

## Implementation and verification

`SystemDiagnostics.Read` calls `EnvironmentInspection.Read`. `Reports.Doctor` supplies the same fields to the CLI and the scrollable Diagnostics page. Each probe reports failure independently. Native calls load Windows system libraries, read fixed observations, and do not spawn scripts, make network requests, or write registry values.

The implementation uses existing .NET and Windows APIs, with no new package. Tests cover missing observations, access failures, CPU-flag interpretation, partial-report exit status, memory units, terminal text sanitization, and scrolling/refresh at 50x12 and 80x24. A live report was collected in the development session. Roblox installation discovery remained access-limited there, so the command correctly returned `1` even though the environment observations completed.

The live Player trial, GPU assignment, guest deployment, and network verification remain unrun. The current application test suite does not establish those capabilities.

## Primary references

- [Windows processor-feature observations](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-isprocessorfeaturepresent)
- [Current memory observations](https://learn.microsoft.com/en-us/windows/win32/api/sysinfoapi/nf-sysinfoapi-globalmemorystatusex)
- [Display-device enumeration](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-enumdisplaydevicesw)
- [Hypervisor feature discovery](https://learn.microsoft.com/en-us/virtualization/hyper-v-on-windows/tlfs/feature-discovery)
- [Hyper-V hardware requirements and active-hypervisor limitations](https://learn.microsoft.com/en-us/windows-server/virtualization/hyper-v/host-hardware-requirements)
- [Roblox VM and client compatibility guidance](https://en.help.roblox.com/hc/en-us/articles/24275616578708-Anti-cheat-Messages)
- [Project architecture](architecture.md) and [environment decision](adr/0002-account-execution-environments.md)
