# Use the same audited, self-contained release path for every release entry point.
[CmdletBinding()]
param()
& (Join-Path $PSScriptRoot 'Build-Tester-Releases.ps1') -Target Portable
