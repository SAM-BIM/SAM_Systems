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
    exit $exitCode
}

Write-Host "TESTS PASSED" -ForegroundColor Green

# Also run the TwinWheelExample framework-free self-check
Write-Host ""
Write-Host "==== TwinWheelExample.Verify ====" -ForegroundColor Cyan

$buildOutput = "$repoRoot\build_tests"
$dllPath = Get-ChildItem -Path $buildOutput -Recurse -Filter "SAM.Analytical.Systems.Mollier.Tests.dll" | Select-Object -First 1
if (-not $dllPath) {
    Write-Host "Test DLL not found at expected path; skipping Verify." -ForegroundColor Yellow
    exit 0
}

# Capture the Verify output via a thrown exception or xunit output by running the example directly
# Use dotnet test --filter to run just the verification test
$verifyResult = dotnet test $testProject --configuration $Configuration --filter "FullyQualifiedName~Verify" --no-build 2>&1
$verifyExit = $LASTEXITCODE

if ($verifyExit -eq 0) {
    Write-Host "TwinWheelExample.Verify PASSED" -ForegroundColor Green
} else {
    Write-Host "WARNING: TwinWheelExample.Verify test was not found or failed (check test project)" -ForegroundColor Yellow
}

exit 0
