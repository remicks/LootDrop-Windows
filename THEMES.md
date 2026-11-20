# Version 2 color combinations

Open **Themes** in the sidebar. The eight original color presets remain at the top. Ten additional combinations appear below them, each with background/accent/text swatches:

| Preset | Style | Combination |
| --- | --- | --- |
| Obsidian Ice | Dark | Charcoal and ice blue |
| Crimson Noir | Dark | Black cherry and rose |
| Forest Night | Dark | Pine and sea glass |
| Arctic Day | Light | Cloud white and cobalt |
| Peach Cream | Light | Cream and terracotta |
| Lavender Cloud | Pastel | Lilac and plum |
| Mint Sorbet | Pastel | Mint and evergreen |
| Rosewater | Pastel | Blush and mulberry |
| Cyber Lime | Neon | Graphite and acid lime |
| Neon Arcade | Neon | Midnight violet and hot pink |

A preset changes backgrounds, panels, cards, controls, text, status colors and the window/tray icon color together. Light/pastel combinations also switch standard controls to their light variant. Existing icon assets are reused, avoiding extra native/image dependencies or downloads.

To customize a combination, select its preset, apply a HEX/RGB accent and save a named custom theme. Its base combination is retained, including light/pastel surfaces, when loaded later or after restarting. Older saved custom themes without a base-combination field retain the original Purple base. Selecting a preset saves the selection through the existing local library mechanism.

Primary/secondary text and text accents are adjusted for contrast; bright neon buttons use dark lettering where needed. Very light custom accents on a light theme still receive readable text equivalents. Wallpaper content and disabled-control opacity can affect perceived contrast and are not fully covered by the solid-color checks.
