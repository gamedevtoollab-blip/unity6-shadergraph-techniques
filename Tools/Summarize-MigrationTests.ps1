param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
$validationRoot = Join-Path $ProjectRoot 'Artifacts/Validation'
$migrationRoot = Join-Path $validationRoot 'Migration'
$packageManifest = Get-Content -LiteralPath (Join-Path $ProjectRoot 'Artifacts/Packages/package-manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$targets = @(
    @{ Name = '01_Dissolve'; Package = 'ShaderGraphTechniques-01-Dissolve.unitypackage' },
    @{ Name = '02_IntersectionShield'; Package = 'ShaderGraphTechniques-02-IntersectionShield.unitypackage' },
    @{ Name = '03_HeatHaze'; Package = 'ShaderGraphTechniques-03-HeatHaze.unitypackage' },
    @{ Name = '04_TriplanarSnow'; Package = 'ShaderGraphTechniques-04-TriplanarSnow.unitypackage' },
    @{ Name = '05_InteractiveGrass'; Package = 'ShaderGraphTechniques-05-InteractiveGrass.unitypackage' },
    @{ Name = '06_ScanPulse'; Package = 'ShaderGraphTechniques-06-ScanPulse.unitypackage' },
    @{ Name = 'Runtime-All'; Package = 'ShaderGraphTechniques-Runtime-All.unitypackage' }
)

$results = foreach ($target in $targets) {
    $reportPath = Join-Path (Join-Path $migrationRoot $target.Name) 'migration-validation.json'
    $report = Get-Content -LiteralPath $reportPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $expectedPackage = $packageManifest.packages | Where-Object filename -eq $target.Package
    $allImages = @($report.effects | ForEach-Object { $_.stateAPath; $_.stateBPath })
    $checks = [ordered]@{
        reportPassed = [bool]$report.passed
        unityVersion = $report.unityVersion -eq '6000.3.22f1'
        urpVersion = $report.urpVersion -eq '17.3.0'
        shaderGraphVersion = $report.shaderGraphVersion -eq '17.3.0'
        packageName = [IO.Path]::GetFileName($report.packagePath) -eq $target.Package
        packageHash = $null -ne $expectedPackage -and $report.packageSha256 -eq $expectedPackage.sha256
        stateImagesExist = @($allImages | Where-Object { -not (Test-Path -LiteralPath $_) }).Count -eq 0
        expectedEffectCount = @($report.effects).Count -eq $(if ($target.Name -eq 'Runtime-All') { 6 } else { 1 })
    }
    [ordered]@{
        name = $target.Name
        reportPath = $reportPath
        projectPath = $report.projectPath
        package = $target.Package
        packageSha256 = $report.packageSha256
        effectCount = @($report.effects).Count
        minimumPixelDifference = ($report.effects | Measure-Object -Property meanPixelDifference -Minimum).Minimum
        checks = $checks
        passed = @($checks.Values | Where-Object { -not $_ }).Count -eq 0
    }
}

$projectPaths = @($results.projectPath | Sort-Object -Unique)
$report = [ordered]@{
    generatedUtc = [DateTime]::UtcNow.ToString('O')
    scope = 'Six per-effect imports plus one Runtime-All import into seven independent Unity URP projects.'
    independentProjectCount = $projectPaths.Count
    independentProjects = $projectPaths
    contactSheets = @(
        (Join-Path $migrationRoot 'Runtime-All/contact-state-a.png'),
        (Join-Path $migrationRoot 'Runtime-All/contact-state-b.png')
    )
    allPassed = $projectPaths.Count -eq 7 -and @($results | Where-Object { -not $_.passed }).Count -eq 0
    results = @($results)
}

$jsonPath = Join-Path $validationRoot 'migration-validation-summary.json'
$markdownPath = Join-Path $validationRoot 'migration-validation-summary.md'
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $jsonPath -Encoding utf8NoBOM

$lines = @(
    '# Isolated package migration validation',
    '',
    "- Overall: **$(if ($report.allPassed) { 'PASS' } else { 'FAIL' })**",
    "- Independent Unity URP projects: $($report.independentProjectCount)",
    '- Versions: Unity 6000.3.22f1, URP 17.3.0, Shader Graph 17.3.0',
    '- Scope: six per-effect packages and the Runtime-All package; prefab import, supported shader, missing-script scan, control state A/B, offscreen Unity render, and image difference.',
    '',
    '| Import | Effects rendered | Minimum pixel difference | Result |',
    '|---|---:|---:|---|'
)
foreach ($result in $results) {
    $lines += "| $($result.name) | $($result.effectCount) | $(([double]$result.minimumPixelDifference).ToString('0.000000')) | $(if ($result.passed) { 'PASS' } else { 'FAIL' }) |"
}
$lines += @('', 'Full per-check evidence, screenshot hashes, package hashes, and project paths are in `migration-validation-summary.json` and the seven source reports.')
$lines | Set-Content -LiteralPath $markdownPath -Encoding utf8NoBOM

if (-not $report.allPassed) { throw "Migration summary validation failed. See $jsonPath" }
Write-Output "PASS: $($results.Count) independent package imports validated"
Write-Output $jsonPath
