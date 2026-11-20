# Installer troubleshooting — version 2 security update

Use Build-Tester-Releases.ps1, which creates fresh audited output. `-SkipPublish` is intentionally unsupported. Opening the .iss file directly will fail because its publish path must come from the build script.

If the compiler is missing, install Inno Setup from its official publisher and supply its trusted ISCC.exe path via `-InnoCompilerPath` if required. Compiler discovery checks conventional Program Files locations; it no longer executes an arbitrary compiler found through PATH or environment variables.

If NuGet audit data is unavailable or a package advisory is reported, the build stops before publishing. Restore connectivity or investigate/update the dependency; do not suppress the warning to obtain a release. If a lock mismatch occurs after an intentional package update, review and regenerate the Release lock, then repeat the security checks.

If compilation fails, keep the full log. A portable ZIP or installer from an earlier folder is not a substitute for a successful current build. Output folders are unique and are never automatically cleaned up.

See SECURITY-AUDIT.md and BUILD-FOR-TESTERS.md for current commands and manual validation requirements.
