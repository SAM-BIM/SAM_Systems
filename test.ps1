# SPDX-License-Identifier: LGPL-3.0-or-later
# Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
$repoRoot = $PSScriptRoot
$testProject = "$repoRoot\SAM.Analytical.Systems.Mollier.Tests\SAM.Analytical.Systems.Mollier.Tests.csproj"

Write-Host "==== Running Mollier Bridge Tests ($Configuration) ====" -ForegroundColor Cyan

$result = dotnet test $testProject --configuration $Configuration 2>&1
$exitCode = $LASTEXITCODE

if ($exitCode -ne 0) {
    Write-Host "TESTS FAILED" -ForegroundColor Red
    Write-Host ($result | Out-String)
    exit $exitCode
}

Write-Host "TESTS PASSED" -ForegroundColor Green

# Also run the TwinWheelExample framework-free self-check
Write-Host ""
Write-Host "==== TwinWheelExample.Verify ====" -ForegroundColor Cyan

$buildOutput = "$repoRoot\build_tests"
$dllPath = Get-ChildItem -Path $buildOutput -Recurse -Filter "SAM.Analytical.Systems.Mollier.Tests.dll" | Select-Object -First 1
if (-not $dllPath) {
    Write-Host "FAIL: test DLL not found under $buildOutput after dotnet test." -ForegroundColor Red
    exit 1
}

# Capture the Verify output via a thrown exception or xunit output by running the example directly
# Use dotnet test --filter to run just the verification test
$verifyResult = dotnet test $testProject --configuration $Configuration --filter "FullyQualifiedName~Verify" --no-build 2>&1
$verifyExit = $LASTEXITCODE

if ($verifyExit -ne 0) {
    Write-Host "TwinWheelExample.Verify FAILED" -ForegroundColor Red
    Write-Host ($verifyResult | Out-String)
    exit 1
}

# A zero exit code with no matching tests would otherwise look like a silent pass; treat it as a failure.
if (($verifyResult | Out-String) -match 'No test matches|No test is available') {
    Write-Host "TwinWheelExample.Verify FAILED: no tests matched the Verify filter" -ForegroundColor Red
    Write-Host ($verifyResult | Out-String)
    exit 1
}

Write-Host "TwinWheelExample.Verify PASSED" -ForegroundColor Green

exit 0
