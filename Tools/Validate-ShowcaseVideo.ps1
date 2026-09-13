[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$VideoPath,
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot),
    [double]$ExpectedDurationSeconds = 65,
    [int]$ExpectedFrames = 3900
)

$ErrorActionPreference = 'Stop'
$video = (Resolve-Path -LiteralPath $VideoPath).Path
$validationRoot = Join-Path $ProjectRoot 'Artifacts/Validation/Showcase'
New-Item -ItemType Directory -Force -Path $validationRoot | Out-Null

$probeText = & ffprobe -v error -show_entries 'stream=codec_name,codec_type,width,height,pix_fmt,avg_frame_rate,nb_frames,sample_rate,channels,channel_layout,duration:format=duration,size,tags' -of json $video
if ($LASTEXITCODE -ne 0) { throw "ffprobe failed for $video" }
$probe = $probeText | ConvertFrom-Json
$videoStream = @($probe.streams | Where-Object codec_type -eq 'video')[0]
$audioStreams = @($probe.streams | Where-Object codec_type -eq 'audio')
$audioStream = $audioStreams | Select-Object -First 1

$loudnessText = (& ffmpeg -hide_banner -nostats -i $video -map '0:a:0' -af 'ebur128=peak=true' -f null - 2>&1) -join [Environment]::NewLine
if ($LASTEXITCODE -ne 0) { throw 'Could not measure showcase audio loudness.' }
$loudnessPattern = '(?s)Integrated loudness:\s+I:\s+(?<integrated>-?\d+(?:\.\d+)?) LUFS.*?True peak:\s+Peak:\s+(?<peak>-?\d+(?:\.\d+)?) dBFS'
$loudnessMatches = [regex]::Matches($loudnessText, $loudnessPattern)
if ($loudnessMatches.Count -eq 0) { throw 'Could not parse showcase audio loudness.' }
$loudnessMatch = $loudnessMatches[$loudnessMatches.Count - 1]
$integratedLufs = [double]::Parse($loudnessMatch.Groups['integrated'].Value, [Globalization.CultureInfo]::InvariantCulture)
$truePeakDbfs = [double]::Parse($loudnessMatch.Groups['peak'].Value, [Globalization.CultureInfo]::InvariantCulture)

& ffmpeg -hide_banner -loglevel error -i $video -f null -
$fullDecodePassed = $LASTEXITCODE -eq 0
if (-not $fullDecodePassed) { throw 'Full showcase video decode failed.' }

$contactSheet = Join-Path $validationRoot 'showcase-contact-sheet.png'
$selectFrames = 'eq(n,60)+eq(n,480)+eq(n,1080)+eq(n,1680)+eq(n,2280)+eq(n,2880)+eq(n,3480)+eq(n,3840)'
& ffmpeg -hide_banner -loglevel error -y -i $video -vf "select='$selectFrames',scale=480:270:flags=lanczos,tile=4x2" -frames:v 1 -update 1 $contactSheet
if ($LASTEXITCODE -ne 0) { throw 'Could not generate showcase contact sheet.' }

# Five checkpoints (1/3/5/7/9 seconds) inside each ten-second effect chapter.
$motionFrames = @(
    240, 360, 480, 600, 720,
    840, 960, 1080, 1200, 1320,
    1440, 1560, 1680, 1800, 1920,
    2040, 2160, 2280, 2400, 2520,
    2640, 2760, 2880, 3000, 3120,
    3240, 3360, 3480, 3600, 3720
)
$motionSelect = ($motionFrames | ForEach-Object { "eq(n,$_)" }) -join '+'
$motionContactSheet = Join-Path $validationRoot 'showcase-motion-contact-sheet.png'
& ffmpeg -hide_banner -loglevel error -y -i $video -vf "select='$motionSelect',scale=384:216:flags=lanczos,tile=5x6" -frames:v 1 -update 1 $motionContactSheet
if ($LASTEXITCODE -ne 0) { throw 'Could not generate showcase motion contact sheet.' }

$frameMd5Text = & ffmpeg -v error -i $video -map '0:v:0' -an -vf "select='$motionSelect'" -fps_mode vfr -f framemd5 -
if ($LASTEXITCODE -ne 0) { throw 'Could not hash showcase motion samples.' }
$motionHashes = @($frameMd5Text |
    Where-Object { $_ -and -not $_.StartsWith('#') } |
    ForEach-Object { ($_ -split ',')[-1].Trim() })
$uniqueMotionHashes = @($motionHashes | Sort-Object -Unique).Count

$duration = [double]$probe.format.duration
$checks = [ordered]@{
    codec = $videoStream.codec_name -eq 'h264'
    dimensions = [int]$videoStream.width -eq 1920 -and [int]$videoStream.height -eq 1080
    pixelFormat = $videoStream.pix_fmt -eq 'yuv420p'
    frameRate = $videoStream.avg_frame_rate -eq '60/1'
    frameCount = [int]$videoStream.nb_frames -eq $ExpectedFrames
    duration = [Math]::Abs($duration - $ExpectedDurationSeconds) -lt .001
    underSeventySeconds = $duration -le 70
    audioStreamCount = $audioStreams.Count -eq 1
    audioCodec = $audioStream.codec_name -eq 'aac'
    audioChannels = [int]$audioStream.channels -eq 2
    audioSampleRate = [int]$audioStream.sample_rate -eq 48000
    audioDuration = [Math]::Abs(([double]$audioStream.duration) - $ExpectedDurationSeconds) -lt .05
    audioIntegratedLufs = [Math]::Abs($integratedLufs - (-16.0)) -le 1.0
    audioTruePeak = $truePeakDbfs -le -1.0
    fullDecode = $fullDecodePassed
    contactSheet = Test-Path -LiteralPath $contactSheet
    motionContactSheet = Test-Path -LiteralPath $motionContactSheet
    motionSampleCount = $motionHashes.Count -eq $motionFrames.Count
    motionSamplesUnique = $uniqueMotionHashes -eq $motionFrames.Count
}
$passed = @($checks.Values | Where-Object { -not $_ }).Count -eq 0
$report = [ordered]@{
    generatedUtc = [DateTime]::UtcNow.ToString('O')
    video = $video
    bytes = (Get-Item -LiteralPath $video).Length
    sha256 = (Get-FileHash -LiteralPath $video -Algorithm SHA256).Hash.ToLowerInvariant()
    durationSeconds = $duration
    frames = [int]$videoStream.nb_frames
    codec = $videoStream.codec_name
    width = [int]$videoStream.width
    height = [int]$videoStream.height
    frameRate = $videoStream.avg_frame_rate
    pixelFormat = $videoStream.pix_fmt
    audioStreams = $audioStreams.Count
    audioCodec = $audioStream.codec_name
    audioChannels = [int]$audioStream.channels
    audioChannelLayout = $audioStream.channel_layout
    audioSampleRate = [int]$audioStream.sample_rate
    audioDurationSeconds = [double]$audioStream.duration
    audioIntegratedLufs = $integratedLufs
    audioTruePeakDbfs = $truePeakDbfs
    contactSheet = $contactSheet
    motionContactSheet = $motionContactSheet
    motionSampleCount = $motionHashes.Count
    uniqueMotionSampleCount = $uniqueMotionHashes
    checks = $checks
    passed = $passed
}
$reportPath = Join-Path $validationRoot 'showcase-video-validation.json'
$report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $reportPath -Encoding utf8NoBOM
$report | ConvertTo-Json -Depth 6
if (-not $passed) { throw "Showcase validation failed. See $reportPath" }
