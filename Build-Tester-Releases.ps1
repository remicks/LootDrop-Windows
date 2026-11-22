#Requires -Version 7.2
# PowerShell 7.2+ supplies standard ZIP SmallestSize compression (.NET 6+).
# Creates fresh version 2 release artifacts. Does not install, launch, sign, or upload them.
[CmdletBinding()]
param(
    [ValidateSet('All','Portable','Installer')][string]$Target = 'All',
    [string]$InnoCompilerPath = ''
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Set-Location -LiteralPath $PSScriptRoot
$project = Join-Path $PSScriptRoot 'LootDrop.csproj'
$dist = Join-Path $PSScriptRoot 'dist'
# Avoid following a pre-existing junction into an unrelated project or user-data folder.
foreach ($folder in @($PSScriptRoot,$dist)) {
    if ((Test-Path -LiteralPath $folder) -and ((Get-Item -LiteralPath $folder -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw "Linked build folders are not supported: $folder"
    }
}
$iscc = $null
if ($Target -in @('All','Installer')) {
    if ([string]::IsNullOrWhiteSpace($InnoCompilerPath)) {
        foreach ($base in @(${env:ProgramFiles(x86)},$env:ProgramFiles)) {
            foreach ($version in @('Inno Setup 6','Inno Setup 7')) {
                $candidate = Join-Path (Join-Path $base $version) 'ISCC.exe'
                if (Test-Path -LiteralPath $candidate -PathType Leaf) { $iscc = $candidate; break }
            }
            if ($iscc) { break }
        }
    } else { $iscc = (Resolve-Path -LiteralPath $InnoCompilerPath).ProviderPath }
    if (-not $iscc) { throw 'Install Inno Setup from its official source, or supply -InnoCompilerPath. Nothing is downloaded automatically.' }
}
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw '.NET 10 SDK required.' }
if (-not (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'packages.Release.lock.json'))) { throw 'Release dependency lock file is required.' }
$run = Join-Path $dist ('v2-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0,8))
$publish = Join-Path $run 'publish'
New-Item -ItemType Directory -Path $run | Out-Null
# Restore must refresh advisory data; all audit failures and advisories are errors in the project.
& dotnet restore $project -r win-x64 -p:Configuration=Release --locked-mode --force --no-http-cache
if ($LASTEXITCODE -ne 0) { throw 'Locked restore / vulnerability audit failed. No release created.' }
& dotnet publish $project -c Release -r win-x64 --self-contained true --no-restore `
    -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=false `
    -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishReadyToRun=false `
    -p:PublishTrimmed=false -p:DebugType=none -p:DebugSymbols=false -o $publish
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
if (-not (Test-Path -LiteralPath (Join-Path $publish 'LootDrop.exe'))) { throw 'Published executable missing.' }
# Native dependency packages can copy over 100 MiB of developer symbols despite DebugSymbols=false.
# They are not runtime inputs. Remove only symbol files in this newly created publish directory.
foreach ($symbols in Get-ChildItem -LiteralPath $publish -Filter '*.pdb' -File) {
    Remove-Item -LiteralPath $symbols.FullName -Force
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'SECURITY-AUDIT.md') -Destination $run
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'SECURITY-RECHECK-2026-11-18.md') -Destination $run
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'RELEASE-0.7.11.md') -Destination $run
if ($Target -in @('All','Portable')) {
    $portable = Join-Path $run 'LootDrop-Portable-win-x64.zip'
    [IO.Compression.ZipFile]::CreateFromDirectory($publish, $portable, [IO.Compression.CompressionLevel]::SmallestSize, $false)
}
if ($Target -in @('All','Installer')) {
    & $iscc "/O$run" "/DPublishDir=$publish" (Join-Path $PSScriptRoot 'LootDrop-Installer.iss')
    if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
    if (-not (Test-Path -LiteralPath (Join-Path $run 'LootDrop-Setup.exe'))) { throw 'Installer missing.' }
}
Get-ChildItem -LiteralPath $run -File | Where-Object Extension -in @('.exe','.zip') |
    Get-FileHash -Algorithm SHA256 | ForEach-Object { "$($_.Hash)  $([IO.Path]::GetFileName($_.Path))" } |
    Set-Content -LiteralPath (Join-Path $run 'SHA256SUMS.txt')
Write-Host "Artifacts: $run"
Write-Host 'Build completed. This is not distribution approval; follow SECURITY-AUDIT.md before sharing.'





