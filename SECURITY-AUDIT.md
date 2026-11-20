Current expanded review: [Windows v2 0.7.8](SECURITY-REVIEW-0.7.8.md). Historical findings/results follow.

# LootDrop version 2 b15 — security hardening review

**Latest update:** [0.7.6 offer details, interface and Help validation](RELEASE-0.7.6.md). Historical results below are not certification of a current release.

**Current release:** [0.7.5 reliability, recovery and package-size validation](RELEASE-0.7.5.md). Historical findings and limits below remain relevant; they are not release certification.

**Follow-up:** see [November 18 security recheck](SECURITY-RECHECK-2026-11-18.md) for the current 0.7.4.2 security/theme changes. Results below describe the original November 3 review.

Review date: November 3, 2026 (America/Chicago). Updated source version: 0.7.4.1.

## Status

Source review and automated validation are documented below. This is not a security certification or approval for outside testers. Windows GUI operation, installer execution/uninstall, and clean-machine behavior require manual validation before distribution.

## Baseline and isolation

The referenced ChatGPT chat exposed only recent turns and a beta attachment link; it did not expose a version 2 ZIP or older attachment history. The user explicitly selected the local `LootDrop-v2-b15` project as the complete version 2 baseline. A separate copy was made, excluding generated `bin`, `obj`, and `dist` files. Original projects and user data were not modified. No beta project or beta data was opened or changed. This is version 2, not a v3beta fork. Version 2 retains its existing application identity, installer AppId, `%LOCALAPPDATA%\LootDrop` data directory, and HKCU Run value `LootDrop`.

The delivered ZIP is a complete source project, not an installer or portable executable. Assets, project configuration, build scripts, regression harness, dependency locks and audit evidence are included. Generated binaries and personal library data are excluded.

## Reviewed surface

All application C#, XAML, project/manifest configuration, PowerShell build scripts, Inno Setup definition and documentation were inspected. Main boundaries: third-party JSON and artwork, feed-supplied claim links, store price verification, local JSON/themes/backgrounds, opt-in startup registration and release packaging. Dependency checks include transitive packages in the Release graph. NuGet advisories do not cover every native library, embedded code, OS component, SDK/compiler, or unknown vulnerability.

## Findings and implemented changes

| Priority | Finding | Change and remaining limits |
| --- | --- | --- |
| High | Feed-supplied claim URLs were automatically probed with HEAD/GET and automatic redirects. A provider or poisoned feed could induce unintended requests. HTTP links were accepted. | Removed automatic claim-page requests entirely. Structural HTTPS validation blocks custom schemes, userinfo, literal IPs, single-label/local-style hosts, control characters, backslashes, oversized URLs and non-443 ports. Claims require a destination preview showing the ASCII host and full URL, followed by a deliberate Open website action. No login, checkout, account linking or claim automation was added. Browser destinations and downstream redirects remain untrusted. |
| High | Arbitrary remote cover hosts and unbounded image downloads/decodes were accepted. API responses could also grow without a practical application limit. | Automatic cover requests are restricted to explicit provider/CDN domain boundaries. Shared HTTP transport disables redirects, cookies, default credentials and automatic decompression. API bodies are capped at 8 MiB, image bodies at 4 MiB, including streams without Content-Length. A linked 20-second deadline covers response headers and body. Image dimensions are checked with the existing Skia decoder before Avalonia bitmap decode: at most 8192 per axis and 16 million pixels. Covers decode to a smaller width; previous feed bitmaps are disposed on refresh. At most 500 entries per provider are mapped. Missing artwork does not block claiming. |
| High | Steam verification extracted an app ID using a regex across the entire URL; Epic verification could be selected by a platform label and match a title from an unrelated destination. | Verification now requires a direct, structurally valid HTTPS store host. Steam IDs come only from an anchored `/app/<id>` path on `store.steampowered.com`, with bounded numeric length. A matching Epic title can produce a verified result only for the directly fetched Epic provider listing, not a GamerPower title match. Price evidence is regional and time-sensitive; it is not a checkout guarantee. |
| Medium | Corrupt/unreadable local JSON could be followed by autosaving an empty or partial library. Fixed `.tmp` files could collide or be linked. | Bounded JSON reads, basic null/collection validation, unique CreateNew temporary files, flush-before-replace and link checks for the data directory/file. A failed load disables subsequent saves in that service instance, preserving the original file. No settings migration or personal-data rewrite was performed during this review. |
| Medium | A stored wallpaper path could point to an arbitrary file or network share; image import copied without size limits. | Stored backgrounds must remain inside the existing version 2 Backgrounds directory. UNC/outside paths are rejected before file access; managed directory/file links are rejected. Imports are bounded and decoded before a uniquely named file is written. Older backgrounds outside the managed folder must be selected again. |
| Medium | Extreme advertised values could overflow statistics; oversized titles could make regex-based grouping expensive. | Bounded provider title/description/platform fields and advertised values; excessive saved values/titles are rejected without autosaving over the original library. This deliberately rejects malformed outliers rather than treating them as genuine prices. |
| Medium | Merely loading a `StartWithWindows` JSON setting could write to the startup registry. | Launch reads the actual registration without writing it. Startup registry changes occur only when the user changes that setting. Executable paths remain quoted; startup and installation remain per-user, without elevation. Moving an app requires re-enabling startup from the new location. |
| High | Release builds lacked a fail-closed dependency gate, accepted stale publish output and recursively repackaged old output folders. Debug diagnostic tooling remained in Release's dependency resolution. | Explicit NuGet source/audit source; separate configuration-specific dependency locks; audit warnings/unavailable audit data fail restore. Release script performs fresh locked restore with transitive auditing before publish. Removed SkipPublish. Each build has a new output directory, and direct linked build directories are refused. No recursive cleanup or reuse of old publish output. Installer requires the fresh publish directory supplied by the script. Debug diagnostic package is excluded from Release. |
| Medium | Release identity and documentation used ambiguous “beta” wording. | Retained version 2 identity, changed release display/version to 0.7.4.1, explicitly requests asInvoker privileges, and updated build instructions. No beta identity/data path was adopted. |

## Credentials, privacy and local data

No application authentication flow, embedded API key/password, credential store, telemetry upload, auto-updater, remote command execution or executable download was found in the reviewed source. This is a source inspection finding, not proof that every dependency has no such behavior. Data consists of saved game records, filters, themes, wallpaper paths and preferences in ordinary local JSON, plus copied backgrounds. It is not encrypted; the current Windows account and administrators can access it. Do not put passwords or tokens into these fields. Browsers handle store authentication separately. Logs may contain exception details/URLs; review before sharing them.

Per-user directories rely on Windows account permissions. A malicious process already running as the same user can replace code/data or race file checks; these checks do not provide isolation against a compromised account. Back up the version 2 library before first manual testing. Portable and installed version 2 intentionally share version 2 data, as before.

## Validation

See `audit-evidence` for final command output and package inventory. Final results are recorded after running the checks; individual unavailable checks are explicitly noted. The harness uses synthetic HTTP responses and a separate fixture directory under its own build output. It does not launch the GUI, open claim pages, register startup, install software, or use the real LootDrop library. Optional `--live-feeds` makes one read-only request to each fixed feed API, and does not crawl any offer URLs.

| Actual check | Result |
| --- | --- |
| Compiler/runtime environment | .NET SDK 10.0.403; installed .NET runtime 10.0.14; Inno Setup 7 compiler. Initial `dotnet --info` could not query Windows services in the restricted context; this did not prevent subsequent builds. |
| Release and Debug dependency restores | Completed against NuGet.org with all-package auditing, without advisory warnings. Final Release restore used locked mode. Separate Debug and Release lock files are included. |
| Live direct/transitive NuGet vulnerability listing | Completed successfully; no known vulnerable packages reported in the restored Release graph. This is a point-in-time advisory result, not an “audit passed” certification. See `nuget-vulnerabilities.json`, `packages.json` and lock files. |
| Security regression harness | 48 checks passed, covering unsafe URLs, host spoofing, streamed/declared oversized bodies, redirects, body cancellation, verification spoofing, malformed/oversized images, UNC/outside wallpaper paths, library round-trip, null/corrupt/oversized JSON, overflow inputs and autosave preservation. See `security-regressions.txt`. |
| Fixed live feed smoke checks | GamerPower parsed 30 offers, Epic Games 1, CheapShark 14 at check time. Offers and counts change; this only validates retrieval/parsing. See `live-feeds.txt`. |
| PowerShell parser | Both delivered build scripts parsed with no errors. |
| Release gate negative checks | A simulated restore failure stopped publishing; removed SkipPublish parameter was rejected. Actual unavailable audit-data errors NU1900/NU1905 also stopped the release before publishing. Network access was then granted and the live audited build succeeded. |
| Windows Release publish and installer compilation | Both completed successfully with final application source. No generated app or installer was launched or installed. Full output in `release-build.txt`. |
| Portable ZIP validation | Every entry read successfully; its single bundled LootDrop.exe matches the published executable SHA-256. No PDBs included. |
| Final package sizes | Portable ZIP: 46,274,428 bytes (44.1 MiB). Installer: 45,891,466 bytes (43.8 MiB). Native developer PDBs totaling 104,951,808 bytes are omitted from fresh release packaging. These test artifacts are not included in the delivered source ZIP. |
| Signing | Actual Authenticode inspection: app and installer both NotSigned. See `release-artifacts.json`. |
| Original source preservation | Original b15 source file hashes rechecked against the initial inventory and unchanged. Application/beta user data were not loaded by the harness. |
| Interactive GUI, runtime bundle launch, install/update/uninstall, startup and clean-machine tests | Not performed. These remain required before outside distribution. |

## Remaining risks and required manual validation

- Do not distribute solely because compilation, regression tests or NuGet Audit succeed. This review is not a penetration test, malware analysis or complete supply-chain attestation.
- Claim URLs can still be phishing pages on public HTTPS domains, and browsers may follow redirects. The preview helps users decide; it does not certify the site. Host filtering is syntactic and does not pin DNS or categorically prevent a hostname from resolving to a private address. Automatic requests trust the enumerated provider/CDN domains and system DNS/TLS/proxy configuration. No certificate-validation bypass was introduced.
- Image parsing still uses native Skia code, including header parsing. Byte/dimension limits reduce resource exposure but cannot eliminate codec vulnerabilities or all CPU/memory exhaustion. Unlisted CDNs and redirects intentionally show no cover until explicitly reviewed.
- Fixed APIs can change schema, rate-limit requests, serve stale/incorrect data or change regional eligibility. Steam/Epic evidence is not assurance about payment, account eligibility, ownership or final checkout price. No real checkout or claim was attempted.
- Release binaries/installer are unsigned unless separately signed by the maintainer. Hashes detect accidental differences; they do not establish publisher identity. Obtain .NET and Inno Setup from official sources; this script downloads or installs neither. Never disable antivirus, SmartScreen or signature checks to test these builds.
- Before outside testing: use a disposable Windows account/VM with no saved store credentials. Test normal launch, tray/exit and software rendering; confirm the claim preview's Cancel and Open actions; verify filters/wishlist/claimed state persistence; reject malformed/oversized data without losing the original; exercise allowed/missing artwork and wallpaper import/removal; test per-user install, update, uninstall and manually opted-in startup. Confirm installer/portable operation on a machine with no SDK/runtime preinstalled and confirm v3beta data is unchanged. No such interactive tests have been claimed here.
- Audit the fresh dependency/runtime graph again at every release. Advisory databases and runtime security status change over time. Check runtime/SDK and native-library advisories separately before wider release.

## Maintainer commands

Use a .NET 10 SDK in the extracted project folder:

```powershell
dotnet restore .\SecurityChecks\SecurityChecks.csproj -p:Configuration=Release --locked-mode
dotnet run --project .\SecurityChecks\SecurityChecks.csproj -c Release --no-restore
.\Build-Tester-Releases.ps1 -Target All
```

The build script creates a fresh `dist\v2-<timestamp>-<random>` folder. It does not install or launch the generated files. There is no SkipPublish option. A failed audit must be investigated, not bypassed. Intentional package updates require reviewing dependency changes, regenerating the corresponding lock file with `dotnet restore -p:Configuration=Release --force-evaluate`, rerunning the checks and reviewing the audit report.

## Primary references

- NuGet Audit and warning meanings: https://learn.microsoft.com/en-us/nuget/concepts/auditing-packages
- Locked restore/content hashes: https://learn.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files
- Avalonia scaled image decode: https://reference.avaloniaui.net/api/Avalonia.Media.Imaging/Bitmap/F815A524

These references explain mechanisms, not the safety of this application. Actual project validation is recorded separately.

