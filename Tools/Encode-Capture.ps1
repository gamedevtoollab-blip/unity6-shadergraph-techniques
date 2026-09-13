param(
    [Parameter(Mandatory = $true)][string]$InputPattern,
    [Parameter(Mandatory = $true)][string]$OutputFile,
    [int]$FrameRate = 60
)

$ErrorActionPreference = 'Stop'
ffmpeg -y -framerate $FrameRate -i $InputPattern -c:v libx264 -preset slow -crf 18 -pix_fmt yuv420p -movflags +faststart $OutputFile
if ($LASTEXITCODE -ne 0) { throw "ffmpeg failed with exit code $LASTEXITCODE" }
