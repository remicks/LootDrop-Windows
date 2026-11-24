# LootDrop Windows v2 0.7.14 — theme and font review

Date: November 23, 2026 (America/Chicago).

This is a targeted review of six added palette records, their selector buttons, five embedded font families, license display, persistence and appearance regression coverage. It retains the broader existing security tests and does not constitute an independent audit, security certification or general public-release approval.

## Boundaries preserved

- Font preference remains a fixed allowlist of nine bundled families. Saved settings cannot introduce a URL, arbitrary local path or user-supplied font. The previous default and existing font keys remain unchanged. Collection import preserves the local font setting.
- Added font files are copied unmodified from official sources at pinned commits, with unchanged license texts. Actual regular and semibold/bold glyph faces are checked by family and weight, rather than accepting a visually similar system fallback. Sources and SHA-256 hashes are recorded under Assets/Fonts.
- Fonts/licenses are application resources, with no Windows font installation, directory scan, elevated operation or runtime font request. Ten added TTF files total 3,610,000 bytes before compression. No new package, executable font tool, font modification/subsetting step or build download is introduced.
- License display is derived from the internal font catalog rather than a separate manually maintained family list. Only known internal license resource names are opened. Font rasterization uses the existing Avalonia/Skia stack; provenance and successful parsing are not guarantees against all font-parser vulnerabilities.
- Palettes are fixed hexadecimal color records. The existing contrast adjustment applies to every palette and extreme custom accents; preset selection does not alter security, startup, network, update or docking settings.
- No claim/URL validation, credential handling, provider endpoint, updater policy, native message permission, signing policy or user-data location was changed. Beta/iOS projects and data remain untouched.

## Validation

The rendered suite passed **723 regression assertions**. This includes contrast >= 4.5 for five readable text/status roles on five surfaces in every palette, accent/hover text contrast, selectable UI buttons for all 24 palettes, persistence and restoration, all nine font families at regular and semibold/bold weights, card action alignment, known font-key validation and portable-data isolation. Added coverage checks each new font in the 360-pixel sidebar using Paper Ink and Deep Ocean, with independent font/palette persistence and fresh-window restoration. All eight additional bundled license texts are present in About. Existing switch input, Settings navigation, updater, URL, cache, backup, icon and safe-shortcut regressions remain included.

A separate real-Windows run passed 62 docking checks across three monitors, with all original work areas restored. Native Windows docking logs, refreshed locked dependency restores, final executable icon/manifest inspection, archive verification and upload hashes are recorded separately in the evidence and release artifact reports. Package references and lock files remain unchanged. Advisory refresh failures and NU1900–NU1905 still block builds.

Tests use synthetic libraries and inert shortcut fixtures. They do not install fonts or software, inspect credentials, open claim pages, change real startup settings, terminate Explorer or reconfigure displays. Native checks briefly use only their own windows and verify restoration of work areas.

## Remaining coverage

Screen-reader behavior, clean installation/update/uninstall, real mixed-DPI/physical display transitions, actual shell/app crash recovery, remote desktop, fullscreen interactions and long-duration use still need tester coverage. Existing sidebar limitations and unsigned status remain. Fullscreen/system UI may cover the sidebar; it is not a security boundary.

See Assets/Fonts/README.md and provenance-0.7.14.json for official font repositories/revisions and complete source mappings. Historical UI/sidebar security reports remain available.
