# SPDX-License-Identifier: LGPL-3.0-or-later
# Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
$repoRoot = $PSScriptRoot

$solutions = @(
    @{ Name = "SAM";              Path = "$repoRoot\..\SAM\SAM.sln" },
    @{ Name = "SAM_Mollier";      Path = "$repoRoot\..\SAM_Mollier\SAM_Mollier.sln" },
    @{ Name = "SAM_Systems";      Path = "$repoRoot\SAM_Systems.sln" }
)

$failed = @()

foreach ($sol in $solutions) {
    Write-Host "==== Building $($sol.Name) ($Configuration) ====" -ForegroundColor Cyan
    $result = dotnet build $sol.Path --configuration $Configuration 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host "FAILED: $($sol.Name)" -ForegroundColor Red
        $failed += $sol.Name
    } else {
        Write-Host "OK: $($sol.Name)" -ForegroundColor Green
    }
    Write-Host ""
}

if ($failed.Count -gt 0) {
    Write-Host "BUILD FAILED: $($failed -join ', ')" -ForegroundColor Red
    exit 1
}

Write-Host "BUILD SUCCEEDED: all $($solutions.Count) solutions" -ForegroundColor Green
exit 0
