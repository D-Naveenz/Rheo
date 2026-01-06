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

.PARAMETER ApiKey
    NuGet API key for publishing (can be provided directly or via file)

.PARAMETER ApiKeyFile
    Path to file containing NuGet API key (default: nuget-api-key.secret.txt)

.PARAMETER OutputPath
    Output path for NuGet packages (default: ./nupkg)

.EXAMPLE
    .\build.ps1
    .\build.ps1 -Configuration Debug -SkipTests
    .\build.ps1 -Publish
    .\build.ps1 -Publish -ApiKeyFile 'path/to/key.txt'
    .\build.ps1 -Publish -ApiKey 'your-api-key-here'
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
    [string]$ApiKey,

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
Write-Host "Verifying build tools..." -ForegroundColor Yellow

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
    if ($targetFramework -match '^net([\d.]+)$') {
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

# Calculate total steps for progress display
$TotalSteps = if ($SkipTests) { 2 } else { 3 }

# Step 1: Build Rheo.Storage
Write-Host "[1/$TotalSteps] Building Rheo.Storage ($Configuration)..." -ForegroundColor Yellow
try {
    dotnet build $StorageProject --configuration $Configuration
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed with exit code $LASTEXITCODE"
    }
    Write-Host "✓ Rheo.Storage built successfully" -ForegroundColor Green
} catch {
    throw "Failed to build Rheo.Storage: $_"
}

Write-Host ""

# Step 2: Build and run tests (unless skipped)
if (-not $SkipTests) {
    Write-Host "[2/$TotalSteps] Building and running tests ($Configuration)..." -ForegroundColor Yellow
    Write-Host "  Building test project..." -ForegroundColor Cyan
    try {
        dotnet build $TestProject --configuration $Configuration
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet build failed with exit code $LASTEXITCODE"
        }
        Write-Host "  ✓ Test project built successfully" -ForegroundColor Green
    } catch {
        throw "Failed to build test project: $_"
    }

    Write-Host ""
    Write-Host "  Running tests..." -ForegroundColor Cyan
    try {
        dotnet test $TestProject --configuration $Configuration --no-build --verbosity normal
        if ($LASTEXITCODE -ne 0) {
            throw "Tests failed with exit code $LASTEXITCODE"
        }
        Write-Host "  ✓ All tests passed" -ForegroundColor Green
    } catch {
        throw "Tests failed: $_"
    }

    Write-Host ""
    $PackStep = 3
} else {
    Write-Host "Skipping tests" -ForegroundColor Yellow
    Write-Host ""
    $PackStep = 2
}

# Step 3/4: Pack NuGet package
Write-Host "[$PackStep/$TotalSteps] Packing NuGet package..." -ForegroundColor Yellow

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
    throw "Failed to pack NuGet package: $_"
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

    # Get API key from parameter or file
    $ResolvedApiKey = $null
    
    if (-not [string]::IsNullOrWhiteSpace($ApiKey)) {
        # Use API key provided as argument
        $ResolvedApiKey = $ApiKey.Trim()
        Write-Host "Using API key from command-line argument" -ForegroundColor Cyan
    } else {
        # Read API key from file
        $ApiKeyPath = if ([System.IO.Path]::IsPathRooted($ApiKeyFile)) {
            $ApiKeyFile
        } else {
            Join-Path $ScriptRoot $ApiKeyFile
        }

        if (-not (Test-Path $ApiKeyPath)) {
            throw "API key not provided and file not found: $ApiKeyPath"
        }

        $ResolvedApiKey = Get-Content -Path $ApiKeyPath -Raw | ForEach-Object { $_.Trim() }
        if ([string]::IsNullOrWhiteSpace($ResolvedApiKey)) {
            throw "API key file is empty: $ApiKeyPath"
        }
        Write-Host "Using API key from file: $ApiKeyPath" -ForegroundColor Cyan
    }

    Write-Host "Publishing $($PackageFile.Name)..." -ForegroundColor Yellow
    try {
        # Use environment variable instead of passing API key on the command line
        $env:NUGET_API_KEY = $ResolvedApiKey
        dotnet nuget push $PackageFile.FullName --source https://api.nuget.org/v3/index.json --api-key $env:NUGET_API_KEY
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet nuget push failed with exit code $LASTEXITCODE"
        }
        Write-Host "✓ Package published successfully" -ForegroundColor Green
    } catch {
        throw "Failed to publish package: $_"
    } finally {
        # Clear the API key from environment
        Remove-Item Env:\NUGET_API_KEY -ErrorAction SilentlyContinue
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
