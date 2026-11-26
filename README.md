# LootDrop for Windows

A Windows desktop app by **Degenerates Inc.** for discovering free-game offers and tracking a local wishlist and claimed collection.

Current version: **0.7.15 — release packaging fix tester prerelease**. Download published builds from [Releases](https://github.com/remicks/LootDrop-Windows/releases). See [release notes](RELEASE-0.7.15.md), [Help](HELP.md), and the [theme/font security review](SECURITY-THEMES-FONTS-0.7.14.md).

## Features

- GamerPower, Epic Games and CheapShark feeds, with offline cache and source health.
- Compact grouped offers, destination previews, price/freshness details and local tracking.
- Official store sign-in shortcuts that open your browser; LootDrop never collects store passwords or browser sessions and does not claim games automatically.
- Manual GitHub update checks and checksum-verified installer/portable downloads, with no automatic installation.
- Optional desktop sidebar with Windows-reserved screen space, display/edge selection and a single-column layout.
- Eight embedded Windows icon colors, independent icon selection and explicit themed desktop shortcuts.
- Nine embedded interface fonts, categorized Settings and rounded controls.
- Four embedded interface fonts, categorized Settings and rounded controls.
- 24 themes, searchable Help, library export/import and recovery backups.
- Self-contained Windows x64 installer and portable packages.

## Safety and testing

The regression suite completed 723 checks for this version. 62 native Windows docking checks passed across three displays; remaining hardware coverage is documented in the UI review. Packages are unsigned. Native installation/update/uninstall, real browser sign-in and clean-machine validation remain outstanding; passing tests is not a security certification. Do not disable antivirus or SmartScreen. Start testing in a disposable Windows account/VM without valuable store credentials. Windows v2 stores data under `%LOCALAPPDATA%\LootDrop`; portable and installed v2 share that location.

## Build

Use the .NET 10 SDK. From PowerShell 7.2+:

```powershell
dotnet restore .\SecurityChecks\SecurityChecks.csproj -p:Configuration=Release --locked-mode
dotnet run --project .\SecurityChecks\SecurityChecks.csproj -c Release --no-restore
.\Build-Tester-Releases.ps1 -Target All
```

Installer builds additionally require Inno Setup from its official source. The release script refreshes dependency advisories, fails on audit/restore errors and uses a fresh output directory. It does not install or launch the generated app.

[Detailed project notes](README-LootDrop.md) · [Build instructions](BUILD-FOR-TESTERS.md) · [Artwork provenance](Assets/Branding/PROVENANCE.md)

This repository contains Windows version 2 only. The beta and iOS fork are maintained separately. Company artwork remains identified in its provenance document; no license grant for it is implied by publishing the repository.





Release notifications: [Discord announcements](DISCORD-RELEASES.md) wait for uploaded assets and disable all mentions.
