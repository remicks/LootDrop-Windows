# LootDrop Windows v2 0.7.13 — UI security and stability review

Date: November 23, 2026 (America/Chicago).

## Scope and changes

Targeted review of the typography catalog, embedded font assets/licenses, local preference validation, categorized Settings navigation, rounded control templates, interface layout and their interaction with existing app behavior. Existing URL, provider, cache, backup, store shortcut, update and native icon regressions were retained. This is not an independent audit, certification, exhaustive dependency inspection or general public-release approval.

The beta/iOS forks and their data are unchanged. Tests use isolated synthetic libraries and inert shortcut targets; they do not inspect browser credentials, open claim pages or write real startup settings.

## Security boundaries

- Font selection is an allowlist of four internal resource families. A setting cannot specify a network URL or arbitrary filesystem path. Malformed font keys are rejected before use. The default is bundled Outfit; existing Inter remains available.
- New font resources are unmodified fixed-weight files from pinned official author commits. Their SHA-256 hashes and licenses are included. Both regular and semibold/bold glyph faces were resolved and checked against the requested family/weight, avoiding silent system-font fallback and thin variable-font defaults. Font rasterization still depends on the existing Avalonia/Skia native stack; trusted provenance and successful parsing are not proof that all font parsers are vulnerability-free.
- Fonts and their licenses are embedded into the executable. No runtime font fetching, global font installation, elevated access or new package reference is introduced. The existing font manager/rasterizer handles them.
- Font preference is local to Windows v2. Collection export uses portable defaults, and import preserves the destination preference. Existing corrupt-library protections, atomic saves and backups remain active.
- Category navigation changes panel visibility, selected navigation state and scroll position only. Existing startup opt-in, updater network actions, destination confirmation and dock reservation gates remain under their original explicit controls. An inactive search box is hidden, without changing search or filter state.
- Rounded switches retain CheckBox/ToggleButton state and original event handlers. Keyboard focus, Space-key activation, click targets, disabled behavior and reduced motion were considered; no unsafe input automation is added to the app. Existing navigation and status glyphs remain separate from custom text typography.
- No changes were made to external endpoints, credential handling, command execution, release signing, window-message permissions or the native reservation service. No telemetry, automatic claiming, updater installation or security-setting change was introduced.

## Validation

The final rendered suite passed 599 regression checks. The independent Windows docking run passed 62 checks on three monitors and restored every original work area. Final logs are in audit-evidence/0.7.13/ and package checks are in the release artifact validation report. Checks cover all four real embedded typefaces and intended weights, immediate/persisted/restarted font choices, import isolation, rejecting URL-like font keys, seven categories and relevant-panel visibility, narrow Settings rendering, aligned card actions with short/wrapped titles, pill-shaped tracks, 44-pixel targets, Space and pointer-label activation, theme accents and disabled new motion in Low graphics mode. Dark, gold and pastel layouts were rendered and inspected.

The full security/reliability regression suite and real Windows desktop docking harness were run independently. Locked dependency restores refresh advisory data; NU1900–NU1905 remain build errors, and package references/lock files are unchanged. Final executable resources and source/portable ZIP contents are verified during packaging. Installer compilation is checked without installing the build.

## Limitations

Headless snapshots and isolated desktop tests do not replace screen-reader validation, real install/update/uninstall, clean-machine testing or long-duration use. Existing sidebar limitations remain: real physical display unplug/replug, mixed-DPI changes/rotation, real Explorer/app crash recovery, other AppBars/auto-hide taskbars, fullscreen games and remote desktop still need tester coverage. Builds remain unsigned at the user's request; Windows reputation is unchanged.

## References

- [Avalonia embedded font guidance](https://docs.avaloniaui.net/docs/styling/custom-fonts)
- Font provenance and full licenses: Assets/Fonts/README.md and Assets/Fonts/*-OFL.txt
- Native sidebar review: SECURITY-SIDEBAR-0.7.12.md
