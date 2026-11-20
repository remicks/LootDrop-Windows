# LootDrop version 2 for Windows — 0.7.5

Reliability, recovery and package-size update. November 18, 2026.

## Offline cache and source health

The last successful response from GamerPower, Epic Games and CheapShark is retained separately under `%LOCALAPPDATA%\LootDrop\FeedCache`. Discovery can display usable cached listings when a provider fails. Each source shows its state and last successful retrieval time. Cached cards explicitly say OFFLINE CACHE / REFRESH REQUIRED; their claim buttons are disabled and they never inherit a previous “verified free” result. Expired cached listings remain accessible through All Games rather than normal Discovery.

A successful empty response clears that provider's old cached offers. A failure preserves its last successful cache while other sources continue to display fresh results. Cache reads/writes are bounded, schema/source validated and atomic. Invalid caches are ignored with a visible source-health message. Cache contents never supply authoritative wishlist/claimed flags. Artwork downloads are limited to current live results.

Refreshed live listings retain the existing conservative price-check behavior and destination preview; a fresh API listing is not a checkout guarantee. Cached or saved-only offers cannot open a claim page until a fresh live feed returns them. A refresh also invalidates stale objects held by an already-open confirmation dialog.

## Backups, export, import and recovery

Open Settings → Backups & Portable Library.

- Before the first change to an existing library each UTC day, a validated local snapshot is created under `%LOCALAPPDATA%\LootDrop\Backups`.
- The 20 newest managed daily/recovery snapshots are retained. Backup writes are completed through unique temporary files before being published. An unreadable existing daily snapshot stops saving instead of silently counting as a valid backup; retain a separate copy and repair/remove that damaged snapshot before retrying.
- Export writes a versioned JSON file containing saved offers and named custom themes. It does not export the feed cache, startup/device preferences or wallpaper paths.
- Import validates the entire supported export before showing counts, sample titles and replacement behavior. Cancel makes no replacement. Approval replaces the collection and named themes, preserving valid current device preferences and stripping imported wallpaper paths. No imported link opens automatically.
- Restore Backup lets you choose a managed snapshot and review it before replacing collection data. Existing library bytes are saved as a recovery snapshot first, including corrupt originals when explicitly recovering an unreadable library.
- A library change between preview and approval invalidates the preview. Invalid/oversized files, unsupported formats, duplicate offer identities and backup-path traversal are rejected. Raw beta/unknown JSON does not qualify as a version 2 portable export.

Restoration is deliberately about collection data and named themes. It does not register startup or restore renderer/window/device settings from the incoming file. If the current library itself is unreadable, its device preferences cannot be recovered and defaults are used. Absolute background paths are never activated from an import. Wallpaper image files are not included; select them again on the receiving machine if desired.

Snapshots/exports are ordinary local JSON, not encrypted archives or credential stores. They may contain game titles and claim URLs; review before sharing. Automatic snapshot retention is not a substitute for an off-device backup. No personal library was imported or edited during this development task.

## Duplicate handling

Saved state uses provider plus native offer ID where available, rather than a bare numeric hash. GamerPower retains its native numeric IDs. Epic and CheapShark persist their opaque provider IDs, preventing accidental collisions from the older hashed integer IDs. Unambiguous older entries migrate when their live provider result returns; an ambiguous legacy hash never has its personal flags assigned to a guessed native route.

Presentation groups matching normalized full titles in expandable groups. Every provider/storefront route remains a separate card, with its own URL, flags, price evidence, expiry and claiming state. Edition/DLC suffixes are not stripped. Grouping is a convenience, not proof that two differently sourced titles describe identical products. Existing library flags cannot be recovered if an earlier version had already overwritten a colliding record; the new identity scheme prevents that future cross-provider loss.

The 18 theme presets and saved custom base palettes remain available.

## Package-size work

Two self-contained, untrimmed Windows x64 publishes were compared: internal single-file compression enabled versus disabled. Standard ZIP Optimal/SmallestSize and the same Inno Setup LZMA2 configuration were measured. Trial application-assembly SHA-256 values matched between both publishes, and ZIP round-trips matched the complete published executable. No runtime, font, theme, image decoder or security feature was removed.

The chosen configuration disables compression *inside* the executable and uses standard ZIP SmallestSize compression around the portable output. Inno Setup can compress the raw payload much more effectively than an already compressed executable. This avoids adding a packer or changing application code/runtime behavior. The extracted executable is larger; download size and installed size are different measurements.

Unused PNG icon previews and the unused Avalonia template logo are no longer embedded in the EXE. They remain in the source project. All eight runtime ICOs remain embedded and are individually decoded in the regression checks; theme icons and wallpaper PNG import continue to work. No fonts or runtime libraries were excluded.

The build script now requires **PowerShell 7.2+** for .NET's standard SmallestSize ZIP support. This is a maintainer build prerequisite only; testers still receive a self-contained application and do not need PowerShell or .NET installed separately. Use `pwsh -File .\Build-Tester-Releases.ps1 -Target All`. No tool is downloaded or installed automatically.

Detailed measured bytes and assembly hashes are in `audit-evidence/0.7.5/size-comparison.json`. Final measurements are recorded below after packaging. Aggressive trimming, AOT conversion, external executable packers, removal of runtime/security components and signature/antivirus bypasses were not used. Native launch performance was not measured.

## Validation and remaining limits

The regression suite covers cache failure/recovery/empty-response behavior, stale claim blocking, per-provider flags, native-ID collision prevention, conservative legacy migration, backup retention and corruption, preview cancellation/no-write behavior, changed-target rejection, import preference isolation, recovery from corrupt JSON, all theme controls and existing transport/image protections. Headless UI tests use synthetic libraries and fake feed providers; they neither contact claim pages nor open a native app window. The current logs and release metadata are under `audit-evidence/0.7.5/`.

NuGet advisory checks are point-in-time dependency checks, not a security certificate. Public HTTPS destinations and redirect chains remain untrusted, native codecs remain a dependency risk, and same-user malicious processes are outside the data-file checks' isolation boundary. Concurrent multi-process writers are not transactionally locked; preview hashes detect a changed target before applying, but cannot protect against every same-user race.

App and installer remain unsigned. Native installation/update/uninstallation, actual browser handoff, real-network throttling, startup registration, hardware rendering and clean-machine operation still require manual testing before outside distribution. No automatic claiming, store login, cloud upload, installation or publisher signing was performed. Beta and iOS source/data remain separate and unchanged.

## References

- [Microsoft single-file deployment and compression guidance](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview)
- [Microsoft standard ZIP compression levels](https://learn.microsoft.com/dotnet/api/system.io.compression.compressionlevel)
- [Original security audit](SECURITY-AUDIT.md) and [November 18 recheck](SECURITY-RECHECK-2026-11-18.md)

## Final results

- Completed 286 security regression checks. No app launched, real links fetched, or startup registry changed.
- Updated application and installer version: 0.7.5.0. Release publish and Inno compilation completed successfully. No native app/installer was executed.
- Live NuGet vulnerability listing: no known vulnerable direct/transitive Release packages reported. Debug and Release audited restores completed without advisory warnings.
- Actual XAML was rendered headlessly for all 18 presets. Light/neon Settings and synthetic collection screenshots were inspected; input readability and selected/hover text contrast were also checked automatically.
- Fresh portable ZIP read/hash validation succeeded. Both app and installer Authenticode status: NotSigned.
- Simulated audit/restore failure stopped before publishing. Build script parsed without errors.
- iOS fork hashes unchanged. No beta or personal-library changes were made.
- Native GUI, real browser handoff, install/update/uninstall, startup and clean-machine validation remain outstanding. Outside distribution is not cleared by this report.

## Final package measurements

Comparison uses the same 0.7.5 feature set with the former packaging and embedded preview-asset configuration as the baseline.

| Artifact | Former strategy, bytes | Final package, bytes | Reduction |
| --- | ---: | ---: | ---: |
| Portable ZIP | 46295388 | 45200953 | 2.36% |
| Installer | 45903867 | 35278543 | 23.15% |
| Extracted executable | 52016410 | 105230414 | Deliberately larger on disk |

Final downloads: portable ZIP 43.11 MiB; installer 33.64 MiB. The extracted EXE is 100.36 MiB. No runtime/security dependencies or fonts were removed. Final-asset compression trials matched the application assembly SHA-256 across internal-compression on/off variants. No native startup-time improvement is claimed.
