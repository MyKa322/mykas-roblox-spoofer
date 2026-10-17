# System identity, comparison and recovery

Version 0.3 adds a local Windows identity window. Open the Identity tab with 4, then press Enter or O. The tab lists available operations, inspect-only functions and limits. You can also press S in Adapters, choose System identity in the fallback menu, or run:

~~~powershell
.\.artifacts\publish\myka.exe identity manage
~~~

Capture Before, review and apply selected operations, capture After, then press Compare Before / After. No operation is selected by default. Firmware observations and stored registry settings are shown separately.

The tools window uses the main menu's dark background, monospaced text and cyan selection. It has three tabs:

- Snapshots contains capture, comparison, export, load and restore actions. Before and After have separate timestamps and appear together when both are available. The values table scrolls horizontally when the window is narrow.
- Operations contains the selection list and Apply selected. The list scrolls independently, so Apply stays visible. A standard user can select the scoped cleanup tools; stored GUID and network options require an elevated terminal.
- Results keeps messages and recovery backup paths for the current window session. The footer's Results button opens it from any tab. Export a snapshot or keep the recovery file to continue after closing the window.

Cancel current, or Esc during work, requests cancellation without closing the window. Once the current step finishes, you can retry. Close waits for that cancellation before closing. When idle, Esc closes the window. Cancellation does not undo completed system steps; read Results and capture After before applying anything again.

Snapshot probes and operation/file work run away from the UI thread. A failed capture leaves existing snapshots available, returns the controls to their ready state and opens Results. The window's minimum size grows with larger text so actions and values remain visible.

## Scope

| Item | Implemented behavior | Limit |
| --- | --- | --- |
| System UUID | Read `Win32_ComputerSystemProduct.UUID` through CIM/WMI | No firmware or WMI-provider spoofing |
| Baseboard serial | Read `Win32_BaseBoard.SerialNumber` | No motherboard firmware writes |
| Physical drive serial | Read `Win32_DiskDrive.SerialNumber` | No drive-controller or firmware modification |
| Volume serial | Read fixed-volume serials with `GetVolumeInformationW` | No raw filesystem edits or reformatting |
| Monitor EDID | Compare SHA-256 hashes of available registry EDID blobs | No display-driver or monitor firmware writes |
| `MachineGuid`, `HwProfileGuid`, `ComputerHardwareId` | Optional replacements of existing, normally terminated GUID `REG_SZ` values | Does not replace firmware-derived identifiers or regenerate Windows hardware-ID sets |
| Registry BIOS mirrors | Inspect the requested `BaseBoardSerialNumber` and `SystemUUID` values if exposed | Missing values are not synthesized |
| Special REG_SZ encoding | Inspect trailing NUL and embedded NUL in fixed identifier values | No malformed strings or hidden value names are created |
| MAC addresses | Generate locally administered unicast overrides for every eligible six-byte adapter whose driver advertises `NetworkAddress` support | Unsupported adapters are skipped; actual driver application needs separate verification |
| Launcher cache | Move existing Roblox `logs` and `http` folders to quarantine | No broad filesystem erasure |
| Registry cleanup | Back up and clear the existing `HKCU\SOFTWARE\Roblox\RobloxPlayer` tree when selected | No broad registry sweep |
| IP address | Request IPv4 DHCP renewal on active DHCP adapters | A different local or public IP is not guaranteed |
| DNS cache | Run the built-in `Clear-DnsClientCache` command | Does not change DNS servers, routing or public IP; cannot be undone |

The writable identifier locations use the 64-bit registry view:

| Path under `HKLM` | Value |
| --- | --- |
| `SOFTWARE\Microsoft\Cryptography` | `MachineGuid` |
| `SYSTEM\CurrentControlSet\Control\IDConfigDB\Hardware Profiles\0001` | `HwProfileGuid` |
| `SYSTEM\CurrentControlSet\Control\SystemInformation` | `ComputerHardwareId` |

An absent, malformed or differently typed GUID value blocks that selected operation during preparation. The application does not create a replacement key to make an operation appear supported. Encoding observations use the byte count returned by `RegQueryValueExW`; they do not assume that a string has a terminator. Embedded-NUL registry names are not scanned or created.

GUID changes and network operations require an elevated terminal. The application does not elevate itself, restart adapters, reboot Windows, install a driver or modify RobloxPlayer. Stored GUID changes can affect Windows and installed software. Review the old and proposed values before applying.

## Backups and cleanup

Create backup file exports the Before snapshot as JSON. This observation file contains device identifiers and local IPs; keep it private. Exports use private Windows file permissions and refuse to overwrite an existing file.

Apply selected prepares a fresh Before snapshot, records original GUID/MAC values and selected Player preferences, then shows a review. After confirmation, it writes a recovery file before the first selected change:

~~~text
%LOCALAPPDATA%\MyKa\RobloxSpoofer\identity-backups\<operation-id>.json
~~~

If that write fails, no changes begin. Selected cache folders are moved from `%LOCALAPPDATA%\Roblox\logs` and `http` into `<operation-id>\logs` and `<operation-id>\http` beside the JSON. Keep those folders with the recovery file. Their contents are not embedded in JSON. Result journals in the same directory record partial failures and cancellation.

Cleanup excludes installed versions, browser profiles, account-store data and unrelated files. Quarantined files are not permanently erased. Close RobloxPlayer and Studio before cleanup or restore; the application checks for clients without terminating them.

Snapshot and recovery schemas start at version 1. Imports have size/depth limits and reject unknown fields, invalid targets, path traversal, duplicate settings, unsupported schemas and malformed registry trees. Recovery targets are fixed in code, so imported data cannot select arbitrary registry or filesystem roots. Linked paths are refused.

## Compare and restore

Compare shows Unchanged, Changed, Added, Removed or Unavailable. Failed observations do not count as successful changes. A missing row in an incomplete inventory is conservatively unavailable. Configured MAC overrides and effective adapter MACs have separate rows. Restart the adapter or reboot through Windows when appropriate, then capture After again.

Load Before / backup accepts an exported snapshot or a recovery file, so comparison can continue after restarting the app or Windows. Read-only CLI commands are also available:

~~~powershell
.\.artifacts\publish\myka.exe identity snapshot --output before.snapshot.json
.\.artifacts\publish\myka.exe identity snapshot --output after.snapshot.json
.\.artifacts\publish\myka.exe identity compare --before before.snapshot.json --after after.snapshot.json
~~~

Snapshot export returns 1 when any observation is unavailable, while preserving useful observations in the file. Compare returns 1 if a comparison includes unavailable observations. These commands support redirected output.

Restore changes checks the Windows user, computer name and original or recorded replacement MachineGuid before offering recovery. A registry setting is restored only if it still has its recorded replacement, or is already original. A later unrelated value is preserved. An originally absent MAC override is restored by deleting the override. No factory MAC is invented.

Folder restore refuses an existing destination rather than merging or overwriting it. Player preference restore similarly refuses a different existing tree. DHCP leases and flushed DNS contents cannot be recovered. Restore can require administrator rights and a later adapter restart/reboot.

Operations apply individually. A failure or cancellation can leave a partial result. Keep the backup, read the messages and capture After before retrying. A per-user lock prevents two copies of these tools from applying/restoring simultaneously. Compare-before-write checks detect intervening registry changes; there is no single transaction spanning registry, files and network requests.

## Verification limits

These tools operate on the current Windows installation. They do not create three independent computers, configure public egress or prove Roblox account unlinkability. The [environment architecture](architecture.md) still applies to separate running clients.

Automated tests cover comparison, export permissions, refusal to overwrite, fixed targets, registry encoding, stale-value checks, typed recovery data and command validation. A live read-only snapshot was collected. Real identifier changes, DHCP renewal, DNS flushing, Roblox cleanup and privileged recovery have not been exercised by the agent. Validate them on a disposable Windows installation before relying on them.

Native UI tests also cover minimum-size layout, larger fonts, simulated scaling, long values, cancellation, capture failures, retry and repeated closing/reopening. The [UI review](review-2026-10-18.md) records the rendering fixes and remaining manual checks.

## Platform references

- [System UUID from SMBIOS](https://learn.microsoft.com/en-us/windows/win32/cimwin32prov/win32-computersystemproduct)
- [Baseboard serial observation](https://learn.microsoft.com/en-us/windows/win32/cimwin32prov/win32-baseboard)
- [Physical-drive observations](https://learn.microsoft.com/en-us/windows/win32/cimwin32prov/win32-diskdrive)
- [Volume and physical drive serial distinction](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-getvolumeinformationa)
- [Driver-configured network address](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/ndis/nf-ndis-ndisreadnetworkaddress)
- [Registry string lengths and NUL terminators](https://learn.microsoft.com/en-us/windows/win32/api/winreg/nf-winreg-regqueryvalueexw)
- [DHCP renewal and completion codes](https://learn.microsoft.com/en-us/windows/win32/cimwin32prov/renewdhcplease-method-in-class-win32-networkadapterconfiguration)
- [Clear-DnsClientCache](https://learn.microsoft.com/en-us/powershell/module/dnsclient/clear-dnsclientcache)
