#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Builds, tests, packs, and optionally publishes the Rheo.Storage NuGet package.

.DESCRIPTION
    This script replicates the nuget-publish-storage.yml workflow locally for easier debugging.
    It builds the solution, runs tests, packs the NuGet package, and can optionally publish it.

.PARAMETER Configuration
    Build configuration (default: Release)

.PARAMETER SkipTests
    Skip running tests

.PARAMETER Publish
    Publish the package to NuGet.org after packing

.PARAMETER ApiKeyFile
    Path to file containing NuGet API key (default: nuget-api-key.secret.txt)

.PARAMETER OutputPath
    Output path for NuGet packages (default: ./nupkg)

.EXAMPLE
    .\nuget-build.ps1
    .\nuget-build.ps1 -Configuration Debug -SkipTests
    .\nuget-build.ps1 -Publish
#>

[CmdletBinding()]
param(
    [Parameter()]
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [Parameter()]
    [switch]$SkipTests,

    [Parameter()]
    [switch]$Publish,

    [Parameter()]
    [string]$ApiKeyFile = 'nuget-api-key.secret.txt',

    [Parameter()]
    [string]$OutputPath = './nupkg'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Script location
$ScriptRoot = $PSScriptRoot

# Project paths
$SolutionFile = Join-Path $ScriptRoot 'Rheo.sln'
$DefinitionsBuilderProject = Join-Path $ScriptRoot 'Rheo.Storage.DefinitionsBuilder\Rheo.Storage.DefinitionsBuilder.csproj'
$StorageProject = Join-Path $ScriptRoot 'Rheo.Storage\Rheo.Storage.csproj'
$TestProject = Join-Path $ScriptRoot 'Rheo.Storage.Test\Rheo.Storage.Test.csproj'

# Resolve output path
$ResolvedOutputPath = if ([System.IO.Path]::IsPathRooted($OutputPath)) {
    $OutputPath
} else {
    Join-Path $ScriptRoot $OutputPath
}

Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "  Rheo.Storage NuGet Build & Pack   " -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

# Step 0: Verify build tools and target framework
Write-Host "[0/$(if ($SkipTests) {3} else {4})] Verifying build tools..." -ForegroundColor Yellow

# Check if dotnet CLI is available
try {
    $dotnetVersion = dotnet --version 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet CLI not found"
    }
    Write-Host "  ✓ dotnet CLI: $dotnetVersion" -ForegroundColor Green
} catch {
    Write-Error ".NET SDK not found. Please install the .NET SDK from https://dotnet.microsoft.com/download"
    exit 1
}

# Extract target framework from project file
try {
    [xml]$projectXml = Get-Content $StorageProject
    $targetFramework = $projectXml.Project.PropertyGroup.TargetFramework
    
    if ([string]::IsNullOrWhiteSpace($targetFramework)) {
        throw "TargetFramework not found in project file"
    }
    
    # Parse framework version (e.g., "net10.0" -> "10.0")
    if ($targetFramework -match '^net(\d+\.\d+)$') {
        $requiredVersion = $Matches[1]
        Write-Host "  Required: .NET $requiredVersion ($targetFramework)" -ForegroundColor Cyan
    } else {
        Write-Warning "  Unable to parse framework version from: $targetFramework"
        $requiredVersion = $null
    }
} catch {
    Write-Warning "  Could not read target framework from project file: $_"
    $requiredVersion = $null
}

# Check if required .NET SDK is installed
if ($requiredVersion) {
    try {
        $installedSdks = dotnet --list-sdks 2>$null | ForEach-Object {
            if ($_ -match '^(\d+\.\d+\.\d+)') {
                $Matches[1]
            }
        }
        
        $majorMinor = $requiredVersion
        $sdkFound = $installedSdks | Where-Object { $_ -match "^$majorMinor" } | Select-Object -First 1
        
        if ($sdkFound) {
            Write-Host "  ✓ .NET SDK $sdkFound installed" -ForegroundColor Green
        } else {
            Write-Host ""
            Write-Warning ".NET $requiredVersion SDK not found!"
            Write-Host "Installed SDKs:" -ForegroundColor Yellow
            dotnet --list-sdks
            Write-Host ""
            $response = Read-Host "Do you want to continue anyway? (y/n)"
            if ($response -notmatch '^[Yy]') {
                Write-Host "Build cancelled. Please install .NET $requiredVersion SDK from https://dotnet.microsoft.com/download" -ForegroundColor Yellow
                exit 1
            }
        }
    } catch {
        Write-Warning "  Could not verify SDK installation: $_"
    }
}

Write-Host ""
Write-Host "Configuration: $Configuration" -ForegroundColor Cyan
Write-Host "Output Path:   $ResolvedOutputPath" -ForegroundColor Cyan
Write-Host ""

# Step 1: Build Rheo.Storage
Write-Host "[1/$(if ($SkipTests) {2} else {3})] Building Rheo.Storage ($Configuration)..." -ForegroundColor Yellow
try {
    dotnet build $StorageProject --configuration $Configuration
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed with exit code $LASTEXITCODE"
    }
    Write-Host "✓ Rheo.Storage built successfully" -ForegroundColor Green
} catch {
    Write-Error "Failed to build Rheo.Storage: $_"
    exit 1
}

Write-Host ""

# Step 2: Build and run tests (unless skipped)
if (-not $SkipTests) {
    Write-Host "[2/4] Building Rheo.Storage.Test ($Configuration)..." -ForegroundColor Yellow
    try {
        dotnet build $TestProject --configuration $Configuration
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet build failed with exit code $LASTEXITCODE"
        }
        Write-Host "✓ Test project built successfully" -ForegroundColor Green
    } catch {
        Write-Error "Failed to build test project: $_"
        exit 1
    }

    Write-Host ""

    Write-Host "[3/4] Running tests..." -ForegroundColor Yellow
    try {
        dotnet test $TestProject --configuration $Configuration --no-build --verbosity normal
        if ($LASTEXITCODE -ne 0) {
            throw "Tests failed with exit code $LASTEXITCODE"
        }
        Write-Host "✓ All tests passed" -ForegroundColor Green
    } catch {
        Write-Error "Tests failed: $_"
        exit 1
    }

    Write-Host ""
    $PackStep = 4
} else {
    Write-Host "[2/3] Skipping tests" -ForegroundColor Yellow
    Write-Host ""
    $PackStep = 3
}

# Step 3/4: Pack NuGet package
Write-Host "[$PackStep/$(if ($SkipTests) {3} else {4})] Packing NuGet package..." -ForegroundColor Yellow

# Ensure output directory exists
if (-not (Test-Path $ResolvedOutputPath)) {
    New-Item -Path $ResolvedOutputPath -ItemType Directory -Force | Out-Null
}

try {
    dotnet pack $StorageProject --configuration $Configuration --no-build --output $ResolvedOutputPath
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet pack failed with exit code $LASTEXITCODE"
    }
    Write-Host "✓ Package created successfully" -ForegroundColor Green
} catch {
    Write-Error "Failed to pack NuGet package: $_"
    exit 1
}

Write-Host ""

# Find the generated package
$PackageFiles = @(Get-ChildItem -Path $ResolvedOutputPath -Filter "*.nupkg" -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending)
if ($PackageFiles.Count -eq 0) {
    Write-Error "No .nupkg file found in $ResolvedOutputPath"
    exit 1
}

$PackageFile = $PackageFiles[0]
Write-Host "Package: $($PackageFile.Name)" -ForegroundColor Cyan
Write-Host ""

# Optional: Publish to NuGet
if ($Publish) {
    Write-Host "=====================================" -ForegroundColor Cyan
    Write-Host "  Publishing to NuGet.org...         " -ForegroundColor Cyan
    Write-Host "=====================================" -ForegroundColor Cyan
    Write-Host ""

    # Read API key
    $ApiKeyPath = if ([System.IO.Path]::IsPathRooted($ApiKeyFile)) {
        $ApiKeyFile
    } else {
        Join-Path $ScriptRoot $ApiKeyFile
    }

    if (-not (Test-Path $ApiKeyPath)) {
        Write-Error "API key file not found: $ApiKeyPath"
        exit 1
    }

    $ApiKey = Get-Content -Path $ApiKeyPath -Raw | ForEach-Object { $_.Trim() }
    if ([string]::IsNullOrWhiteSpace($ApiKey)) {
        Write-Error "API key file is empty: $ApiKeyPath"
        exit 1
    }

    Write-Host "Publishing $($PackageFile.Name)..." -ForegroundColor Yellow
    try {
        dotnet nuget push $PackageFile.FullName --api-key $ApiKey --source https://api.nuget.org/v3/index.json
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet nuget push failed with exit code $LASTEXITCODE"
        }
        Write-Host "✓ Package published successfully" -ForegroundColor Green
    } catch {
        Write-Error "Failed to publish package: $_"
        exit 1
    }

    Write-Host ""
    Write-Host "=====================================" -ForegroundColor Green
    Write-Host "  ✓ Build, pack, and publish done!   " -ForegroundColor Green
    Write-Host "=====================================" -ForegroundColor Green
} else {
    Write-Host "=====================================" -ForegroundColor Green
    Write-Host "  ✓ Build and pack completed!        " -ForegroundColor Green
    Write-Host "=====================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "To publish, run with -Publish flag" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Output location: $ResolvedOutputPath" -ForegroundColor Cyan
