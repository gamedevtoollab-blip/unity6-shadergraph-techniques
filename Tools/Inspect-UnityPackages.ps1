param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
$packageRoot = Join-Path $ProjectRoot 'Artifacts/Packages'
$validationRoot = Join-Path $ProjectRoot 'Artifacts/Validation'
$manifestPath = Join-Path $packageRoot 'package-manifest.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
$runName = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ')
$inspectionRoot = Join-Path $validationRoot "package-inspection/$runName"
New-Item -ItemType Directory -Force -Path $inspectionRoot | Out-Null

$results = foreach ($packageEntry in $manifest.packages) {
    $packagePath = Join-Path $packageRoot $packageEntry.filename
    $extractPath = Join-Path $inspectionRoot ([IO.Path]::GetFileNameWithoutExtension($packageEntry.filename))
    New-Item -ItemType Directory -Force -Path $extractPath | Out-Null
    & tar -xf $packagePath -C $extractPath
    if ($LASTEXITCODE -ne 0) { throw "Could not extract $packagePath" }

    $archiveAssets = foreach ($guidFolder in Get-ChildItem -LiteralPath $extractPath -Directory) {
        $pathnameFile = Join-Path $guidFolder.FullName 'pathname'
        $assetFile = Join-Path $guidFolder.FullName 'asset'
        $metaFile = Join-Path $guidFolder.FullName 'asset.meta'
        if (-not (Test-Path -LiteralPath $pathnameFile)) { continue }
        $assetPath = (Get-Content -LiteralPath $pathnameFile -Raw -Encoding UTF8).Trim()
        [ordered]@{
            path = $assetPath
            guid = $guidFolder.Name
            sha256 = if (Test-Path -LiteralPath $assetFile) { (Get-FileHash -LiteralPath $assetFile -Algorithm SHA256).Hash.ToLowerInvariant() } else { $null }
            hasAsset = Test-Path -LiteralPath $assetFile
            hasMeta = Test-Path -LiteralPath $metaFile
        }
    }

    $expectedByPath = @{}
    foreach ($asset in $packageEntry.assets) { $expectedByPath[$asset.path] = $asset }
    $actualByPath = @{}
    foreach ($asset in $archiveAssets) { $actualByPath[$asset.path] = $asset }
    $missing = @($expectedByPath.Keys | Where-Object { -not $actualByPath.ContainsKey($_) } | Sort-Object)
    $unexpected = @($actualByPath.Keys | Where-Object { -not $expectedByPath.ContainsKey($_) } | Sort-Object)
    $mismatches = foreach ($assetPath in $expectedByPath.Keys) {
        if (-not $actualByPath.ContainsKey($assetPath)) { continue }
        $expected = $expectedByPath[$assetPath]
        $actual = $actualByPath[$assetPath]
        if ($expected.guid -ne $actual.guid -or $expected.sha256.ToLowerInvariant() -ne $actual.sha256 -or
            -not $actual.hasAsset -or -not $actual.hasMeta) {
            [ordered]@{ path = $assetPath; expectedGuid = $expected.guid; actualGuid = $actual.guid; expectedSha256 = $expected.sha256; actualSha256 = $actual.sha256; hasAsset = $actual.hasAsset; hasMeta = $actual.hasMeta }
        }
    }

    $forbidden = @($archiveAssets.path | Where-Object { $_ -match '(^|/)(Demo|Tests|Editor|ProjectSettings)(/|$)' })
    $effectToken = if ($packageEntry.filename -match 'ShaderGraphTechniques-(\d\d-[^.]+)\.unitypackage') { $Matches[1].Replace('-', '_') } else { $null }
    $boundaryViolations = if ($effectToken) {
        @($archiveAssets.path | Where-Object {
            $_ -notlike "Assets/ShaderGraphTechniques/Runtime/$effectToken/*" -and
            $_ -notlike 'Assets/ShaderGraphTechniques/Runtime/Common/*' -and
            $_ -ne 'Assets/ShaderGraphTechniques/Runtime/ShaderGraphTechniques.Runtime.asmdef'
        })
    } else { @() }

    $checks = [ordered]@{
        packageExists = Test-Path -LiteralPath $packagePath
        packageSize = (Get-Item -LiteralPath $packagePath).Length -eq [long]$packageEntry.byteSize
        packageSha256 = (Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash.ToLowerInvariant() -eq $packageEntry.sha256.ToLowerInvariant()
        exactAssetSet = $missing.Count -eq 0 -and $unexpected.Count -eq 0
        assetGuidAndHash = @($mismatches).Count -eq 0
        noForbiddenPaths = $forbidden.Count -eq 0
        effectBoundary = $boundaryViolations.Count -eq 0
    }

    [ordered]@{
        filename = $packageEntry.filename
        extractionPath = $extractPath
        assetCount = @($archiveAssets).Count
        missing = $missing
        unexpected = $unexpected
        mismatches = @($mismatches)
        forbidden = $forbidden
        boundaryViolations = $boundaryViolations
        checks = $checks
        passed = @($checks.Values | Where-Object { -not $_ }).Count -eq 0
        assets = @($archiveAssets)
    }
}

$licenseGuids = @(
    foreach ($result in $results) {
        foreach ($asset in $result.assets) {
            if ($asset['path'] -eq 'Assets/ShaderGraphTechniques/Runtime/Common/LICENSE.txt') { $asset['guid'] }
        }
    }
) | Sort-Object -Unique
$report = [ordered]@{
    generatedUtc = [DateTime]::UtcNow.ToString('O')
    manifest = $manifestPath
    inspectionRoot = $inspectionRoot
    commonLicenseGuid = if ($licenseGuids.Count -eq 1) { $licenseGuids[0] } else { $null }
    commonLicenseGuidConsistent = $licenseGuids.Count -eq 1
    allPassed = $licenseGuids.Count -eq 1 -and @($results | Where-Object { -not $_.passed }).Count -eq 0
    packages = @($results)
}

$jsonPath = Join-Path $validationRoot 'package-content-validation.json'
$markdownPath = Join-Path $validationRoot 'package-content-validation.md'
$report | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $jsonPath -Encoding utf8NoBOM

$lines = @(
    '# Unity package content validation',
    '',
    "- Generated (UTC): $($report.generatedUtc)",
    "- Overall: **$(if ($report.allPassed) { 'PASS' } else { 'FAIL' })**",
    "- Shared license GUID: $($report.commonLicenseGuid)",
    '',
    '| Package | Assets | Exact manifest | Boundary | Result |',
    '|---|---:|---|---|---|'
)
foreach ($result in $results) {
    $lines += "| $($result.filename) | $($result.assetCount) | $(if ($result.checks.exactAssetSet -and $result.checks.assetGuidAndHash) { 'PASS' } else { 'FAIL' }) | $(if ($result.checks.noForbiddenPaths -and $result.checks.effectBoundary) { 'PASS' } else { 'FAIL' }) | $(if ($result.passed) { 'PASS' } else { 'FAIL' }) |"
}
$lines += @('', 'The validator extracted each archive and compared every pathname, GUID, and asset SHA-256 against `package-manifest.json`.')
$lines | Set-Content -LiteralPath $markdownPath -Encoding utf8NoBOM

if (-not $report.allPassed) { throw "Package content validation failed. See $jsonPath" }
Write-Output "PASS: $($results.Count) package archives validated"
Write-Output $jsonPath
