# LootDrop version 2 Windows — security recheck and themes

Date: November 18, 2026 (America/Chicago). Project version: 0.7.4.2.

This is a follow-up to SECURITY-AUDIT.md. It is not security certification or approval for outside distribution. The scope is the Windows version 2 project only. iOS source and the beta remain unchanged; no existing user library was loaded or edited during validation.

## Rechecked

- Feed/API transport, size/deadline limits, image allowlist and decode limits, browser handoff and destination confirmation.
- Store-verification host and app-ID boundaries, conservative price evidence and saved-record claiming restrictions.
- Local JSON/theme persistence, corrupt-library preservation, wallpaper-path boundaries and startup registration.
- Debug/Release dependencies, NuGet audit failure behavior, fresh publishing and installer definition.
- Theme lookup and custom color parsing, light/dark control variants, persisted palette identity and foreground contrast.

## Additional fixes

1. The shared HTTP download helper now rejects non-HTTPS/unsupported URLs before dispatch and rejects invalid body-limit arguments. The original caller checks remain in place. Synthetic tests verify the boundary without contacting any destination.
2. Theme JSON is validated before both reading into the UI and writing a replacement library: bounded preset names/background paths, at most 256 named themes, names up to 45 characters and strict six-digit HEX colors. Saved URLs/provider identifiers are also bounded. Unsupported data is preserved rather than overwritten; there is no automatic migration or deletion of the user's file.
3. Epic advertised values now use the same maximum supported currency range as other providers/local persistence, avoiding the possibility of a feed value that saves successfully but cannot be read on the next launch.
4. Saved custom themes store their base palette as well as their accent, so light/pastel combinations survive saving/reloading. Older custom themes without that new field default to the original Purple base.
5. A headless-validation constructor accepts a separate library and disables feed polling and startup-key access. The normal constructor retains normal application behavior. Saved records are displayed immediately with claiming disabled until refreshed.

## Theme changes

Eight existing presets remain available. Ten combinations were added: Obsidian Ice, Crimson Noir, Forest Night, Arctic Day, Peach Cream, Lavender Cloud, Mint Sorbet, Rosewater, Cyber Lime and Neon Arcade.

Each controls the window/sidebar/panel/card/input surfaces, primary and secondary text, accent, hover/selected button text, status colors and matching existing icon. Five new combinations use light Fluent controls. Text accents and status colors are adjusted when needed to maintain a calculated contrast ratio of at least 4.5:1 against the supported solid surfaces; very pale or dark custom accents do not make their text unreadable. This is not a complete accessibility certification and does not cover arbitrary user wallpapers, disabled-control opacity or every native rendering condition.

See THEMES.md for the combinations and saved-custom-theme behavior.

## Validation evidence

Fresh Debug and Release locked dependency restores completed with NuGet auditing enabled for direct/transitive dependencies. The live Release vulnerability listing reported no known vulnerable packages. Evidence is under `audit-evidence/2026-11-18/`. A network-unavailable attempt correctly failed the existing NU1900/NU1905 gate; it was not bypassed.

The offline regression harness includes the original URL, streaming-response, image and library-corruption cases plus palette contrast, actual HEX-input readability, all preset UI selections and persistence, named custom palette round-trips and isolated collection rendering. Headless screenshots are rendered from the actual XAML without opening a native window. Final check/build results are recorded below after the corresponding commands complete.

No automatic browser opening, store login/claim/checkout, installer execution, startup registration or personal-library migration was performed. NuGet results are point-in-time advisory checks, not proof that every managed/native dependency or SDK/runtime is free of vulnerabilities.

## Remaining limits

- Public HTTPS pages can still be phishing destinations or redirect elsewhere. The preview is a user decision aid, not a site certificate. DNS is not pinned, and allowlisted providers/CDNs/system proxy settings remain trust dependencies.
- The native image decoder remains an attack surface despite byte/pixel limits. A malicious process with the same Windows-user privileges can replace data/code or race file checks.
- Local game/theme JSON is ordinary unencrypted per-user data, not a credential store. No passwords, API credentials, automatic account access or telemetry were added.
- Native GUI behavior, installer update/uninstall, startup, real hardware/software rendering, wallpaper extremes and clean-machine operation remain manual validation items before sharing. Headless screenshots do not establish these behaviors.
- Binaries remain unsigned. Do not bypass antivirus, SmartScreen or signature protections. The source/ZIP checksums verify file identity, not publisher identity. SDK/runtime patch currency and the complete native supply chain are not independently certified here.

## External checks consulted

- [NuGet package auditing](https://learn.microsoft.com/en-us/nuget/concepts/auditing-packages) documents transitive advisory checks and unavailable-audit-data warnings.
- [Avalonia's October incident statement](https://avaloniaui.net/blog/security-incident-oct26) reports a website-CMS compromise, rejected package-publishing attempts and no identified package tampering. That is the vendor's published assessment, not an independent supply-chain attestation. This review did not infer that installed packages were compromised or automatically replace them on that basis.

## Manual commands

```powershell
dotnet restore .\SecurityChecks\SecurityChecks.csproj -p:Configuration=Release --locked-mode
dotnet run --project .\SecurityChecks\SecurityChecks.csproj -c Release --no-restore
# Optional actual-XAML screenshots in the harness's own build directory:
dotnet run --project .\SecurityChecks\SecurityChecks.csproj -c Release --no-restore -- --theme-screenshots
.\Build-Tester-Releases.ps1 -Target All
```

All checks use synthetic local data. The release script performs a fresh locked advisory check before publishing, creates a new output folder and never installs or launches the result.

## Final results

- Completed 242 security regression checks. No app launched, real links fetched, or startup registry changed.
- Updated application and installer version: 0.7.4.2. Release publish and Inno compilation completed successfully. No native app/installer was executed.
- Live NuGet vulnerability listing: no known vulnerable direct/transitive Release packages reported. Debug and Release audited restores completed without advisory warnings.
- Actual XAML was rendered headlessly for all 18 presets. Light/neon Settings and synthetic collection screenshots were inspected; input readability and selected/hover text contrast were also checked automatically.
- Fresh portable ZIP read/hash validation succeeded. Both app and installer Authenticode status: NotSigned.
- Simulated audit/restore failure stopped before publishing. Build script parsed without errors.
- iOS fork hashes unchanged. No beta or personal-library changes were made.
- Native GUI, real browser handoff, install/update/uninstall, startup and clean-machine validation remain outstanding. Outside distribution is not cleared by this report.
