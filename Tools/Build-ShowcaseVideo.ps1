[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$RunRoot,
    [string]$OutputFile,
    [string]$MusicFile = (Join-Path (Split-Path -Parent $PSScriptRoot) 'Artifacts/SourceAudio/Mixkit-Digital-Clouds-175.mp3'),
    [int]$FrameRate = 60,
    [int]$Width = 1920,
    [int]$Height = 1080,
    [double]$MusicTargetLufs = -16.0,
    [double]$MusicTruePeakDb = -1.5
)

$ErrorActionPreference = 'Stop'
$run = (Resolve-Path -LiteralPath $RunRoot).Path
$capturesRoot = Split-Path -Parent (Split-Path -Parent $run)
if (-not $OutputFile) {
    $OutputFile = Join-Path $capturesRoot 'Videos/ShaderGraphTechniques-YouTube-Showcase.mp4'
}
$output = [IO.Path]::GetFullPath($OutputFile)
$outputDirectory = Split-Path -Parent $output
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
if (-not (Test-Path -LiteralPath $MusicFile)) {
    throw "Missing showcase music: $MusicFile. Download Digital Clouds from https://mixkit.co/free-stock-music/electronic/"
}
$music = (Resolve-Path -LiteralPath $MusicFile).Path

$segmentsRoot = Join-Path $run 'Segments'
$segments = @(
    '00_Gallery.mp4',
    '01_Dissolve.mp4',
    '02_IntersectionShield.mp4',
    '03_HeatHaze.mp4',
    '04_TriplanarSnow.mp4',
    '05_InteractiveGrass.mp4',
    '06_ScanPulse.mp4'
) | ForEach-Object { Join-Path $segmentsRoot $_ }
foreach ($segment in $segments) {
    if (-not (Test-Path -LiteralPath $segment)) { throw "Missing showcase segment: $segment" }
}

$fontRegularPath = 'C:\Windows\Fonts\segoeui.ttf'
$fontBoldPath = 'C:\Windows\Fonts\segoeuib.ttf'
$fontJapaneseRegularPath = 'C:\Windows\Fonts\YuGothM.ttc'
$fontJapaneseBoldPath = 'C:\Windows\Fonts\YuGothB.ttc'
foreach ($font in $fontRegularPath, $fontBoldPath, $fontJapaneseRegularPath, $fontJapaneseBoldPath) {
    if (-not (Test-Path -LiteralPath $font)) { throw "Required title font not found: $font" }
}
$fontRegular = $fontRegularPath.Replace('\', '/').Replace(':', '\:')
$fontBold = $fontBoldPath.Replace('\', '/').Replace(':', '\:')
$fontJapaneseRegular = $fontJapaneseRegularPath.Replace('\', '/').Replace(':', '\:')
$fontJapaneseBold = $fontJapaneseBoldPath.Replace('\', '/').Replace(':', '\:')

$chapters = @(
    [ordered]@{ Number = '01 / 06'; Title = 'DISSOLVE'; Subtitle = 'NOISE MASK + EMISSIVE EDGE'; Caption = 'ノイズの境界で物体を消しながら 縁だけを発光させる' },
    [ordered]@{ Number = '02 / 06'; Title = 'INTERSECTION SHIELD'; Subtitle = 'DEPTH CONTACT + FRESNEL'; Caption = 'バリアを物体が横切ると 交差した部分だけがシアンに光る' },
    [ordered]@{ Number = '03 / 06'; Title = 'HEAT HAZE'; Subtitle = 'OPAQUE TEXTURE DISTORTION'; Caption = '背景の読み取り位置をずらして 熱による空気の揺らぎを作る' },
    [ordered]@{ Number = '04 / 06'; Title = 'TRIPLANAR SNOW'; Subtitle = 'WORLD PROJECTION + UP MASK'; Caption = 'ワールド座標で模様を投影し 上向きの面に雪を積もらせる' },
    [ordered]@{ Number = '05 / 06'; Title = 'INTERACTIVE GRASS'; Subtitle = 'WIND + CHARACTER BENDING'; Caption = '根元を固定した草が 風と接近する物体に反応して曲がる' },
    [ordered]@{ Number = '06 / 06'; Title = 'SCAN PULSE'; Subtitle = 'ONE WAVE ACROSS MANY SURFACES'; Caption = 'ワールド空間の距離を使い 同じ光の波を床から壁へ走らせる' }
)

$filters = [System.Collections.Generic.List[string]]::new()
$filters.Add("[0:v]fps=$FrameRate,scale=$Width`:$Height`:flags=lanczos,format=yuv420p,split=2[galleryintro][galleryoutro]")
$filters.Add("[galleryintro]trim=duration=3,setpts=PTS-STARTPTS,drawbox=x=0:y=0:w=iw:h=ih:color=0x020817@0.72:t=fill,drawbox=x=220:y=238:w=1480:h=3:color=0x36d9ff@0.85:t=fill,drawtext=fontfile='$fontBold':text='UNITY 6.3 SHADER GRAPH':fontcolor=white:fontsize=96:x=(w-text_w)/2:y=330,drawtext=fontfile='$fontRegular':text='SIX REUSABLE TECHNIQUES':fontcolor=0x4ee5ff:fontsize=49:x=(w-text_w)/2:y=470,drawtext=fontfile='$fontRegular':text='URP 17.3  -  REAL-TIME SHOWCASE':fontcolor=0xb9c9df:fontsize=28:x=(w-text_w)/2:y=555,fade=t=in:st=0:d=0.35,fade=t=out:st=2.65:d=0.35[intro]")

for ($index = 0; $index -lt $chapters.Count; $index++) {
    $input = $index + 1
    $chapter = $chapters[$index]
    $outputLabel = "effect$($index + 1)"
    $filters.Add("[$input`:v]fps=$FrameRate,scale=$Width`:$Height`:flags=lanczos,format=yuv420p,setpts=PTS-STARTPTS,drawbox=x=0:y=0:w=iw:h=ih:color=0x020817@0.78:t=fill:enable='lt(t,1.2)',drawbox=x=0:y=0:w=iw:h=8:color=0x38d9ff@0.95:t=fill:enable='lt(t,1.2)',drawtext=fontfile='$fontRegular':text='$($chapter.Number)':fontcolor=0x6ee9ff:fontsize=34:x=(w-text_w)/2:y=310:enable='lt(t,1.2)',drawtext=fontfile='$fontBold':text='$($chapter.Title)':fontcolor=white:fontsize=82:x=(w-text_w)/2:y=380:enable='lt(t,1.2)',drawtext=fontfile='$fontRegular':text='$($chapter.Subtitle)':fontcolor=0xa9bed6:fontsize=30:x=(w-text_w)/2:y=500:enable='lt(t,1.2)',drawtext=fontfile='$fontJapaneseBold':text='$($chapter.Caption)':fontcolor=0xe9f6ff:fontsize=34:x=(w-text_w)/2:y=580:enable='lt(t,1.2)',drawbox=x=42:y=38:w=680:h=104:color=0x020817@0.64:t=fill:enable='gte(t,1.2)',drawbox=x=42:y=38:w=8:h=104:color=0x3edcff@0.95:t=fill:enable='gte(t,1.2)',drawtext=fontfile='$fontBold':text='$($chapter.Number)  $($chapter.Title)':fontcolor=white:fontsize=36:x=72:y=55:enable='gte(t,1.2)',drawtext=fontfile='$fontRegular':text='$($chapter.Subtitle)':fontcolor=0x8fe8ff:fontsize=21:x=73:y=103:enable='gte(t,1.2)',drawbox=x=150:y=h-150:w=1620:h=96:color=0x020817@0.78:t=fill:enable='gte(t,1.2)',drawbox=x=150:y=h-150:w=7:h=96:color=0x3edcff@0.95:t=fill:enable='gte(t,1.2)',drawtext=fontfile='$fontJapaneseRegular':text='$($chapter.Caption)':fontcolor=white:fontsize=31:x=(w-text_w)/2:y=h-124:enable='gte(t,1.2)',drawbox=x=0:y=h-10:w=iw*t/10:h=10:color=0x3edcff@0.9:t=fill,fade=t=in:st=0:d=0.18,fade=t=out:st=9.7:d=0.3[$outputLabel]")
}

$filters.Add("[galleryoutro]trim=start=1:duration=2,setpts=PTS-STARTPTS,drawbox=x=0:y=0:w=iw:h=ih:color=0x020817@0.76:t=fill,drawtext=fontfile='$fontBold':text='EDITABLE. PORTABLE. VERIFIED.':fontcolor=white:fontsize=72:x=(w-text_w)/2:y=405,drawtext=fontfile='$fontRegular':text='6 SHADER GRAPHS  -  1 UNITY PROJECT':fontcolor=0x4ee5ff:fontsize=34:x=(w-text_w)/2:y=515,fade=t=in:st=0:d=0.3,fade=t=out:st=1.6:d=0.4[outro]")
$filters.Add('[intro][effect1][effect2][effect3][effect4][effect5][effect6][outro]concat=n=8:v=1:a=0[outv]')
$targetLufs = $MusicTargetLufs.ToString('0.0', [Globalization.CultureInfo]::InvariantCulture)
$truePeak = $MusicTruePeakDb.ToString('0.0', [Globalization.CultureInfo]::InvariantCulture)
$filters.Add("[7:a]atrim=start=0:duration=65,asetpts=PTS-STARTPTS,loudnorm=I=$targetLufs`:LRA=7`:TP=$truePeak,afade=t=in`:st=0`:d=0.8,afade=t=out`:st=62`:d=3,aresample=48000,aformat=sample_fmts=fltp`:channel_layouts=stereo[outa]")
$filterComplex = $filters -join ';'

$arguments = @('-y')
foreach ($segment in $segments) { $arguments += @('-i', $segment) }
$arguments += @('-i', $music)
$arguments += @(
    '-filter_complex', $filterComplex,
    '-map', '[outv]',
    '-map', '[outa]',
    '-r', $FrameRate,
    '-c:v', 'libx264',
    '-preset', 'slow',
    '-crf', '17',
    '-pix_fmt', 'yuv420p',
    '-c:a', 'aac',
    '-b:a', '192k',
    '-ar', '48000',
    '-ac', '2',
    '-movflags', '+faststart',
    '-metadata', 'title=Unity 6.3 Shader Graph - Six Reusable Techniques',
    '-metadata', 'comment=Captured from Unity 6000.3.22f1. Music: Digital Clouds by Alejandro Magana (A. M.), Mixkit Stock Music Free License.',
    $output
)

& ffmpeg @arguments
if ($LASTEXITCODE -ne 0) { throw "ffmpeg showcase build failed with exit code $LASTEXITCODE" }
Write-Host "Showcase video: $output"
