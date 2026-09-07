param(
    [string]$ModelDirectory = 'C:\WhisperTool\models',
    [switch]$Force
)
$ErrorActionPreference = 'Stop'

function Test-VadModel([string]$Path) {
    $reader = [IO.BinaryReader]::new([IO.File]::OpenRead($Path))
    try {
        return ($reader.BaseStream.Length -ge 1024 -and
            $reader.ReadUInt32() -eq 0x67676d6c -and
            $reader.ReadInt32() -eq 10 -and
            [Text.Encoding]::ASCII.GetString($reader.ReadBytes(10)) -eq 'silero-16k')
    } finally { $reader.Dispose() }
}

$directory = [IO.Path]::GetFullPath($ModelDirectory)
[IO.Directory]::CreateDirectory($directory) | Out-Null
$destination = Join-Path $directory 'ggml-silero-v6.2.0.bin'
if ((Test-Path -LiteralPath $destination) -and !$Force) {
    if (!(Test-VadModel $destination)) { throw 'Invalid VAD model. Run setup-vad.ps1 -Force to replace it.' }
    Write-Host "VAD model already installed: $destination"
    return
}
$temporary = Join-Path $directory ('.vad-' + [Guid]::NewGuid().ToString('N') + '.tmp')
try {
    Invoke-WebRequest -Uri 'https://huggingface.co/ggml-org/whisper-vad/resolve/main/ggml-silero-v6.2.0.bin' -OutFile $temporary -UseBasicParsing -TimeoutSec 120
    if (!(Test-VadModel $temporary)) { throw 'Downloaded file is not a valid Silero VAD model.' }
    if (Test-Path -LiteralPath $destination) {
        [IO.File]::Replace($temporary, $destination, $null)
    } else {
        [IO.File]::Move($temporary, $destination)
    }
    Write-Host "VAD model installed: $destination" -ForegroundColor Green
} finally {
    if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary }
}
