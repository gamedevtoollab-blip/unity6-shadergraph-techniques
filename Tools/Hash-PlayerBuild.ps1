param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
$playerRoot = Join-Path $ProjectRoot 'Artifacts/Player/Windows'
$validationRoot = Join-Path $ProjectRoot 'Artifacts/Validation'
$buildSummaryPath = Join-Path $validationRoot 'player-build-summary.json'
$buildSummary = Get-Content -LiteralPath $buildSummaryPath -Raw -Encoding UTF8 | ConvertFrom-Json
$files = @(
    Get-ChildItem -LiteralPath $playerRoot -File -Recurse |
        ForEach-Object {
            [ordered]@{
                path = [IO.Path]::GetRelativePath($playerRoot, $_.FullName).Replace('\', '/')
                bytes = $_.Length
                sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
            }
        } |
        Sort-Object path
)
$treeText = ($files | ForEach-Object { "$($_.path)`t$($_.bytes)`t$($_.sha256)" }) -join "`n"
$treeBytes = [Text.Encoding]::UTF8.GetBytes($treeText)
$treeSha = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($treeBytes)).ToLowerInvariant()
$report = [ordered]@{
    generatedUtc = [DateTime]::UtcNow.ToString('O')
    buildResult = $buildSummary.result
    unityVersion = $buildSummary.unityVersion
    buildReportedBytes = [long]$buildSummary.totalSize
    fileCount = $files.Count
    fileBytes = [long](($files | ForEach-Object { $_['bytes'] } | Measure-Object -Sum).Sum)
    treeSha256 = $treeSha
    files = $files
}
$jsonPath = Join-Path $validationRoot 'player-build-file-manifest.json'
$markdownPath = Join-Path $validationRoot 'player-build-file-manifest.md'
$report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $jsonPath -Encoding utf8NoBOM
@(
    '# Player build file manifest',
    '',
    "- Build result: **$($report.buildResult)**",
    "- Unity: $($report.unityVersion)",
    "- Files: $($report.fileCount)",
    "- File bytes: $($report.fileBytes)",
    "- Tree SHA-256: $($report.treeSha256)",
    '',
    'The tree hash covers each relative path, byte size, and file SHA-256 in sorted order.'
) | Set-Content -LiteralPath $markdownPath -Encoding utf8NoBOM
if ($report.buildResult -ne 'Succeeded' -or $files.Count -eq 0) { throw 'Player build manifest validation failed.' }
Write-Output "PASS: $($files.Count) Player files hashed; tree=$treeSha"
