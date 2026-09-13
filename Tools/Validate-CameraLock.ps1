[CmdletBinding()]
param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot),
    [Parameter(Mandatory)]
    [string]$RunRoot
)

$ErrorActionPreference = 'Stop'
$project = (Resolve-Path -LiteralPath $ProjectRoot).Path
$resolvedRunRoot = if ([IO.Path]::IsPathRooted($RunRoot)) {
    (Resolve-Path -LiteralPath $RunRoot).Path
} else {
    (Resolve-Path -LiteralPath (Join-Path $project $RunRoot)).Path
}
$manifestPath = Join-Path $resolvedRunRoot 'capture-manifest.json'
if (-not (Test-Path -LiteralPath $manifestPath)) { throw "Capture manifest not found: $manifestPath" }

$manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
$poseFields = @(
    'positionX', 'positionY', 'positionZ',
    'rotationX', 'rotationY', 'rotationZ', 'rotationW',
    'fieldOfView'
)

$captures = @(
    foreach ($capture in $manifest.captures) {
        $tracePath = Join-Path ([string]$capture.frameFolder) 'camera-trace.csv'
        $traceExists = Test-Path -LiteralPath $tracePath
        $rows = if ($traceExists) { @(Import-Csv -LiteralPath $tracePath) } else { @() }
        $completeRows = @($rows | Where-Object {
            $row = $_
            -not @($poseFields | Where-Object { [string]::IsNullOrWhiteSpace([string]$row.$_) }).Count
        })
        $uniquePoses = if ($rows.Count -gt 0) {
            @($rows | Select-Object -Property $poseFields -Unique)
        } else {
            @()
        }
        $rowCountMatches = $rows.Count -eq [int]$capture.frames
        $allRowsComplete = $completeRows.Count -eq $rows.Count
        $cameraIsLocked = $uniquePoses.Count -eq 1

        [ordered]@{
            scene = [string]$capture.scene
            trace = $tracePath
            expectedFrames = [int]$capture.frames
            traceRows = $rows.Count
            uniqueCameraPoses = $uniquePoses.Count
            rowCountMatches = $rowCountMatches
            allRowsComplete = $allRowsComplete
            cameraIsLocked = $cameraIsLocked
            passed = $traceExists -and $rowCountMatches -and $allRowsComplete -and $cameraIsLocked
        }
    }
)

$report = [ordered]@{
    generatedUtc = [DateTime]::UtcNow.ToString('O')
    runRoot = $resolvedRunRoot
    expectedCaptures = @($manifest.captures).Count
    validatedCaptures = $captures.Count
    totalTraceRows = [int](($captures | ForEach-Object { $_.traceRows } | Measure-Object -Sum).Sum)
    captures = $captures
    passed = $captures.Count -eq @($manifest.captures).Count -and -not @($captures | Where-Object { -not $_.passed }).Count
}

$validationRoot = Join-Path $project 'Artifacts/Validation/Showcase'
New-Item -ItemType Directory -Force -Path $validationRoot | Out-Null
$outputPath = Join-Path $validationRoot 'camera-lock-validation.json'
$report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $outputPath -Encoding utf8NoBOM
if (-not $report.passed) { throw "Camera lock validation failed. See $outputPath" }
Write-Output "PASS: $($report.validatedCaptures) captures, $($report.totalTraceRows) frames, one camera pose per scene."
