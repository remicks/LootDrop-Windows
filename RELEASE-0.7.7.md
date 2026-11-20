# LootDrop Windows v2 0.7.7 — compact card grid hotfix

The 0.7.6 grouped-offer container stretched across the discovery window, leaving a large blank area beside each card. The outer list now wraps compact 302-pixel groups into columns. Long group headings wrap to two lines with an ellipsis and a full-text hover hint. Duplicate routes remain together inside their expandable game group. Existing offer actions, verification restrictions, Help and library data formats are unchanged.

Validation: 340 regression checks completed, including 24 new layout assertions at 1000, 1280 and 1600 pixels, each with expanded and compact sidebars. Checks cover group widths, multiple columns, row wrapping, horizontal overflow and preservation of duplicate routes. Actual XAML was rendered headlessly and the compact-grid screenshot was inspected. Tests use synthetic data and do not launch the native application, open real claim links or change startup registration.

The Release dependency restore refreshed advisory data successfully with audit warnings treated as errors. Packaging retains self-contained, untrimmed Windows x64 output and the previous ZIP compression settings. The final build uses a fresh locked restore and fails on audit/restore errors. No new dependencies or unsafe automation were introduced. This focused hotfix is not a new comprehensive security audit or distribution certification.

App and installer remain unsigned. Native installation, clean-machine testing and real hardware interaction remain untested. Do not disable antivirus or SmartScreen to test. Beta and iOS projects were not modified. The Windows source was copied into the current workspace; no personal library data was copied or accessed.

Historical security findings and limits remain documented in SECURITY-AUDIT.md and RELEASE-0.7.6.md. Current evidence is in audit-evidence/0.7.7/.

Final packaging validation: version 0.7.7.0; fresh locked audited restore, Release publish and Inno compilation succeeded. Portable archive contains one executable matching the fresh published executable by SHA256. App signature: NotSigned; installer signature: NotSigned. Neither was installed or launched.
