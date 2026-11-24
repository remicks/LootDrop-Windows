# LootDrop Windows v2 0.7.14 — six themes and five fonts

The appearance catalog now offers **24 themes and nine embedded interface fonts**. Existing choices, defaults and saved preferences are retained.

## New themes

- **Midnight Slate:** cool blue and silver on deep slate surfaces.
- **Amber Terminal:** warm charcoal with an amber accent.
- **Deep Ocean:** blue-green surfaces with sea-glass highlights.
- **Paper Ink:** warm off-white surfaces and blue ink.
- **Periwinkle Dawn:** light blue/periwinkle pastel surfaces.
- **Neon Voltage:** cool graphite with electric yellow highlights.

Choose presets on Themes, or use Settings > Appearance & fonts > Themes & app icons. Presets retain coordinated background, navigation, card, input and text colors and can still be used as bases for named custom themes. Text/accent colors use the existing contrast correction.

## New fonts

**Lato, Montserrat, Source Sans 3, IBM Plex Sans and Atkinson Hyperlegible** join Outfit, Space Grotesk, Sora and Inter. Choose them in Settings > Appearance & fonts. Font choice remains independent of the color palette, applies immediately, survives restart and remains local to the computer. Collection imports preserve the destination font preference.

The ten new font files are unmodified static regular and semibold/bold faces from pinned official author/Google Fonts repositories. They total 3,610,000 bytes before package compression. No font installation, runtime font download, new NuGet dependency or font subsetting tool is used. Full licenses are embedded and listed in About; source revisions and SHA-256 hashes accompany the project under Assets/Fonts.

## Validation and status

723 offline security/reliability/UI assertions passed, including all 24 selectable palettes, readable text on every surface, actual regular and semibold/bold glyph faces for all nine fonts, aligned card actions, font persistence/import isolation, and the five additions in 360-pixel sidebars using both light and dark palettes. Rendered previews were inspected. Native docking checks, dependency advisory refresh and final artifact verification are recorded in audit-evidence/0.7.14/ and SECURITY-THEMES-FONTS-0.7.14.md.

This remains an unsigned tester prerelease. Clean install/update/uninstall, screen-reader checks, physical hotplug/mixed-DPI changes, actual shell/app crashes and extended use still need coverage. Passing tests are not a security certification. Beta and iOS files/data remain unchanged. Every published build is announced in Discord after uploads finish, with all pings disabled.
