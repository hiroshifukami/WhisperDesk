$ErrorActionPreference = "Stop"

if (Get-Command ffmpeg.exe -ErrorAction SilentlyContinue) {
    Write-Host "FFmpeg is already available." -ForegroundColor Green
    ffmpeg.exe -version | Select-Object -First 1
    exit 0
}

winget install --id Gyan.FFmpeg -e --source winget
Write-Host "FFmpegを導入しました。PowerShellとWhisperDeskを開き直してください。" -ForegroundColor Green
