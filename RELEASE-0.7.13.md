# LootDrop Windows v2 0.7.13 — cleaner interface, fonts and Settings

## Interface

Offer groups are centered within their available grid, with consistent header and title space so short and wrapped titles leave actions aligned. Covers and inner cards are centered, primary actions have centered labels, and buttons, dropdowns and inputs share softer corners and more consistent sizing. Header surfaces now match the selected palette rather than defaulting to black. Status glyphs use a stable symbol face independently of the selected text font.

Inactive search boxes are replaced with a breadcrumb on Settings, Themes, Store sign-in and Help. The sidebar layout retains its compact page selector and single column of responsive cards. Destination confirmation, price evidence and disabled-claim rules are retained.

## Embedded fonts

Settings > Appearance & fonts offers Outfit (default), Space Grotesk, Sora and the existing Inter. Each option previews instantly, applies to the interface and app dialogs, and persists on this computer. Font choice is independent of the color theme. Importing a collection preserves the destination computer's font preference.

The three new families use unmodified static regular and semibold/bold files from pinned official author repositories. No Windows font installation, font-directory scan, runtime font download or new NuGet package is needed. About contains their full SIL Open Font License texts, also included in the project. See Assets/Fonts/README.md for source revisions and SHA-256 hashes. Fixed-weight files were chosen after render checks found thin defaults in variable fonts.

## Settings and controls

Settings branches into General, Startup & tray, Window & layout, Appearance & fonts, Library & backups, Updates and About LootDrop. Only the selected category is shown. Expanded navigation uses a submenu; compact navigation and docked mode use a category dropdown. Navigating categories does not change preferences or trigger startup/update/docking actions.

On/off preferences now use pill-shaped switches with 44-pixel minimum hit targets, clickable labels, Space-key activation, visible keyboard focus, disabled states and palette-aware colors. Selection checkboxes retain checkmarks with rounder corners; sliders follow the theme accent. Switch movement is short and is disabled in Low graphics mode. These use the existing checkbox behavior and event handlers, preserving their accessibility state and persistence.

## Validation and release status

599 regression checks and 62 real-Windows docking checks passed. 599 regression checks and 62 real-Windows docking checks passed. The full regression suite, actual embedded font-family/weight checks, normal/360-pixel docked category rendering, card-action alignment, font restart/import behavior and switch input checks were run with synthetic data. Existing native desktop-reservation checks were rerun separately. Evidence and final counts are in audit-evidence/0.7.13/ and SECURITY-UI-0.7.13.md.

This remains an unsigned tester prerelease. Real mixed-DPI/hotplug transitions, actual shell/app crashes, clean installation/update/uninstall, screen-reader testing and long-duration use still need coverage. Passing tests are not a security certification. Beta and iOS files/data remain unchanged. Every published build is announced in Discord after uploads finish, with all pings disabled.
