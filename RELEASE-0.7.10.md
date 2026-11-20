# LootDrop Windows v2 0.7.10 — manual GitHub update checker

## How to use

Install/extract 0.7.10 manually once to obtain the checker. In Settings > App updates, click Check for updates. Enable Include tester prereleases to include the current unsigned tester channel; normal releases are the default, and the choice is session-only. Select the offered installer or portable ZIP and choose Download unsigned package, then select a local folder. Review notes first. Back up the library and close LootDrop before manually updating. Nothing is installed or launched by the checker. Existing packages are not overwritten; choose another folder if a copy is already there.

## Behaviour and security

No startup/timed update check or download occurs. The app reads the public, unauthenticated releases API for remicks/LootDrop-Windows only. GitHub sees the user's network address; no token, store credentials, library data or account/session contents are sent. Stable releases are selected by default; drafts never qualify. Numeric three/four-component tags (optional v prefix) are compared numerically. Unsupported suffix tags are skipped; up to 100 recent published releases and a 2 MiB response are considered. A failed check is not presented as up-to-date. Changing channel clears prior download actions.

Metadata must point to the exact repository/tag release page and exact versioned Setup.exe or Portable-win-x64.zip name/URL, with uploaded state, positive bounded size and valid SHA256 digest. The newest valid release is shown even if its packages are incomplete; there is no silent fallback to an older installer. Duplicate package names are excluded. Notes are plain text, truncated to 16,000 characters and never rendered as executable HTML/XAML. No third-party Markdown renderer or new dependency was introduced.

On explicit download, initial URLs are reconstructed/validated against that repository. Automatic redirects remain disabled on the underlying transport. The downloader handles at most three redirects/four responses, permitting HTTPS on github.com and release-assets.githubusercontent.com only, without userinfo, fragments or alternate ports. Cookies/default credentials/decompression remain disabled. Body size is bounded by the advertised size and a global 256 MiB limit, with a ten-minute operation deadline and user cancellation. Only after exact size and SHA256 match is a uniquely created partial file renamed to the final name without overwrite. Failed/cancelled partial writes are removed. No process launch, extraction, application replacement, startup change or elevation is triggered by a download. Opening release notes uses the fixed repository URL separately.

Checksums come from GitHub metadata over normal TLS. They protect transfer consistency, not against a compromised maintainer/GitHub account, malicious release contents or a compromised machine. The binaries remain unsigned. SmartScreen/Defender checks must remain enabled. The file picker lets the user choose a local filesystem folder; synced local folders can upload saved files according to the user's sync configuration. Same-user filesystem races remain outside application isolation guarantees. A network attacker cannot set an arbitrary installer path through release metadata.

## Actual validation

417 regression assertions passed, including 42 new update assertions and the complete previous suite. Tests exercise numeric ordering/equal/older versions; draft/prerelease filters; invalid tags, URLs, digests and sizes; incomplete newest packages; malformed JSON; actual UI states and channel changes; no automatic checks; trusted and rejected redirects; redirect loops; checksum/truncation/chunked-oversize failures; cancellation and preservation of existing files. The actual XAML update section was rendered headlessly and visually inspected. Its available-update screenshot uses an explicitly synthetic v9.0.0 fixture, not a published release.

Live integration used the production UpdateService and GitHub's real API: stable channel correctly had no published stable release; tester channel found v0.7.9 with two supported packages. A simulated 0.7.8 client detected it as newer; 0.7.10 correctly did not offer a downgrade. The production downloader fetched LootDrop-v2-0.7.9-Setup.exe into an isolated test directory, followed the real permitted GitHub redirect, verified all 36,510,368 bytes and SHA256 5644a5e94a277e4ce91ce5c079cd42c8946dc9b7a9510c5658213f79bd286475. The file was never executed/installed. Evidence is audit-evidence/0.7.10/live-validation.txt. A test-harness context deadlock was corrected by running optional live checks off the headless UI context; final live validation completed successfully.

Native folder-picker interaction, real hardware rendering, installation/update/uninstall and clean-machine testing remain manual. No real library, beta/iOS source/data, signing subscription or Windows protection settings were modified. This update extends the previous security review; it is not independent penetration testing or certification. Signing remains deferred at the user's request. Fresh audited release restore/publish and package integrity results are added at finalization.

## Primary documentation

- GitHub release listing (including prereleases): https://docs.github.com/en/rest/releases/releases#list-releases
- GitHub release assets and SHA256 digest: https://docs.github.com/en/rest/releases/assets#get-a-release-asset


Final packaging validation: version 0.7.10.0; fresh locked audited restore, Release publish and Inno compilation succeeded. Portable archive contains one executable matching the fresh published executable by SHA256. App signature: NotSigned; installer signature: NotSigned. Neither was installed or launched.
