<#
.SYNOPSIS
    Validates the ume-template-api NuGet template package by testing all parameter
    combinations (size × api-type × database).

.DESCRIPTION
    This script performs end-to-end validation of the dotnet new template package:
    1. Packs the template package
    2. Installs it locally
    3. For each parameter combination (8 total):
       a. Generates a new solution from the template
       b. Restores NuGet packages
       c. Builds the generated solution
       d. Runs tests in the generated solution
    The script reports results for all combinations and fails if any combination fails.

.PARAMETER OutputDir
    Directory where temporary validation artifacts are created. Defaults to a temp folder.
#>
param(
    [string]$OutputDir = (Join-Path ([System.IO.Path]::GetTempPath()) "ume-template-api-$([System.Guid]::NewGuid().ToString('N').Substring(0,8))")
)

$ErrorActionPreference = 'Stop'

$scriptRoot = $PSScriptRoot
$templateProjectDir = Join-Path $scriptRoot '..' '..'
$templateProjectDir = (Resolve-Path $templateProjectDir).Path

Write-Host "=== ume-template-api validation ===" -ForegroundColor Cyan
Write-Host "Template project: $templateProjectDir"
Write-Host "Output directory:  $OutputDir"
Write-Host ""

# Ensure clean output directory
if (Test-Path $OutputDir) {
    Remove-Item $OutputDir -Recurse -Force
}
New-Item -ItemType Directory -Path $OutputDir | Out-Null

$nupkgDir = Join-Path $OutputDir 'nupkg'

# All parameter combinations: size × api-type × database
$combinations = @(
    @{ Size = "Default"; ApiType = "Cloud"; DatabaseName = "" }
    @{ Size = "Default"; ApiType = "Cloud"; DatabaseName = "Database" }
    @{ Size = "Default"; ApiType = "OnPrem"; DatabaseName = "" }
    @{ Size = "Default"; ApiType = "OnPrem"; DatabaseName = "Database" }
    @{ Size = "Small"; ApiType = "Cloud"; DatabaseName = "" }
    @{ Size = "Small"; ApiType = "Cloud"; DatabaseName = "Database" }
    @{ Size = "Small"; ApiType = "OnPrem"; DatabaseName = "" }
    @{ Size = "Small"; ApiType = "OnPrem"; DatabaseName = "Database" }
)

try {
    # Step 1: Pack
    Write-Host "--- Step 1: Pack template package ---" -ForegroundColor Yellow
    dotnet pack $templateProjectDir --output $nupkgDir --configuration Release
    if ($LASTEXITCODE -ne 0) { throw "dotnet pack failed with exit code $LASTEXITCODE" }

    $nupkg = Get-ChildItem -Path $nupkgDir -Filter '*.nupkg' | Select-Object -First 1
    if (-not $nupkg) { throw "No .nupkg file found in $nupkgDir" }
    Write-Host "Packed: $($nupkg.Name)" -ForegroundColor Green
    Write-Host ""

    # Step 2: Install template
    Write-Host "--- Step 2: Install template locally ---" -ForegroundColor Yellow
    dotnet new uninstall Umea.se.Templates 2>$null
    dotnet new install $nupkg.FullName
    if ($LASTEXITCODE -ne 0) { throw "dotnet new install failed with exit code $LASTEXITCODE" }
    Write-Host ""

    # Step 3: Generate all combinations (serial — fast, and template engine uses shared state)
    Write-Host "--- Step 3: Generate all combinations ---" -ForegroundColor Yellow
    $comboJobs = @()
    foreach ($combo in $combinations) {
        $dbLabel = if ($combo.DatabaseName) { "Database=$($combo.DatabaseName)" } else { "NoDatabase" }
        $label = "Size=$($combo.Size), ApiType=$($combo.ApiType), $dbLabel"
        $projectName = "Validate$($combo.Size)$($combo.ApiType)$(if ($combo.DatabaseName) { 'Db' } else { 'NoDb' })"
        $generatedDir = Join-Path $OutputDir $projectName

        $newArgs = @("new", "ume-template-api", "-n", $projectName, "-o", $generatedDir, "--size", $combo.Size, "--api-type", $combo.ApiType)
        if ($combo.DatabaseName) {
            $newArgs += @("--database-name", $combo.DatabaseName)
        }
        & dotnet @newArgs
        if ($LASTEXITCODE -ne 0) { throw "dotnet new failed for '$label' with exit code $LASTEXITCODE" }

        $comboJobs += @{ Label = $label; ProjectName = $projectName; GeneratedDir = $generatedDir }
        Write-Host "  Generated: $label" -ForegroundColor DarkGray
    }
    Write-Host ""

    # Step 4: Restore, build, and test all combinations in parallel
    Write-Host "--- Step 4: Validate all combinations (parallel) ---" -ForegroundColor Yellow
    $sharedNugetCache = Join-Path $OutputDir '.nuget-packages'
    $sharedHttpCache = Join-Path $OutputDir '.nuget-http-cache'
    $results = $comboJobs | ForEach-Object -ThrottleLimit 8 -Parallel {
        $job = $_
        $label = $job.Label
        $generatedDir = $job.GeneratedDir
        $logFile = Join-Path $generatedDir "validate.log"
        $env:NUGET_PACKAGES = $using:sharedNugetCache
        $env:NUGET_HTTP_CACHE_PATH = $using:sharedHttpCache

        try {
            $slnFile = Get-ChildItem -Path $generatedDir -Filter '*.slnx' | Select-Object -First 1
            if (-not $slnFile) { throw "No .slnx file found in $generatedDir" }

            dotnet restore $slnFile.FullName *>> $logFile
            if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed (exit code $LASTEXITCODE)" }

            dotnet build $slnFile.FullName --no-restore --configuration Release *>> $logFile
            if ($LASTEXITCODE -ne 0) { throw "dotnet build failed (exit code $LASTEXITCODE)" }

            dotnet test $slnFile.FullName --no-build --configuration Release *>> $logFile
            if ($LASTEXITCODE -ne 0) { throw "dotnet test failed (exit code $LASTEXITCODE)" }

            [PSCustomObject]@{ Label = $label; Status = "PASS"; Error = ""; LogFile = $logFile }
        }
        catch {
            [PSCustomObject]@{ Label = $label; Status = "FAIL"; Error = $_.Exception.Message; LogFile = $logFile }
        }
    }

    # Summary
    Write-Host ""
    Write-Host "=== Validation Summary ===" -ForegroundColor Cyan
    $passed = @($results | Where-Object { $_.Status -eq "PASS" }).Count
    $failed = @($results | Where-Object { $_.Status -eq "FAIL" }).Count

    foreach ($r in $results) {
        $color = if ($r.Status -eq "PASS") { "Green" } else { "Red" }
        $line = "  [$($r.Status)] $($r.Label)"
        if ($r.Error) { $line += " - $($r.Error)" }
        Write-Host $line -ForegroundColor $color
    }

    # Print full log for failed combinations
    $failedResults = @($results | Where-Object { $_.Status -eq "FAIL" })
    if ($failedResults.Count -gt 0) {
        Write-Host ""
        Write-Host "=== Failed Combination Logs ===" -ForegroundColor Red
        foreach ($r in $failedResults) {
            Write-Host "--- $($r.Label) ---" -ForegroundColor Red
            if (Test-Path $r.LogFile) {
                Get-Content $r.LogFile | Write-Host
            }
            Write-Host ""
        }
    }

    Write-Host ""
    Write-Host "Passed: $passed / $($results.Count)" -ForegroundColor $(if ($failed -eq 0) { "Green" } else { "Red" })

    if ($failed -gt 0) {
        throw "$failed combination(s) failed validation."
    }

    Write-Host "=== ALL VALIDATIONS PASSED ===" -ForegroundColor Green
}
finally {
    # Uninstall template to clean up
    Write-Host ""
    Write-Host "--- Cleanup: Uninstall template ---" -ForegroundColor Yellow
    dotnet new uninstall Umea.se.Templates 2>$null

    # Clean up output directory
    if (Test-Path $OutputDir) {
        Remove-Item $OutputDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}
