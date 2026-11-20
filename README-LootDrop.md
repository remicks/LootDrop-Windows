# LootDrop version 2 — offer details and Help (0.7.10)

New: compact hover/click status icons, a full offer-details drawer, a cleaner dashboard and searchable Help (sidebar or F1). See [current release validation](RELEASE-0.7.10.md) and [plain-language help](HELP.md). Offline caching, backups, collision-safe identities, 18 themes and the smaller 0.7.5 package configuration are retained.

New: per-source offline cache/health, reviewed library export/import/restore, daily local snapshots, and native-provider-ID duplicate handling. See [release notes and validation](RELEASE-0.7.5.md). The Windows release build script requires PowerShell 7.2+ (`pwsh`). All 18 theme presets remain available.

This complete source project continues the user's confirmed version 2 b15 baseline. It is not LootDrop-v3beta. Read SECURITY-AUDIT.md for changes, actual validation results, limitations and the manual tests required before outside distribution.

## November 18 update

Adds 10 coordinated dark, light, pastel and neon combinations while retaining the eight existing presets. See THEMES.md and SECURITY-RECHECK-2026-11-18.md for current changes and actual validation.

## Features retained

- GamerPower, Epic Games and CheapShark giveaway feeds; visible errors and 20-minute refresh.
- Wishlist, claimed backlog, local not-free flags, search, platform/store filters and sorting.
- Conservative Steam/Epic US price evidence; saved/offline records cannot be claimed as live listings.
- Themes, custom colors, managed wallpaper imports, window appearance and low graphics/software rendering options.
- Tray controls and explicitly opted-in per-user Windows startup.
- Per-user installer and self-contained portable Windows x64 builds.

## Security behavior changes

Only structurally valid HTTPS claim links can be opened, after a destination preview. LootDrop does not crawl claim pages, log in, claim games, fill checkout forms or store account credentials. Price evidence never guarantees checkout eligibility or a final price. Automatic artwork is limited to reviewed provider/CDN domains; unsupported images show no cover. Responses, images and local library reads have size limits. An unreadable library is preserved rather than replaced by autosave. Re-select a wallpaper if an older path is outside the managed Backgrounds folder. Startup registration is read at launch and changed only from Settings.

## Build and check

Install a .NET 10 SDK from Microsoft. From the extracted project folder:

```powershell
dotnet restore .\SecurityChecks\SecurityChecks.csproj -p:Configuration=Release --locked-mode
dotnet run --project .\SecurityChecks\SecurityChecks.csproj -c Release --no-restore
```

These are isolated regression checks; they do not launch the app or use your existing library. For an intentional local GUI test, first back up `%LOCALAPPDATA%\LootDrop`, then use `dotnet run -c Release`. This uses version 2 data. Do not launch it against valuable data until you have reviewed the audit and backups.

Build fresh packages with:

```powershell
.\Build-Tester-Releases.ps1 -Target Portable
# Both outputs require a separately installed Inno Setup compiler:
.\Build-Tester-Releases.ps1 -Target All
```

See BUILD-FOR-TESTERS.md. No bypass of Windows execution policy or security warnings is required or recommended here. Use your normal trusted script policy. There is no SkipPublish option.

## Data and identity

Version 2 intentionally uses `%LOCALAPPDATA%\LootDrop\library.json` and its Backgrounds directory. Installed and portable version 2 share that directory. Startup uses HKCU Run value `LootDrop`; installer AppId and installation directory remain version 2's original identity. No beta path or data is used. The source ZIP contains no personal settings or game library.

Local JSON is not encrypted and is not a credential store. This package does not certify external stores, API data or native dependencies as safe. Follow the audit's manual validation checklist before sharing executables.




Subtle company branding: see RELEASE-0.7.10.md and Assets/Branding/PROVENANCE.md.

