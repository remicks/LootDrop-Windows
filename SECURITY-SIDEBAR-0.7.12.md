# LootDrop Windows v2 0.7.12 — sidebar security and stability review

Review date: November 23, 2026 (America/Chicago).

## Scope

Targeted review of the new desktop docking service, window lifecycle, compact layout, local preferences, import/export isolation and native interop, plus the existing full regression suite and refreshed dependency advisory checks. Historical broad application security reviews remain available. This is evidence from development tests, not an independent audit, certification, guarantee of maximum safety, or exhaustive re-audit of every third-party component.

Beta/iOS projects, artwork originals, real version 2 libraries and browser credentials were not accessed or modified by these tests. Native checks use synthetic libraries and test windows. No provider/claim URLs are opened. Only the integration harness's own windows are maximized, hidden and closed; Explorer and display configuration are not changed.

## Design and trust boundaries

- Opt-in Windows-only docking. Default mode and existing minimum-size behavior remain normal desktop windows. Saved docking preferences can resume an enabled sidebar; hidden startup does not request a reservation before it hides.
- SHAppBarMessage registers the app's own HWND, negotiates with the shell/taskbar, and removes that registration on hide, minimize, disable, failure, final close and native destruction. No SystemParametersInfo work-area override, external process manipulation, global message hook or registry persistence is introduced.
- The native callback is Avalonia's supported per-window callback. Existing message handling is preserved; callback values are compared as numbers, never dereferenced as pointers. Repositioning is deferred/coalesced, guarded against reentry and skipped when the approved rectangle is unchanged. The callback is removed on service disposal.
- DLL imports are limited to shell32/user32 using the System32 search path. APPBARDATA uses pointer-sized handles/LPARAM and normal native alignment. Rectangles are physical pixels; preferences use logical pixels and the target display scale.
- Width, scale, monitor geometry, available height and shell-returned bounds are checked. Small/unavailable displays fail into a usable normal window. The app preserves room for other applications and negotiates around existing taskbar reservations.
- Monitor identifiers are bounded local settings; width choices are allowlisted. Collection exports omit these device settings; importing collections preserves the local choices and cannot enable docking on another computer.
- No new dependency, outgoing request, login storage, update execution, security privilege or security-setting change is part of this feature. Existing claim confirmations, URL policy, HTTPS behavior, update checksum verification and release signing status are retained.

## Findings fixed during validation

1. Shell-restart recovery now removes any surviving own registration before registering again. This also makes a repeated/simulated restart notification safe, rather than leaving an old registration behind if a new registration fails.
2. Fullscreen state resets on suspension, so a hidden sidebar does not retain a stale fullscreen flag when restored.
3. Failure rollback normalizes an invalid in-memory width before saving the disabled state. A fault-injection test originally found that an invalid width could prevent the disabled state being persisted even after the work area was restored; this was fixed and retested.
4. Initial visibility events are gated until the first Opened event, preventing hidden startup from briefly reserving desktop space. Returning to normal mode restores the original content alignment and window state.
5. Native hidden-startup testing exposed an existing tray lifecycle issue: the --tray startup instruction was re-applied on later Show/Opened events. It now hides only once at launch, so restoring an enabled sidebar from tray works. Saved-enabled hidden startup, unchanged work areas while hidden, and reservation after restoration were verified.

## Completed validation

Full evidence is included under audit-evidence/0.7.12/. The final offline suite passed 547 checks; the final real-Windows docking harness passed 62 checks on three displays. Package validation is recorded in the separate release artifact report.

- Locked dependency restore with fresh advisory data for the app/test project; audit errors and NU1900–NU1905 remain build errors. No package references or lock files were changed. This establishes only the advisory results available at the time of the restore.
- Release compile and the existing security/reliability/UI suite, extended with negative-coordinate, DPI geometry, width limits, malformed local settings, opt-in defaults, preference round-trip, export/import isolation, narrow card layout, reachable settings and restoration checks. Headless layouts were rendered and inspected.
- Real Windows integration across three connected 1920×1080 displays: left/right reservations, unchanged other displays, a separate real maximized window respecting the strip, native sidebar bounds, hide/tray restoration, minimize, undock, and close. Original work areas were read independently and restored after the harness ended.
- Repeated position broadcasts, simulated fullscreen start/end, simulated TaskbarCreated, missing selected display and invalid-geometry fault injection. These validate handler paths; they do not establish real Explorer crash or physical hotplug behavior.

## Outstanding tester coverage

Real mixed-DPI displays and DPI changes; physical monitor removal/reconnection and display rotation; actual Explorer crash/restart and abrupt application termination; other third-party AppBars; taskbar auto-hide/relocation; real fullscreen games and multiple topmost apps; remote desktop transitions; long-duration use; installation/update/uninstall and clean-machine testing. These were not simulated by changing the user's hardware, terminating Explorer, or disabling protections.

Fullscreen programs and system UI may overlap a reserved sidebar. Ordinary windows can be manually positioned outside the work area; this feature is not an isolation boundary. Windows remains responsible for shell layout and foreground policy. Builds remain unsigned at the user's request; SmartScreen reputation is unchanged. No broad security pass or public-release readiness is claimed.

## Platform references

- [Microsoft: Application desktop toolbars](https://learn.microsoft.com/en-us/windows/win32/shell/application-desktop-toolbars)
- [Microsoft: SHAppBarMessage](https://learn.microsoft.com/en-us/windows/win32/api/shellapi/nf-shellapi-shappbarmessage)
- [Microsoft: fullscreen AppBar notifications](https://learn.microsoft.com/en-us/windows/win32/shell/abn-fullscreenapp)
- [Avalonia 12.1.5 per-window native callback API](https://github.com/AvaloniaUI/Avalonia/blob/12.1.5/src/Avalonia.Controls/Platform/Win32Properties.cs)
