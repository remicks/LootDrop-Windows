# LootDrop Windows v2 0.7.12 — desktop sidebar test build

## Desktop space reservation

Settings > Desktop sidebar mode can pin LootDrop to the left or right edge of the current or a selected display. Windows reserves that strip through its AppBar API, so maximized desktop windows use the remaining work area. Choose a logical width of 360, 400, 480 or 560 pixels; the width is capped to preserve at least 640 logical pixels and 55% of the display for other apps. A display that cannot fit the sidebar is rejected safely.

Sidebar mode uses a page selector, one column of responsive offer cards, collapsible platform/store filters, and a full-pane details view. The arrow returns to a normal window; the dash hides to tray; the close button follows the existing close-to-tray preference. Help, Settings, Themes and store shortcuts remain accessible. Claim destination confirmation remains mandatory.

Docking starts disabled for existing and new installations. Preferences stay on the local Windows v2 device, are excluded from collection exports, and are preserved by collection imports. Hiding, minimizing, disabling and exiting release the reservation. Restoring an enabled sidebar reacquires it. A missing selected display falls back to the primary display; failed negotiation returns to a normal window and disables docking. The app renegotiates on display/DPI changes and shell restart notifications.

## Safety and limits

This feature adds no package, network endpoint, credential handling, scheduled service, administrator requirement, startup entry or security-setting change. It calls documented Windows shell APIs for its own window and loads system DLLs from System32. It does not overwrite the global desktop work area, inject into other programs, or change display configuration. It leaves Avalonia's native message processing intact and removes its callback on disposal.

An AppBar is a desktop layout feature, not a security boundary. Fullscreen games, secure/system screens and other topmost windows can cover it. LootDrop yields to fullscreen notifications rather than interfering with games. Hiding releases the space rather than leaving an invisible strip. Existing version 2 data remains in its existing folder; beta and iOS files/data were not touched.

Validation passed: 547 offline regression checks and 62 native Windows docking checks across three connected monitors, with original work areas restored. A tray startup/restore bug found by these checks was also fixed.

See [the sidebar security and stability review](SECURITY-SIDEBAR-0.7.12.md) for validation and outstanding coverage. This unsigned test build is not a security certification or a claim that installation, every graphics driver, every monitor combination, or real hardware unplug/replug behavior has been validated.

## Reproducing validation

```powershell
dotnet restore .\SecurityChecks\SecurityChecks.csproj -p:Configuration=Release --locked-mode --force --no-http-cache
dotnet run --project .\SecurityChecks -c Release --no-restore -- --theme-screenshots
# Opt-in Windows integration checks briefly reserve space for isolated test windows.
# They use synthetic libraries, never fetch feeds, and close in finally with work-area verification.
dotnet run --project .\SecurityChecks -c Release --no-build -- --native-sidebar
.\Build-Tester-Releases.ps1 -Target All
```
