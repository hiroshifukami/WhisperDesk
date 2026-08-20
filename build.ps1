$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

dotnet restore
if ($LASTEXITCODE -ne 0) {
  throw "dotnet restore failed (exit code: $LASTEXITCODE)"
}

dotnet publish .\WhisperDesk.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o .\publish
if ($LASTEXITCODE -ne 0) {
  throw "dotnet publish failed (exit code: $LASTEXITCODE)"
}

Write-Host ""
Write-Host "Build complete: $PSScriptRoot\publish\WhisperDesk.exe" -ForegroundColor Green
