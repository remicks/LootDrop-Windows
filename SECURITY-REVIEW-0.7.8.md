# Security review — LootDrop version 2 Windows 0.7.8

Review date: November 19, 2026 (America/Chicago). This is an expanded source and regression review of the current Windows project plus a point-in-time dependency advisory check. It is not a security certification, independent penetration test or assurance that every dependency is benign. Historical reports are retained with their original results; this report describes this release.

## Scope and threat model

Reviewed application entry point/lifetime, C# models and presentation, XAML event bindings, all services, browser handoffs, feed/image requests, JSON persistence/import/export/cache/backup, image decoding/wallpaper, provider identities, price checks, tray/startup, NuGet configuration/locks, project flags, Windows manifest and build/installer scripts. The relevant boundary call sites are inventoried in audit-evidence/0.7.8/boundary-inventory.txt; that inventory is a search aid, not an automated proof of safety.

Assume remote feeds and imported JSON can be malformed or malicious, links can be phishing sites, external images can stress decoders, cached prices can be stale, local data can be damaged and audit services can fail. Assume a normally configured Windows account, trusted OS/browser/DNS/TLS and trusted installed build tools. A compromised account, administrator, malicious browser extension or tampered build tool is outside what application checks can contain. Neither the user's account library nor browser profiles/credentials were inspected. Tests create synthetic libraries under test output. Beta/iOS projects are outside this review and unchanged.

## New findings and remediation

| Severity | Finding | Action and evidence |
| --- | --- | --- |
| Medium — user deception | A claim that credentials are never sent over the internet would misdescribe browser authentication and optional password sync. | The sign-in page and searchable Help state what LootDrop does not collect, explain transmission to the store/identity provider, browser-managed sessions and optional sync. Actual UI wording is tested and rendered. No credential-storage feature was introduced. |
| Low — misleading links | Literal invisible Unicode formatting characters could obscure displayed URLs. | UrlSafety now rejects Unicode Format characters in addition to control characters/backslashes. Four targeted cases and 2,000 deterministic malformed inputs exercised the boundary. This is not universal phishing detection; ordinary lookalike public domains remain possible. |
| Low — local data availability | Stored filter arrays could be null or contain excessive/invalid entries without shared validation. | Validate arrays/names, item nulls, item lengths and list counts on library load/save and transfer/cache validation. Five targeted malformed cases reject before use. Malformed existing libraries follow the existing fail-closed preservation path. No data migration was run. |
| Low — defensive state handling | Expired/ReportedNotFree verification enums were not explicitly in the rejection property, relying on separate fields. | Both now block claim opening even without the corresponding date/manual flag. Tested with otherwise supported HTTPS offers. |
| Low — defensive cache handling | Price verifier relied on cache callers also clearing the live-feed flag. | Cached records now return unverified before any store request regardless of that flag. Existing offline restrictions and tests remain. |

No critical/high exploit was demonstrated in this review. This statement is not evidence that none exists. No unreviewed automatic login, claim, arbitrary process command, executable downloader, cloud upload or updater was added.

## Authentication and external links

The new shortcut catalog is read-only, has five fixed identifiers and returns only fixed HTTPS URLs. Exact identifier comparison rejects nulls, URL-shaped identifiers, query/path injection, case changes, newlines and unknown values. ProcessStartInfo uses the canonical URL with shell execution and no arguments or credentials. The UI exposes each address before a deliberate click. A real click with a tampered unknown tag was tested and ignored; valid targets were inspected without launching a browser. The page does not assert a successful login after browser handoff.

Existing claim opening requires supported HTTPS syntax and a destination preview with ASCII host and full URL, then rechecks claim eligibility and membership after the dialog. URLs are immutable offer initialization fields. Cached, saved-only, expired, blocked and rejected offers cannot open. Tracking marks are local and not inferred from website opening. Provider credit links are fixed HTTPS addresses. Public HTTPS is not a guarantee of trust, reachability or a zero checkout price. Browsers can follow downstream redirects and invoke store authentication providers; LootDrop does not control or audit those websites.

There are no store credential/token fields in the application/library models, no browser-profile reader, no embedded web login surface, no OAuth callback listener and no CookieContainer-based session import in reviewed application code. This is a source finding, not a forensic statement about an installed machine or all dependency internals. Passwords typed into the store page are transmitted by the browser to authenticate. Browser/store session policies, extension access, password saving and sync remain outside LootDrop's control.

## Network and third-party data

Automatic API requests use fixed GamerPower, Epic and CheapShark endpoints; Steam app IDs come only from a bounded numeric path on the exact direct Steam host. Feed URLs are not probed or crawled. Store checks use US catalog evidence and fail to unverified when unavailable. Epic verification is restricted to the Epic source and direct Epic store host; normalized title matching and cached catalog times are not proof of exact product ownership or checkout eligibility. CheapShark redirects remain user-opened and unverified.

SafeHttp uses normal system TLS validation without a certificate bypass. Redirects, cookies, default credentials and automatic decompression are disabled. Headers and streaming bodies share a 20-second deadline, API responses are bounded to 8 MiB, covers to 4 MiB, and streamed bodies are checked even without a Content-Length. Tests cover exact/declared/streaming limits, redirects, cancellation and non-HTTPS rejection. There is no authenticated account API traffic from the shortcut feature. Fetching public metadata/images still sends the user's network address to the destination/service infrastructure.

Automatic image URLs must meet the CDN/provider domain boundary allowlist. Literal IP, local-style/single-label names, userinfo, nonstandard ports and other schemes are rejected. This is syntactic filtering, not DNS pinning: a trusted-domain compromise, malicious DNS/proxy configuration or hostname resolving privately is residual risk. No hostile endpoint was contacted to test rebinding. Provider data is parsed as JSON/plain text, not executed as XAML/HTML. Caps, bounded provider counts, field lengths/value limits and constrained network concurrency reduce resource exhaustion; they do not eliminate CPU/memory pressure or incorrect feed content.

## Files, images, backups and data integrity

Library/cache/import reads have size limits; JSON has finite parser depth and explicit structural/collection validation. Imports require the expected envelope/schema, preview and explicit replacement confirmation. Current bytes are hash-checked against the preview and backed up before replacement; device preferences/wallpaper paths are not imported. Flags are namespaced by provider/native identity; grouping does not merge routes or claims. Tests cover malformed/null/oversized data, duplicate identities, hash collisions, schema/restore behaviour and offline disabled claims.

Writes use unique CreateNew temporary names, flush and replacement. A failed library load prevents silent autosave replacement. Managed backup naming prevents traversal; only direct controlled snapshots are eligible for retention (20). Direct data/cache/backup/file reparse paths are rejected. These are not race-free handle-based filesystem protections and do not inspect every ancestor or sandbox processes running as the same Windows user. Explicit export file pickers can select user-chosen destinations, including synced folders; no promise is made that such exports always remain on the device.

Local collections/cache/backups are ordinary unencrypted JSON under the existing version 2 data folder. They contain titles, links and preferences, not store credentials. Windows account permissions are the protection boundary; backups/exports may expose collection history. Error/trace messages can include URLs or paths; users should review diagnostics before sharing. Portable and installed Windows v2 intentionally share v2 data; beta/iOS remain separate.

Images are validated by Skia before scaled bitmap decoding: at most 4 MiB encoded, 8192 pixels per axis and 16 million pixels. Wallpaper paths must be in the managed local Backgrounds folder; UNC/outside/direct-linked paths are refused. Tests cover invalid/oversized image data, dimensions and path restrictions. Native image decoding remains an attack surface; these controls cannot prove absence of native codec vulnerabilities or decompression/CPU attacks. Low graphics mode reduces remote artwork use.

## Build, supply chain and installation

NuGet sources/audit sources are explicitly NuGet.org. Configuration-specific locks contain package hashes; Release excludes Debug diagnostics. All advisory severities plus unavailable advisory-data warnings fail restore. Both Debug and Release fresh locked audited restores succeeded; separate live direct/transitive vulnerability JSON listings reported no known vulnerable packages. See restore-debug.txt, restore-release.txt, vulnerabilities-debug.json, vulnerabilities-release.json and packages-release.json. No dependency versions were changed in this update. An advisory database result does not establish that a package is uncompromised or cover every native/transitive/runtime vulnerability.

Build scripts use fresh distinct output directories, stop on restore/publish/compiler failure and do not reuse old releases. A simulated restore failure stopped before publish; both scripts parsed successfully. Packaging remains self-contained, untrimmed win-x64; ZIP compression is external, no executable packer or runtime trimming. Installer is per-user/asInvoker, does not require elevation, retains user data on uninstall and does not force a silent post-install launch. Startup is opt-in, quoted and current-user only; loading settings does not enable it. Registry/startup changes were not exercised against the real account.

Installed build environment: SDK 10.0.403 and runtime 10.0.14. Official .NET release material identifies 10.0.14 as a security release addressing Brotli and source-built Linux distribution issues; SafeHttp additionally has automatic decompression disabled. Runtime choice in the fresh published dependency graph must be recorded with the artifact checks. We did not replace system runtimes or build tools. The framework's published policy and SkiaSharp servicing notes were consulted; neither replaces package scanning or native-code review.

## Actual validation and limits

375 regression assertions completed after the fixes, including the complete existing security/reliability/UI suite plus 35 new assertions. One assertion aggregates 2,000 malformed URL inputs; this is bounded deterministic robustness testing, not comprehensive fuzzing. XAML was rendered headlessly at 1000 and 1280 pixels for the sign-in page, alongside prior grid/layout and theme coverage. The rendered sign-in screenshot was inspected. No real authentication, account linking, checkout, claim or browser was launched; unknown-tag handler rejection was exercised and valid launch configurations inspected.

Native execution/installation/update/uninstallation, operating-system shell association behaviour, hardware graphics, full assistive-technology behaviour, clean-machine self-extraction, browser extension/session security, antivirus scanning, packet capture and independent penetration testing were not performed. No malware-clean claim is made. Binary signing, hashes, portable/source ZIP integrity and successful final compilation are recorded during finalization. Hashes verify file consistency, not publisher identity. Unsigned packages can trigger Windows warnings; do not disable antivirus or SmartScreen.

Before outside testing, use a disposable Windows VM/account without valuable credentials: verify normal launch and grid, manual official-browser handoff/domain, Cancel/Open claim preview, session reuse/expiry, sign-out, persistence and malformed-data preservation, opt-in startup/tray behaviour, software rendering, install/update/uninstall and coexistence with untouched beta/iOS data. Scan final binaries with the target environment's security software and obtain independent review/signing before broader distribution. These remain open validation items, not completed results.

## Primary sources checked

- Steam official sign-in: https://store.steampowered.com/login/
- Epic official sign-in: https://www.epicgames.com/id/login
- GOG official homepage: https://www.gog.com/ (redirects to /en/)
- Ubisoft account entry: https://account.ubisoft.com/ (redirects to official Ubisoft account page)
- itch.io sign-in: https://itch.io/login (automated web fetch returned 403; no successful real sign-in claimed)
- NuGet vulnerability listing: https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-package-list
- .NET servicing notes: https://github.com/dotnet/core/blob/main/release-notes/10.0/10.0.14/10.0.14.md
- Avalonia security policy: https://github.com/AvaloniaUI/Avalonia/security
- SkiaSharp 3.119.4 servicing notes: https://mono.github.io/SkiaSharp/docs/releases/3.119.4.html

## Final build evidence

Fresh Release publish and Inno compilation completed. Bundled dependency graph: runtimepack.Microsoft.NETCore.App.Runtime.win-x64/10.0.14. Debug DiagnosticsSupport is absent. Antivirus status inspection was attempted but the local Defender module could not load because its Windows remoting integration is unavailable; antivirus scanning remains unvalidated. No protection settings were changed. See antivirus-availability.txt and bundled-runtime.txt.
