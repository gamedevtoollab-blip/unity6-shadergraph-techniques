param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'

$captures = @(
    [ordered]@{ Name = 'Gallery Perspective'; Folder = 'Gallery-Perspective-v2'; Prefix = '00_Gallery'; Video = '00_Gallery-Perspective.mp4'; Frames = 180; Log = 'player-gallery-v2.log' },
    [ordered]@{ Name = 'Dissolve Perspective'; Folder = '01_Dissolve-Perspective'; Prefix = '01_Dissolve'; Video = '01_Dissolve-Perspective.mp4'; Frames = 180; Log = 'player-01-dissolve.log' },
    [ordered]@{ Name = 'Intersection Shield Perspective'; Folder = '02_IntersectionShield-Perspective'; Prefix = '02_IntersectionShield'; Video = '02_IntersectionShield-Perspective.mp4'; Frames = 180; Log = 'player-02-shield.log' },
    [ordered]@{ Name = 'Heat Haze Perspective'; Folder = '03_HeatHaze-Perspective-final-visible'; Prefix = '03_HeatHaze'; Video = '03_HeatHaze-Perspective.mp4'; Frames = 180; Log = 'player-03-heat-final-visible.log' },
    [ordered]@{ Name = 'Triplanar Snow Perspective'; Folder = '04_TriplanarSnow-Perspective'; Prefix = '04_TriplanarSnow'; Video = '04_TriplanarSnow-Perspective.mp4'; Frames = 180; Log = 'player-04-snow.log' },
    [ordered]@{ Name = 'Interactive Grass Perspective'; Folder = '05_InteractiveGrass-Perspective'; Prefix = '05_InteractiveGrass'; Video = '05_InteractiveGrass-Perspective.mp4'; Frames = 180; Log = 'player-05-grass.log' },
    [ordered]@{ Name = 'Scan Pulse Perspective'; Folder = '06_ScanPulse-Perspective'; Prefix = '06_ScanPulse'; Video = '06_ScanPulse-Perspective.mp4'; Frames = 180; Log = 'player-06-scan.log' },
    [ordered]@{ Name = 'Intersection Shield Orthographic'; Folder = '02_IntersectionShield-Orthographic-visible'; Prefix = '02_IntersectionShield'; Video = '02_IntersectionShield-Orthographic.mp4'; Frames = 120; Log = 'player-02-shield-orthographic-visible.log' },
    [ordered]@{ Name = 'Heat Haze Orthographic'; Folder = '03_HeatHaze-Orthographic-final'; Prefix = '03_HeatHaze'; Video = '03_HeatHaze-Orthographic.mp4'; Frames = 120; Log = 'player-03-heat-orthographic-final.log' }
)

$frameRoot = Join-Path $ProjectRoot 'Artifacts/Captures/Frames'
$videoRoot = Join-Path $ProjectRoot 'Artifacts/Captures/Videos'
$validationRoot = Join-Path $ProjectRoot 'Artifacts/Validation'
New-Item -ItemType Directory -Force -Path $validationRoot | Out-Null

$results = foreach ($capture in $captures) {
    $folder = Join-Path $frameRoot $capture.Folder
    $video = Join-Path $videoRoot $capture.Video
    $log = Join-Path $validationRoot $capture.Log
    $frameFiles = @(Get-ChildItem -LiteralPath $folder -Filter "$($capture.Prefix)_*.png" -File -ErrorAction Stop | Sort-Object Name)
    $probeText = & ffprobe -v error -select_streams v:0 -show_entries 'stream=codec_name,width,height,pix_fmt,avg_frame_rate,nb_frames:format=duration,size' -of json $video
    if ($LASTEXITCODE -ne 0) { throw "ffprobe failed for $video" }
    $probe = $probeText | ConvertFrom-Json
    $stream = $probe.streams[0]
    $captureMetadata = Get-Content -LiteralPath (Join-Path $folder 'capture-complete.txt') -Raw -Encoding UTF8
    $logText = Get-Content -LiteralPath $log -Raw -Encoding UTF8
    $fatalLogMatches = @([regex]::Matches($logText, '(?im)^.*(?:NullReferenceException|Shader error|Assertion failed|Crash!!!).*$') | ForEach-Object Value)
    $expectedDuration = $capture.Frames / 60.0

    $checks = [ordered]@{
        frameCount = $frameFiles.Count -eq $capture.Frames
        captureMetadata = $captureMetadata -match "(?m)^width=1920$" -and
            $captureMetadata -match "(?m)^height=1080$" -and
            $captureMetadata -match "(?m)^fps=60$" -and
            $captureMetadata -match "(?m)^frames=$($capture.Frames)$" -and
            $captureMetadata -match "(?m)^fixedStep=true$"
        codec = $stream.codec_name -eq 'h264'
        dimensions = [int]$stream.width -eq 1920 -and [int]$stream.height -eq 1080
        pixelFormat = $stream.pix_fmt -eq 'yuv420p'
        frameRate = $stream.avg_frame_rate -eq '60/1'
        encodedFrameCount = [int]$stream.nb_frames -eq $capture.Frames
        duration = [math]::Abs([double]$probe.format.duration - $expectedDuration) -lt 0.001
        playerLog = Test-Path -LiteralPath $log
        noFatalLogMatches = $fatalLogMatches.Count -eq 0
    }

    [ordered]@{
        name = $capture.Name
        frameFolder = $folder
        video = $video
        frameCount = $frameFiles.Count
        expectedFrameCount = $capture.Frames
        durationSeconds = [double]$probe.format.duration
        codec = $stream.codec_name
        width = [int]$stream.width
        height = [int]$stream.height
        pixelFormat = $stream.pix_fmt
        averageFrameRate = $stream.avg_frame_rate
        videoBytes = [long]$probe.format.size
        videoSha256 = (Get-FileHash -LiteralPath $video -Algorithm SHA256).Hash
        firstFrameSha256 = (Get-FileHash -LiteralPath $frameFiles[0].FullName -Algorithm SHA256).Hash
        middleFrameSha256 = (Get-FileHash -LiteralPath $frameFiles[[math]::Floor($frameFiles.Count / 2)].FullName -Algorithm SHA256).Hash
        lastFrameSha256 = (Get-FileHash -LiteralPath $frameFiles[-1].FullName -Algorithm SHA256).Hash
        log = $log
        fatalLogMatches = $fatalLogMatches
        checks = $checks
        passed = @($checks.Values | Where-Object { -not $_ }).Count -eq 0
    }
}

$report = [ordered]@{
    generatedUtc = [DateTime]::UtcNow.ToString('O')
    note = 'Fixed-step 60 fps capture proves deterministic output cadence; it is not a real-time performance benchmark.'
    allPassed = @($results | Where-Object { -not $_.passed }).Count -eq 0
    captures = @($results)
}

$jsonPath = Join-Path $validationRoot 'media-validation.json'
$markdownPath = Join-Path $validationRoot 'media-validation.md'
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $jsonPath -Encoding utf8NoBOM

$lines = @(
    '# Media validation',
    '',
    "- Generated (UTC): $($report.generatedUtc)",
    "- Overall: **$(if ($report.allPassed) { 'PASS' } else { 'FAIL' })**",
    '- Scope: deterministic fixed-step capture and encoded-file integrity; not a real-time frame-rate benchmark.',
    '',
    '| Capture | Frames | Duration | Video | Result |',
    '|---|---:|---:|---|---|'
)
foreach ($result in $results) {
    $lines += "| $($result.name) | $($result.frameCount) | $($result.durationSeconds.ToString('0.000')) s | $([IO.Path]::GetFileName($result.video)) | $(if ($result.passed) { 'PASS' } else { 'FAIL' }) |"
}
$lines += @('', 'Full hashes and per-check results are recorded in `media-validation.json`.')
$lines | Set-Content -LiteralPath $markdownPath -Encoding utf8NoBOM

if (-not $report.allPassed) { throw "Media validation failed. See $jsonPath" }
Write-Output "PASS: $($results.Count) captures validated"
Write-Output $jsonPath
