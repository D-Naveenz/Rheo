#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Builds the defbuild tool and executes it to generate file definitions package.

.DESCRIPTION
    This script builds the Rheo.Storage.DefinitionsBuilder project in Release mode
    and executes the resulting defbuild.exe to pack definitions into Rheo.Storage\Assets.

.PARAMETER Configuration
    Build configuration (default: Release)

.PARAMETER OutputPath
    Output path for packed definitions (default: ../Rheo.Storage/Assets)

.PARAMETER SkipBuild
    Skip the build step (used when called from MSBuild post-build event)

.EXAMPLE
    .\build-and-pack.ps1
    .\build-and-pack.ps1 -Configuration Debug -OutputPath "C:\custom\path"
    .\build-and-pack.ps1 -SkipBuild
#>

[CmdletBinding()]
param(
    [Parameter()]
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [Parameter()]
    [string]$OutputPath = 'Rheo.Storage/Assets',

    [Parameter()]
    [switch]$SkipBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Script location
$ScriptRoot = $PSScriptRoot
$ProjectRoot = Split-Path $ScriptRoot -Parent

# Paths
$ProjectFile = Join-Path $ScriptRoot 'Rheo.Storage.DefinitionsBuilder.csproj'
$BuildOutputDir = Join-Path $ScriptRoot "bin\$Configuration\net9.0"
$DefBuildExe = Join-Path $BuildOutputDir 'RheoDefBuilder.exe'
$DefBuildDll = Join-Path $BuildOutputDir 'RheoDefBuilder.dll'

# Resolve output path
$ResolvedOutputPath = if ([System.IO.Path]::IsPathRooted($OutputPath)) {
    $OutputPath
} else {
    Join-Path $ProjectRoot $OutputPath
}

Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "  Rheo Definitions Builder & Packer  " -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

if (-not $SkipBuild) {
    # Step 1: Restore packages
    Write-Host "[1/4] Restoring NuGet packages..." -ForegroundColor Yellow
    try {
        dotnet restore $ProjectFile
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet restore failed with exit code $LASTEXITCODE"
        }
        Write-Host "✓ Packages restored successfully" -ForegroundColor Green
    } catch {
        Write-Error "Failed to restore packages: $_"
        exit 1
    }

    Write-Host ""

    # Step 2: Build the project
    Write-Host "[2/4] Building defbuild ($Configuration)..." -ForegroundColor Yellow
    try {
        dotnet build $ProjectFile --configuration $Configuration
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet build failed with exit code $LASTEXITCODE"
        }
        Write-Host "✓ Build completed successfully" -ForegroundColor Green
    } catch {
        Write-Error "Failed to build project: $_"
        exit 1
    }

    Write-Host ""
    $StepNum = 3
} else {
    Write-Host "Skipping build (already built by MSBuild)" -ForegroundColor Yellow
    Write-Host ""
    $StepNum = 1
}

# Verify build output
Write-Host "[$StepNum/$(if ($SkipBuild) {2} else {4})] Verifying build output..." -ForegroundColor Yellow
$ExecutablePath = if (Test-Path $DefBuildExe) {
    $DefBuildExe
} elseif (Test-Path $DefBuildDll) {
    $DefBuildDll
} else {
    Write-Error "Could not find defbuild executable at:`n  $DefBuildExe`n  $DefBuildDll"
    exit 1
}

Write-Host "✓ Found executable: $ExecutablePath" -ForegroundColor Green
Write-Host ""

# Ensure output directory exists
$StepNum++
Write-Host "[$StepNum/$(if ($SkipBuild) {2} else {4})] Creating output directory..." -ForegroundColor Yellow
if (-not (Test-Path $ResolvedOutputPath)) {
    New-Item -Path $ResolvedOutputPath -ItemType Directory -Force | Out-Null
    Write-Host "✓ Created directory: $ResolvedOutputPath" -ForegroundColor Green
} else {
    Write-Host "✓ Directory exists: $ResolvedOutputPath" -ForegroundColor Green
}

Write-Host ""

# Execute defbuild
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "  Executing defbuild...              " -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

try {
    Push-Location $BuildOutputDir
    
    if ($ExecutablePath -like "*.exe") {
        & $ExecutablePath pack -o $ResolvedOutputPath
    } else {
        dotnet $ExecutablePath pack -o $ResolvedOutputPath
    }
    
    $ExitCode = $LASTEXITCODE
    Pop-Location
    
    if ($ExitCode -ne 0) {
        throw "defbuild exited with code $ExitCode"
    }
    
    Write-Host ""
    Write-Host "=====================================" -ForegroundColor Green
    Write-Host "  ✓ Build and pack completed!        " -ForegroundColor Green
    Write-Host "=====================================" -ForegroundColor Green
    Write-Host "Output location: $ResolvedOutputPath" -ForegroundColor Cyan
    
} catch {
    Pop-Location
    Write-Error "Failed to execute defbuild: $_"
    exit 1
}