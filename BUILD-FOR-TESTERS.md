# Building LootDrop version 2 packages

**0.7.5:** use PowerShell 7.2+ (`pwsh`). The portable ZIP uses standard SmallestSize compression. The single-file payload is internally uncompressed so the installer compresses better; the extracted EXE is larger. Releases remain self-contained and untrimmed. See RELEASE-0.7.5.md for measurements and recovery/duplicate-handling changes.

Read SECURITY-AUDIT.md first. Requirements: .NET 10 SDK; Windows x64; Inno Setup 6 or 7 for installer builds. Obtain tools from their official publishers. The scripts never download/install tools, launch the app, register startup, or upload artifacts.

```powershell
.\Build-Tester-Releases.ps1 -Target Portable
.\Build-Tester-Releases.ps1 -Target All
# Optional explicit trusted compiler path:
.\Build-Tester-Releases.ps1 -Target Installer -InnoCompilerPath 'C:\Program Files\Inno Setup 7\ISCC.exe'
```

Each invocation creates a fresh `dist\v2-<timestamp>-<random>` folder. It contains a publish folder, the requested portable ZIP and/or Setup EXE, the audit report and SHA256SUMS.txt. No existing artifacts are deleted or reused. A failed audit/restore/publish/compiler invocation stops the build. Inspect the exit status and logs; a leftover folder is not evidence of success.

Release restore uses `packages.Release.lock.json`, explicit NuGet.org package/audit sources, and audits direct plus transitive dependencies. Audit-data failures and reported vulnerabilities block publishing. Debug builds use a separate lock file and may include development diagnostics; distribute only Release output. Do not bypass advisory errors or regenerate locks without reviewing the dependency changes.

Packages are self-contained, single-file, compressed, without ReadyToRun or aggressive trimming. Native components may extract at runtime. These settings favor smaller packages while retaining Avalonia functionality; size depends on the SDK/runtime. The source ZIP excludes all generated binaries and user data.

Build-Windows.ps1 delegates to the same audited Portable path. The installer definition requires the fresh publish directory passed by Build-Tester-Releases.ps1. Stale-output/SkipPublish and standalone GUI compilation paths have been removed.

Successful build output is not approval to distribute. Check the audit evidence and complete GUI, install/update/uninstall, startup and clean-machine validation in an isolated Windows account/VM before outside testing. Do not disable antivirus or SmartScreen. Signing and independent runtime/native-library checks remain the maintainer's responsibility.
