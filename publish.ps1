<#
.SYNOPSIS
  Builds, packages, and uploads a new ModelTimer release via Velopack.

.DESCRIPTION
  See PUBLISHING.md for one-time setup (token). Before running this:
    1. Bump <Version> in ModelTimer.csproj.
    2. Set $env:VELOPACK_PUBLISH_TOKEN to a token with write access to the repo (local-only,
       never committed, never embedded in the app - see PUBLISHING.md).

  RepoUrl must match the RepoUrl constant in AppUpdateService.cs.
#>

$ErrorActionPreference = "Stop"

$RepoUrl = "https://github.com/acelandingpages-crypto/modeltimer"  # keep in sync with AppUpdateService.cs
$PublishDir = "publish_temp"
$ReleasesDir = "releases"

if (-not $env:VELOPACK_PUBLISH_TOKEN) {
    Write-Error "Set `$env:VELOPACK_PUBLISH_TOKEN to the read-and-write GitHub token first (see PUBLISHING.md)."
}

[xml]$csproj = Get-Content "ModelTimer.csproj"
$version = $csproj.Project.PropertyGroup.Version | Select-Object -First 1
if (-not $version) {
    Write-Error "No <Version> found in ModelTimer.csproj."
}
Write-Host "Publishing ModelTimer v$version..."

if (Test-Path $PublishDir) { Remove-Item $PublishDir -Recurse -Force }

dotnet publish ModelTimer.csproj -c Release -r win-x64 --self-contained -o $PublishDir
if ($LASTEXITCODE -ne 0) { Write-Error "dotnet publish failed." }

vpk pack `
    --packId ModelTimer `
    --packVersion $version `
    --packDir $PublishDir `
    --mainExe ModelTimer.exe `
    --icon "Assets\favicon.ico" `
    --outputDir $ReleasesDir
if ($LASTEXITCODE -ne 0) { Write-Error "vpk pack failed." }

vpk upload github `
    --repoUrl $RepoUrl `
    --token $env:VELOPACK_PUBLISH_TOKEN `
    --outputDir $ReleasesDir `
    --publish
if ($LASTEXITCODE -ne 0) { Write-Error "vpk upload failed." }

Write-Host "Published v$version. Installed copies will offer to update next time they check."
