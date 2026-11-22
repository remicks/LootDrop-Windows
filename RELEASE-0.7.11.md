# LootDrop Windows v2 0.7.11 — eight embedded app icons

## What changed

Themes now has an App icons selector: Midnight Purple, Electric Blue, Neon Cyan, Emerald Green, Crimson Red, Sunset Orange, Hot Pink and Royal Gold, plus Match app theme. The eight existing coordinated LootDrop artwork variants are reused; no new generated artwork or dependency was introduced. An independent icon preference is saved in the Windows v2 local settings. Existing libraries default to theme matching. Collection export does not transfer the device preference, and import preserves the destination PC's setting.

Choosing a color updates the window and system-tray icons. Existing desktop/pinned shortcuts keep their own icons. The installed/portable build offers Create separate themed desktop shortcut, which creates a color-named .lnk on the user's desktop without overwriting an existing shortcut. It targets the current LootDrop.exe, has no arguments, and selects the matching native icon index. Moving a portable EXE can leave an old shortcut pointing at the old path. Taskbar pinning remains under Windows/user control; no automatic pin modification, registry change, startup action or desktop rewrite occurs. The EXE's default Explorer icon stays Purple.

## Embedded Windows resources

Previously the eight ICO files were managed Avalonia resources, so only one color was exposed to Windows' native Change Icon picker. All eight are now also native RT_GROUP_ICON resources inside the EXE, at stable IDs 32512–32519 (indices 0–7 in the order above). Each retains 16, 24, 32, 48, 64, 128 and 256-pixel frames. Shortcut Properties > Change Icon can browse directly to LootDrop.exe; no loose ICO files are needed.

The build uses a checked MSBuild task, compiled by the installed SDK without fetching another tool/package. It replaces icon resources in the intermediate managed assembly under obj after C# compilation and before apphost creation/single-file bundling. It never rewrites a finished single-file EXE or an installed application. Before/after checks preserve the manifest and version resource byte-for-byte; native icon groups and every frame are verified. The native API maps files as resource data, not executable code. Unexpected input/resource layout stops the build. The managed ICOs remain available for Avalonia window/tray use; native resources add some package size but no runtime trimming or unsafe executable packer is used.

## Safety and validation

523 regression assertions passed with the existing full suite. New checks cover the fixed catalog, preference persistence/theme independence, actual selector and tray/window assignment, restart restoration, Windows ExtractIconEx enumeration, all 56 native frame payloads, retained asInvoker manifest, and real ShellLink persistence of each resource index. Shortcut tests use an inert file in an isolated fixture folder, never the real desktop or an executed EXE. Existing shortcuts are hash-compared after refused replacement, and forged names/indices are rejected.

The updated XAML was rendered headlessly and inspected. Build output and full check logs are under audit-evidence/0.7.11/. Native icon inspection loads the apphost as resource data only. Published single-file and portable archive checks are recorded during final packaging.

The public GitHub update source, unsigned prerelease policy and versioned asset naming are retained. No signing subscription, protection setting, real library, beta/iOS project/data or original artwork was changed. These checks do not certify security or replace manual testing of Explorer's Change Icon dialog, physical taskbar/tray behaviour, real desktop interaction, clean-machine launch or installation/update/uninstall. The app and installer remain unsigned; keep Windows security enabled.

## References

- Windows resources: https://learn.microsoft.com/en-us/windows/win32/menurc/resource-file-formats
- .NET apphost resource copying: https://github.com/dotnet/sdk/blob/main/src/Tasks/Microsoft.NET.Build.Tasks/CreateAppHost.cs
- .NET host/bundler implementation: https://github.com/dotnet/runtime/blob/main/src/installer/managed/Microsoft.NET.HostModel/README.md

Finished-bundle validation: ExtractIconEx found exactly eight native icons in the published single-file EXE; all 56 frame payloads match the embedded sources, and its asInvoker manifest remains present. Resource-only inspection did not execute the app. Package sizes: LootDrop-Portable-win-x64.zip: 48153944 bytes (45.92 MiB). LootDrop-Setup.exe: 36521770 bytes (34.83 MiB).

Final packaging validation: version 0.7.11.0; fresh locked audited restore, Release publish and Inno compilation succeeded. Portable archive contains one executable matching the fresh published executable by SHA256. App signature: NotSigned; installer signature: NotSigned. Neither was installed or launched.
