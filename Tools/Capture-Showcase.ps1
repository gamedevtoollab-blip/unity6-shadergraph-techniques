[CmdletBinding()]
param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$RunName = 'YouTubeShowcase-v1',
    [int]$FrameRate = 60,
    [int]$Width = 1920,
    [int]$Height = 1080,
    [double]$SecondsPerEffect = 10,
    [double]$GallerySeconds = 3
)

$ErrorActionPreference = 'Stop'
$project = (Resolve-Path -LiteralPath $ProjectRoot).Path
$player = Join-Path $project 'Artifacts/Player/Windows/ShaderGraphTechniques.exe'
if (-not (Test-Path -LiteralPath $player)) { throw "Player not found: $player" }
if ($FrameRate -lt 1 -or $Width -lt 1 -or $Height -lt 1) { throw 'Frame rate and dimensions must be positive.' }

$captureRoot = Join-Path $project 'Artifacts/Captures/Showcase'
$runRoot = Join-Path $captureRoot $RunName
if (Test-Path -LiteralPath $runRoot) { throw "Run folder already exists; choose a new RunName: $runRoot" }
$framesRoot = Join-Path $runRoot 'Frames'
$segmentsRoot = Join-Path $runRoot 'Segments'
$logsRoot = Join-Path $runRoot 'Logs'
New-Item -ItemType Directory -Force -Path $framesRoot, $segmentsRoot, $logsRoot | Out-Null

$captures = @(
    [ordered]@{ Scene = '00_Gallery'; Slug = '00_Gallery'; Seconds = $GallerySeconds },
    [ordered]@{ Scene = '01_Dissolve'; Slug = '01_Dissolve'; Seconds = $SecondsPerEffect },
    [ordered]@{ Scene = '02_IntersectionShield'; Slug = '02_IntersectionShield'; Seconds = $SecondsPerEffect },
    [ordered]@{ Scene = '03_HeatHaze'; Slug = '03_HeatHaze'; Seconds = $SecondsPerEffect },
    [ordered]@{ Scene = '04_TriplanarSnow'; Slug = '04_TriplanarSnow'; Seconds = $SecondsPerEffect },
    [ordered]@{ Scene = '05_InteractiveGrass'; Slug = '05_InteractiveGrass'; Seconds = $SecondsPerEffect },
    [ordered]@{ Scene = '06_ScanPulse'; Slug = '06_ScanPulse'; Seconds = $SecondsPerEffect }
)

$results = foreach ($capture in $captures) {
    $frameCount = [int][Math]::Round([double]$capture.Seconds * $FrameRate)
    $frameFolder = Join-Path $framesRoot $capture.Slug
    $logPath = Join-Path $logsRoot ($capture.Slug + '.log')
    $encodeLogPath = Join-Path $logsRoot ($capture.Slug + '-encode.log')
    $segmentPath = Join-Path $segmentsRoot ($capture.Slug + '.mp4')
    New-Item -ItemType Directory -Force -Path $frameFolder | Out-Null

    $arguments = @(
        '-captureSequence',
        '-captureScene', $capture.Scene,
        '-captureOutput', $frameFolder,
        '-captureFrames', $frameCount,
        '-captureFps', $FrameRate,
        '-captureWidth', $Width,
        '-captureHeight', $Height,
        '-captureHideUi',
        '-showcaseMotion',
        '-screen-fullscreen', '0',
        '-logFile', $logPath
    )

    Write-Host "Capturing $($capture.Scene): $frameCount frames"
    $process = Start-Process -FilePath $player -ArgumentList $arguments -PassThru -Wait
    if ($process.ExitCode -ne 0) { throw "Player capture failed for $($capture.Scene): exit $($process.ExitCode)" }

    $metadataPath = Join-Path $frameFolder 'capture-complete.txt'
    if (-not (Test-Path -LiteralPath $metadataPath)) { throw "Capture metadata missing: $metadataPath" }
    $frames = @(Get-ChildItem -LiteralPath $frameFolder -Filter "$($capture.Scene)_*.png" -File | Sort-Object Name)
    if ($frames.Count -ne $frameCount) { throw "Expected $frameCount frames for $($capture.Scene), got $($frames.Count)." }

    $inputPattern = Join-Path $frameFolder ($capture.Scene + '_%04d.png')
    & (Join-Path $PSScriptRoot 'Encode-Capture.ps1') -InputPattern $inputPattern -OutputFile $segmentPath -FrameRate $FrameRate *> $encodeLogPath

    [ordered]@{
        scene = $capture.Scene
        seconds = [double]$capture.Seconds
        frames = $frameCount
        frameFolder = $frameFolder
        playerLog = $logPath
        encodeLog = $encodeLogPath
        segment = $segmentPath
        segmentBytes = (Get-Item -LiteralPath $segmentPath).Length
        segmentSha256 = (Get-FileHash -LiteralPath $segmentPath -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}

$manifest = [ordered]@{
    generatedUtc = [DateTime]::UtcNow.ToString('O')
    player = $player
    runName = $RunName
    frameRate = $FrameRate
    width = $Width
    height = $Height
    showcaseMotion = $true
    captures = @($results)
}
$manifestPath = Join-Path $runRoot 'capture-manifest.json'
$manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $manifestPath -Encoding utf8NoBOM
& (Join-Path $PSScriptRoot 'Validate-CameraLock.ps1') -ProjectRoot $project -RunRoot $runRoot
Write-Host "Showcase capture complete: $manifestPath"
