param([string]$OutputDirectory = (Join-Path $PSScriptRoot 'dist'))
$ErrorActionPreference = 'Stop'
$version = ([xml](Get-Content -LiteralPath (Join-Path $PSScriptRoot 'WhisperDesk.csproj') -Raw)).Project.PropertyGroup.Version
$name = "WhisperDesk-v$version-win-x64"
$output = [IO.Path]::GetFullPath($OutputDirectory)
$package = Join-Path $output $name
if (Test-Path -LiteralPath $package) { throw "Package folder already exists: $package. Use a fresh output directory." }
$files = @('publish\WhisperDesk.exe','GETTING_STARTED.md','README.md','RELEASE_NOTES.md','LICENSE','THIRD_PARTY_NOTICES.md','setup-vad.ps1','setup-ffmpeg.ps1')
foreach ($file in $files) {
    if (!(Test-Path -LiteralPath (Join-Path $PSScriptRoot $file))) { throw "Missing package input: $file" }
}
$exe = Get-Item -LiteralPath (Join-Path $PSScriptRoot 'publish\WhisperDesk.exe')
if (!$exe.VersionInfo.ProductVersion.StartsWith("$version")) { throw 'Build the current version before packaging.' }
New-Item -ItemType Directory -Path $package -Force | Out-Null
foreach ($file in $files) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $file) -Destination (Join-Path $package ([IO.Path]::GetFileName($file)))
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'licenses') -Destination (Join-Path $package 'licenses') -Recurse
$zip = Join-Path $output ($name + '.zip')
if (Test-Path -LiteralPath $zip) { throw "Archive already exists: $zip" }
Compress-Archive -LiteralPath $package -DestinationPath $zip -CompressionLevel Optimal
$hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText((Join-Path $output 'SHA256SUMS.txt'), "$hash  $name.zip`n", [Text.Encoding]::ASCII)
Write-Host "Release package: $zip"
Write-Host "SHA256: $hash"
