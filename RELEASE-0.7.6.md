# LootDrop version 2 Windows — 0.7.6

Offer details, compact status icons, dashboard cleanup and searchable Help. November 18, 2026.

## Offer details without crowded cards

Every offer has a visible Details button. Clicking its title, cover or one of the small status icons opens the same right-hand details drawer. It shows the full description, provider/platform, advertised value, price evidence, listed expiration, actual destination domain and full supported URL, regional/account eligibility guidance and recorded check times. Wishlist, manually claimed and local not-free controls remain available there.

The drawer's website action still opens the existing destination-confirmation dialog. It does not launch a site just because Details was opened. Escape, the close button or the backdrop closes the drawer; covered controls are disabled while it is open. Refresh/import closes details before replacing its offer records, and the claim handler still checks that the offer is current and eligible after confirmation.

## Small icons with useful explanations

The card now shows a short title, destination label, advertised-value summary and a visible Live/Cached/Saved/Unavailable state. Three compact status controls provide:

- **Address:** supported HTTPS format, pending or blocked/unavailable address. This is explicitly not a page-reachability, redirect-safety or free-price guarantee.
- **Price:** verified catalog evidence, unverified price, pending checks or a rejected/expired offer. A positive price icon requires current live evidence and a recorded catalog-check time. Cached/saved records never receive a live verified icon.
- **Time:** provider freshness, cached/saved status and listed expiration.

Hover displays a wrapping explanation; clicking opens the full details drawer. Wishlist and claimed toggles also have hover text and accessible action names. Icons distinguish states by glyph as well as color. A visible Details button and searchable Help provide alternatives to hover.

The last successful provider retrieval is retained in saved metadata. Address timestamps describe a local address-format check, not a page download. Price timestamps are set only when a supported catalog response is available. Epic's cached catalog preserves its original retrieval time rather than presenting every reuse as a fresh query. Missing timestamps are shown as not recorded. User imports and offline cache do not restore a verified-price result.

## Cleaner dashboard

Repeated long verification paragraphs were removed from each card. Provider status is summarized by the Feeds button: hover for the source list or click to pin it. The dashboard header and statistics are smaller; estimates, filter details and live platform counts have contextual hints. Important cached/saved states and disabled claim controls remain visible.

The sidebar scrolls at smaller heights so Help and other sections remain reachable. Hover colors follow the selected palette, and the status controls carry automation names/help text for assistive technologies. Presentation notifications avoid allocating a new property list per update or running when no UI subscriber exists. No new polling timer, additional feed request or image download is caused by opening details or hover hints. This is a layout/data-flow improvement, not a measured CPU/startup benchmark.

## Plain-language Help

Help is available in the sidebar or through F1. Twelve searchable topics explain getting started, icons and verification limits, disabled links, regions, tracking, offline cache/source health, duplicate routes, filters/value estimates, backups/import/recovery, themes/performance, tray/startup/keyboard controls and privacy/safe testing. Navigation shortcuts lead back to Discover, Settings or Themes. An unmatched query shows an empty-state hint. `HELP.md` is generated from the same text used by the app.

## Validation and security boundaries

Evidence is under `audit-evidence/0.7.6/`. Tests cover model status/timestamp behavior, cached/saved claim restrictions, actual hover-popup bindings, live icon/popup changes when price evidence changes, accessible button names, local details opening without a provider request, disabled covered controls, Escape/F1, Help filtering/empty results, refresh invalidation and minimum-window availability. Existing theme/contrast, cache, backup, identity and transport/image regression checks remain in the suite.

Actual-XAML screenshots use synthetic data and the headless renderer. Native Windows installation, real hardware rendering, full screen-reader behavior and physical mouse/keyboard testing remain manual items before outside distribution. Compilation and advisory scans are not a security certification.

The self-contained, untrimmed Windows x64 packaging and standard ZIP size improvements from 0.7.5 are retained. No additional runtime package, font, account access, automatic claim, telemetry or cloud upload was added. Startup remains opt-in. App and installer remain unsigned. Do not disable antivirus or SmartScreen to test them. Beta/iOS source and real user data were not modified.

## References

- [Avalonia hover tooltips](https://docs.avaloniaui.net/controls/feedback/tooltip)
- [Avalonia accessibility metadata](https://docs.avaloniaui.net/docs/app-development/accessibility)
- [0.7.5 reliability and package-size notes](RELEASE-0.7.5.md)
- [Original security audit](SECURITY-AUDIT.md)

## Final results

- Completed 316 security regression checks. No app launched, real links fetched, or startup registry changed.
- Updated application and installer version: 0.7.6.0. Release publish and Inno compilation completed successfully. No native app/installer was executed.
- Live NuGet vulnerability listing: no known vulnerable direct/transitive Release packages reported. Debug and Release audited restores completed without advisory warnings.
- Actual XAML was rendered headlessly for all 18 presets. Light/neon Settings and synthetic collection screenshots were inspected; input readability and selected/hover text contrast were also checked automatically.
- Fresh portable ZIP read/hash validation succeeded. Both app and installer Authenticode status: NotSigned.
- Simulated audit/restore failure stopped before publishing. Build script parsed without errors.
- iOS fork hashes unchanged. No beta or personal-library changes were made.
- Native GUI, real browser handoff, install/update/uninstall, startup and clean-machine validation remain outstanding. Outside distribution is not cleared by this report.
