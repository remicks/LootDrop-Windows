# LootDrop Windows v2 0.7.15 — release packaging fix

This tester prerelease changes how release packages are named and versioned. App features, themes, fonts, data and settings are unchanged from 0.7.14.

## Discord summary

- Release packages are now built with the exact versioned names the in-app updater accepts.
- The installer version now comes from LootDrop.csproj instead of a separately hardcoded value.
- No app feature, data or settings changes; 0.7.14 themes and fonts are retained.

## Release packaging

- The build script produced `LootDrop-Setup.exe` and `LootDrop-Portable-win-x64.zip`, while the in-app updater and Discord announcement only accept `LootDrop-v2-<version>-Setup.exe` and `LootDrop-v2-<version>-Portable-win-x64.zip`. Each release needed a manual rename, and its SHA256SUMS listed the pre-rename names. The script now writes the final names and a versioned `LootDrop-v2-<version>-SHA256SUMS.txt`.
- `LootDrop-Installer.iss` no longer hardcodes the version. Build-Tester-Releases.ps1 reads `LootDrop.csproj` and passes it to Inno Setup, and the installer definition refuses to compile without it. The matching `RELEASE-<version>.md` is required and copied automatically.

## Validation and status

723 offline security/reliability/UI checks and all 27 Discord announcement tests passed. A separate 62-check native Windows docking run passed on three monitors and restored every original work area. Locked restores refreshed dependency advisory data with no package or lock changes. Package names were checked against the updater's accepted names, source/portable archives were verified file by file using SHA-256, and the native icon groups and asInvoker manifest were verified. Inno Setup 7 compiled the installer; installation was not performed. Evidence is under audit-evidence/0.7.15/. This remains an unsigned tester prerelease. Clean install/update/uninstall, screen-reader checks, physical hotplug/mixed-DPI changes and extended use still need coverage. Passing tests are not a security certification. Every published build is announced in Discord after uploads finish, with all pings disabled.
