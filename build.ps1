[CmdletBinding()]
param(
    [Parameter(Mandatory=$false)]
    [string]$Target = "Help",
    
    [Parameter(Mandatory=$false)]
    [string]$Configuration = "Release",
    
    [Parameter(Mandatory=$false)]
    [string]$TargetBranch = "dev",
    
    [Parameter(Mandatory=$false)]
    [switch]$SkipIncrementalist = $false,
    
    [Parameter(Mandatory=$false)]
    [switch]$CollectCodeCoverage = $false,
    
    [Parameter(Mandatory=$false)]
    [string]$NugetApiKey = $null,
    
    [Parameter(Mandatory=$false)]
    [string]$NugetSource = "https://api.nuget.org/v3/index.json",
    
    [Parameter(Mandatory=$false)]
    [string]$SignClientSecret = $null,
    
    [Parameter(Mandatory=$false)]
    [string]$SignClientUser = $null,
    
    [Parameter(Mandatory=$false)]
    [string]$SigningName = "Akka.Management",
    
    [Parameter(Mandatory=$false)]
    [string]$SigningDescription = "Akka.NET Service Discovery and Coordination",
    
    [Parameter(Mandatory=$false)]
    [string]$SigningUrl = "https://getakka.net/",
    
    [Parameter(Mandatory=$false)]
    [string]$VersionSuffix = "",
    
    [Parameter(Mandatory=$false)]
    [switch]$PreRelease = $false
)

$ErrorActionPreference = 'Stop'

# Define paths
$PSScriptRoot = Split-Path $MyInvocation.MyCommand.Path -Parent
$ReleaseNotesPath = Join-Path $PSScriptRoot "RELEASE_NOTES.md"
$DirectoryBuildPropsPath = Join-Path $PSScriptRoot "Directory.Build.props"
$OutputDir = Join-Path $PSScriptRoot "bin"
$OutputNuGet = Join-Path $OutputDir "nuget"
$OutputTests = Join-Path $PSScriptRoot "TestResults"
$OutputPerfTests = Join-Path $PSScriptRoot "PerfResults"
$IncrementalistOutput = Join-Path $OutputDir "incrementalist.txt"

# Find solution file
$SolutionFile = Get-ChildItem -Path $PSScriptRoot -Filter "*.sln" | Select-Object -First 1 -ExpandProperty FullName

# Parse release notes to extract version and notes
function Get-ReleaseNotes {
    param (
        [Parameter(Mandatory=$true)]
        [string]$MarkdownFile
    )

    # Read markdown file content
    $content = Get-Content -Path $MarkdownFile -Raw

    # Split content based on headers
    $sections = $content -split "####"

    # Output object to store result
    $outputObject = [PSCustomObject]@{
        Version       = $null
        Date          = $null
        ReleaseNotes  = $null
    }

    # Check if we have at least 2 sections (1. Before the header, 2. Header + notes)
    if ($sections.Count -ge 2) {
        $headerAndNotes = $sections[1].Trim()
        
        # Extract version and date from the header
        if ($headerAndNotes -match "([0-9]+\.[0-9]+\.[0-9]+)(.*)") {
            $outputObject.Version = $matches[1]
            $notesText = $headerAndNotes.Substring($matches[0].Length).Trim()
            $outputObject.ReleaseNotes = $notesText
        }
    }

    return $outputObject
}

# Update version and release notes in Directory.Build.props
function Update-VersionAndReleaseNotes {
    param (
        [Parameter(Mandatory=$true)]
        [PSCustomObject]$ReleaseNotesResult,

        [Parameter(Mandatory=$true)]
        [string]$XmlFilePath
    )

    if (-not (Test-Path $XmlFilePath)) {
        throw "Directory.Build.props not found at: $XmlFilePath"
    }

    try {
        # Load XML
        $xmlContent = New-Object XML
        $xmlContent.Load($XmlFilePath)

        # Update VersionPrefix and PackageReleaseNotes
        $versionPrefixElement = $xmlContent.SelectSingleNode("//VersionPrefix")
        if ($null -eq $versionPrefixElement) {
            throw "VersionPrefix element not found in Directory.Build.props"
        }
        $versionPrefixElement.InnerText = $ReleaseNotesResult.Version

        $packageReleaseNotesElement = $xmlContent.SelectSingleNode("//PackageReleaseNotes")
        if ($null -eq $packageReleaseNotesElement) {
            throw "PackageReleaseNotes element not found in Directory.Build.props"
        }
        $packageReleaseNotesElement.InnerText = $ReleaseNotesResult.ReleaseNotes

        # Save the updated XML
        $xmlContent.Save($XmlFilePath)
    }
    catch {
        throw "Failed to update Directory.Build.props: $_"
    }
}

# Clean output directories
function Clean-Directories {
    Write-Host "Cleaning output directories..." -ForegroundColor Cyan
    
    $directories = @($OutputDir, $OutputTests, $OutputPerfTests, $OutputNuGet)
    
    foreach ($dir in $directories) {
        if (Test-Path $dir) {
            Remove-Item -Path $dir -Recurse -Force
        }
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }
    
    Write-Host "Output directories cleaned." -ForegroundColor Green
}

# Restore NuGet packages
function Restore-Packages {
    Write-Host "Restoring NuGet packages..." -ForegroundColor Cyan
    
    dotnet restore $SolutionFile
    
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to restore NuGet packages."
    }
    
    Write-Host "NuGet packages restored." -ForegroundColor Green
}

# Update assembly version information
function Update-AssemblyInfo {
    Write-Host "Updating assembly version information..." -ForegroundColor Cyan
    
    # Parse release notes
    $releaseNotes = Get-ReleaseNotes -MarkdownFile $ReleaseNotesPath
    
    if ($null -eq $releaseNotes.Version) {
        throw "Failed to parse version from RELEASE_NOTES.md"
    }
    
    Write-Host "Setting assembly version to $($releaseNotes.Version)" -ForegroundColor Green
    
    # Update VersionPrefix in Directory.Build.props
    if (-not (Test-Path $DirectoryBuildPropsPath)) {
        throw "Directory.Build.props not found at: $DirectoryBuildPropsPath"
    }
    
    try {
        # Load XML
        $xmlContent = New-Object XML
        $xmlContent.Load($DirectoryBuildPropsPath)
        
        # Update VersionPrefix
        $versionPrefixElement = $xmlContent.SelectSingleNode("//VersionPrefix")
        if ($null -eq $versionPrefixElement) {
            throw "VersionPrefix element not found in Directory.Build.props"
        }
        $versionPrefixElement.InnerText = $releaseNotes.Version
        
        # Save the updated XML
        $xmlContent.Save($DirectoryBuildPropsPath)
        
        Write-Host "Assembly version updated successfully." -ForegroundColor Green
    }
    catch {
        throw "Failed to update assembly version: $_"
    }
}

# Build solution
function Build-Solution {
    Write-Host "Building solution..." -ForegroundColor Cyan
    
    if ($SkipIncrementalist) {
        # Build entire solution
        Write-Host "Building entire solution (Incrementalist skipped)" -ForegroundColor Yellow
        dotnet build $SolutionFile -c $Configuration --no-restore
        
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to build solution."
        }
    }
    else {
        # Use Incrementalist to build affected projects
        Write-Host "Using Incrementalist to build affected projects" -ForegroundColor Cyan
        
        dotnet tool run incrementalist -- -b $TargetBranch -r -- build -c $Configuration --no-restore
        
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to build affected projects."
        }
    }
    
    Write-Host "Build completed successfully." -ForegroundColor Green
}

# Run tests
function Run-Tests {
    Write-Host "Running tests..."
    
    # Clean up any lingering test processes
    try {
        taskkill /F /IM testhost.exe 2>$null
    } catch {
        # Ignore errors if no processes found
    }
    
    $baseArgs = @(
        "test"
        "-c", $Configuration
        "--no-build"
        "--logger:trx"
        "--logger", "console;verbosity=normal"
        "--results-directory", $OutputTests
    )

    if ($CollectCodeCoverage) {
        $baseArgs += @(
            "--collect:XPlat Code Coverage"
        )
        Write-Host "Collecting code coverage with args: $($baseArgs -join ' ')" -ForegroundColor Green
    }

    if ($SkipIncrementalist) {
        Get-ChildItem -Path $PSScriptRoot -Filter "*Tests.csproj" -Recurse | ForEach-Object {
            $projectPath = $_.FullName
            Write-Host "Running tests for project: $projectPath"
            $projectArgs = $baseArgs + @($projectPath)
            & dotnet $projectArgs
            if ($LastExitCode -ne 0) { throw "Unit tests failed" }
        }
    } else {
        $affectedProjects = & dotnet tool run incrementalist -- -b $TargetBranch -r --
        if ($LastExitCode -ne 0) { throw "Failed to find affected projects" }
        
        $testProjects = $affectedProjects | Where-Object { $_ -like "*Tests.csproj" }
        foreach ($project in $testProjects) {
            $projectPath = $project.Trim()
            if (Test-Path $projectPath) {
                Write-Host "Running tests for affected project: $projectPath"
                $projectArgs = $baseArgs + @($projectPath)
                & dotnet $projectArgs
                if ($LastExitCode -ne 0) { throw "Unit tests failed" }
            } else {
                Write-Warning "Project not found: $projectPath"
            }
        }
    }
}

# Run NBench performance tests
function Run-NBench {
    Write-Host "Running NBench performance tests..." -ForegroundColor Cyan
    
    # Create performance test results directory if it doesn't exist
    if (-not (Test-Path $OutputPerfTests)) {
        New-Item -ItemType Directory -Path $OutputPerfTests -Force | Out-Null
    }
    

    # Find all performance test projects
    $perfTestProjects = Get-ChildItem -Path "./src/**/*.Tests.Performance.csproj" -Recurse
    
    foreach ($project in $perfTestProjects) {
        $projectDir = Split-Path -Parent $project.FullName
        $projectName = $project.BaseName
        
        Write-Host "Running NBench tests for $projectName" -ForegroundColor Cyan
        
        $nbenchArgs = "run --no-build -c $Configuration -- --output $OutputPerfTests --concurrent true --trace true --diagnostic"
        
        Push-Location $projectDir
        try {
            Invoke-Expression "dotnet $nbenchArgs"
            
            if ($LASTEXITCODE -ne 0) {
                throw "NBench tests failed for project: $projectName"
            }
        }
        finally {
            Pop-Location
        }
    }
    
    Write-Host "NBench tests completed successfully." -ForegroundColor Green
}

# Create NuGet packages
function Create-NuGet {
    Write-Host "Creating NuGet packages..." -ForegroundColor Cyan
    
    # Create NuGet output directory if it doesn't exist
    if (-not (Test-Path $OutputNuGet)) {
        New-Item -ItemType Directory -Path $OutputNuGet -Force | Out-Null
    }
    
    # Determine version suffix
    $buildNumber = $env:BUILD_NUMBER
    if ([string]::IsNullOrEmpty($buildNumber)) {
        $buildNumber = "0"
    }
    
    $effectiveVersionSuffix = $VersionSuffix
    if ($PreRelease) {
        $betaSuffix = if ($buildNumber -ne "0") { $buildNumber } else { [DateTimeOffset]::UtcNow.Ticks.ToString() }
        $effectiveVersionSuffix = "beta$betaSuffix"
    }
    
    $versionSuffixArg = if (![string]::IsNullOrEmpty($effectiveVersionSuffix)) { "--version-suffix", $effectiveVersionSuffix } else { @() }
    
    if ($SkipIncrementalist) {
        # Find all projects excluding test projects
        $packageProjects = Get-ChildItem -Path "./src/**/*.csproj" -Exclude "*Tests*" -Recurse
        
        foreach ($project in $packageProjects) {
            $projectName = $project.BaseName
            
            Write-Host "Creating NuGet package for $projectName" -ForegroundColor Cyan
            
            $packArgs = @(
                "pack",
                $project.FullName,
                "-c", $Configuration,
                "--no-build",
                "--include-symbols",
                "-o", $OutputNuGet
            )
            
            if ($versionSuffixArg.Length -gt 0) {
                $packArgs += $versionSuffixArg
            }
            
            Write-Host "Running: dotnet $($packArgs -join ' ')" -ForegroundColor Yellow
            
            & dotnet $packArgs
            
            if ($LASTEXITCODE -ne 0) {
                throw "Failed to create NuGet package for project: $projectName"
            }
        }
    }
    else {
        # Use Incrementalist to create NuGet packages for affected projects
        Write-Host "Using Incrementalist to create NuGet packages for affected projects" -ForegroundColor Cyan
        
        $packArgs = @(
            "pack",
            "-c", $Configuration,
            "--no-build",
            "--include-symbols",
            "-o", $OutputNuGet
        )
        
        if ($versionSuffixArg.Length -gt 0) {
            $packArgs += $versionSuffixArg
        }
        
        $packCommand = $packArgs -join " "
        Write-Host "Running Incrementalist with: $packCommand" -ForegroundColor Yellow
        
        dotnet tool run incrementalist -- -b $TargetBranch -r -- $packCommand
        
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to create NuGet packages for affected projects."
        }
    }
    
    Write-Host "NuGet packages created successfully." -ForegroundColor Green
}

# Sign NuGet packages
function Sign-Packages {
    param([string] $Configuration = $Configuration,
          [string] $OutputPath = $OutputNuGet,
          [string] $SignClientUser = $null,
          [string] $SignClientSecret = $null,
          [string] $SigningName = "Phobos", 
          [string] $SigningDescription = "Phobos® Tools and Drivers", 
          [string] $SigningUrl = "https://phobos.petabridge.com/")

    Write-Host "Starting package signing process..." -ForegroundColor Cyan
    
    # Try to get values from environment variables if parameters are empty
    if ([string]::IsNullOrEmpty($SignClientUser)) {
        $SignClientUser = $env:SignClientUser
        Write-Host "Using SignClientUser from environment variable" -ForegroundColor Yellow
    }
    
    if ([string]::IsNullOrEmpty($SignClientSecret)) {
        $SignClientSecret = $env:SignClientSecret
        Write-Host "Using SignClientSecret from environment variable" -ForegroundColor Yellow
    }
    
    Write-Host "DEBUG: SignClientUser = '$SignClientUser', Type: $($SignClientUser.GetType().FullName), IsNull: $($null -eq $SignClientUser), IsEmpty: $([string]::IsNullOrEmpty($SignClientUser))" -ForegroundColor Yellow
    Write-Host "DEBUG: SignClientSecret length = '$($SignClientSecret.Length)', Type: $($SignClientSecret.GetType().FullName), IsNull: $($null -eq $SignClientSecret), IsEmpty: $([string]::IsNullOrEmpty($SignClientSecret))" -ForegroundColor Yellow
    
    Write-Host "Looking for NuGet packages in $OutputPath..." -ForegroundColor Cyan
    
    # Check if output directory exists
    if (!(Test-Path $OutputPath)) {
        Write-Warning "Output directory $OutputPath does not exist - skipping signing"
        return
    }

    # First get all nupkg files
    $allPackages = Get-ChildItem $OutputPath -Filter *.nupkg
    Write-Host "Found total of $($allPackages.Count) .nupkg files in directory" -ForegroundColor Green
    
    # Then filter out symbol packages
    $nupkgFiles = $allPackages | Where-Object { $_.Name -notlike "*.symbols.nupkg" }
    
    Write-Host "After filtering, found $($nupkgFiles.Count) packages to sign:" -ForegroundColor Green
    $nupkgFiles | Select-Object -ExpandProperty FullName | ForEach-Object { Write-Host "  - $(Split-Path $_ -Leaf)" }

    if ($nupkgFiles.Count -eq 0) {
        Write-Warning "No NuGet packages found in $OutputPath - skipping signing"
        return
    }

    # If we don't have any signing credentials, skip the signing process
    if ([string]::IsNullOrEmpty($SignClientUser) -or [string]::IsNullOrEmpty($SignClientSecret)) {
        Write-Warning "SignClientUser or SignClientSecret not provided - skipping signing"
        return
    }

    # Using signing configuration with name, description, and URL
    Write-Host "Using signing configuration:`n  Signing Name: $SigningName`n  Signing Description: $SigningDescription`n  Signing URL: $SigningUrl"

    dotnet tool restore

    try {
        Write-Host "Submitting packages for signing..." -ForegroundColor Green
        & $PSScriptRoot\scripts\sign-packages.ps1 -Configuration $Configuration `
                                                  -OutputPath $OutputPath `
                                                  -SignClientUser $SignClientUser `
                                                  -SignClientSecret $SignClientSecret `
                                                  -SigningName $SigningName `
                                                  -SigningDescription $SigningDescription `
                                                  -SigningUrl $SigningUrl
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Build failed: SignClient failed to sign packages"
            return $false
        }
        
        Write-Host "All packages signed successfully" -ForegroundColor Green
        return $true
    }
    catch {
        Write-Error "Build failed: SignClient encountered an error while signing packages: $_"
        return $false
    }
}

# Publish NuGet packages
function Publish-NuGet {
    Write-Host "Publishing NuGet packages..." -ForegroundColor Cyan
    
    if ([string]::IsNullOrEmpty($NugetApiKey)) {
        Write-Warning "Skipping package push as NugetApiKey was not provided."
        return
    }
    
    $packagesPath = Join-Path $OutputNuGet "*.nupkg"
    Write-Host "Looking for NuGet packages in: $packagesPath" -ForegroundColor Yellow
    
    if (-not (Test-Path $OutputNuGet)) {
        Write-Warning "NuGet output directory not found at: $OutputNuGet"
        return
    }
    
    Write-Host "Publishing packages to $NugetSource" -ForegroundColor Cyan
    dotnet nuget push $packagesPath --api-key $NugetApiKey --source $NugetSource --skip-duplicates
    
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to publish NuGet packages."
    }
    
    Write-Host "NuGet packages published successfully." -ForegroundColor Green
}

# Update version information
function Update-Version {
    Write-Host "Updating version information..." -ForegroundColor Cyan
    
    # Parse release notes
    $releaseNotes = Get-ReleaseNotes -MarkdownFile $ReleaseNotesPath
    
    if ($null -eq $releaseNotes.Version) {
        throw "Failed to parse version from RELEASE_NOTES.md"
    }
    
    Write-Host "Updating to version $($releaseNotes.Version)" -ForegroundColor Green
    
    # Update version information
    Update-VersionAndReleaseNotes -ReleaseNotesResult $releaseNotes -XmlFilePath $DirectoryBuildPropsPath
    
    Write-Host "Version information updated successfully." -ForegroundColor Green
}

# Display help
function Show-Help {
    Write-Host "Usage: ./build.ps1 [Target] [Options]" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Targets:" -ForegroundColor Yellow
    Write-Host "  Clean             - Clean output directories" -ForegroundColor Cyan
    Write-Host "  RestorePackages   - Restore NuGet packages (depends on Clean)" -ForegroundColor Cyan
    Write-Host "  UpdateVersion     - Update version information from RELEASE_NOTES.md" -ForegroundColor Cyan
    Write-Host "  Build             - Build the solution (depends on UpdateVersion)" -ForegroundColor Cyan
    Write-Host "  RunTests          - Run unit tests" -ForegroundColor Cyan
    Write-Host "  NBench            - Run NBench performance tests" -ForegroundColor Cyan
    Write-Host "  CreateNuget       - Create NuGet packages" -ForegroundColor Cyan
    Write-Host "  SignPackages      - Sign NuGet packages" -ForegroundColor Cyan
    Write-Host "  PublishNuget      - Publish NuGet packages to NuGet.org" -ForegroundColor Cyan
    Write-Host "  All               - Run all targets (Clean, RestorePackages, UpdateVersion, Build, RunTests, CreateNuget)" -ForegroundColor Cyan
    Write-Host "  Help              - Display this help" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Options:" -ForegroundColor Yellow
    Write-Host "  -Configuration    - Build configuration (Debug or Release, default: Release)" -ForegroundColor Cyan
    Write-Host "  -TargetBranch     - Target branch for Incrementalist (default: dev)" -ForegroundColor Cyan
    Write-Host "  -SkipIncrementalist - Skip Incrementalist and build all projects (default: false)" -ForegroundColor Cyan
    Write-Host "  -CollectCodeCoverage - Collect code coverage during tests (default: false)" -ForegroundColor Cyan
    Write-Host "  -NugetApiKey      - API key for NuGet publishing" -ForegroundColor Cyan
    Write-Host "  -NugetSource      - NuGet source URL (default: https://api.nuget.org/v3/index.json)" -ForegroundColor Cyan
    Write-Host "  -SignClientUser   - User for code signing" -ForegroundColor Cyan
    Write-Host "  -SignClientSecret - Secret for code signing" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Examples:" -ForegroundColor Yellow
    Write-Host "  ./build.ps1 Build -Configuration Debug" -ForegroundColor Cyan
    Write-Host "  ./build.ps1 All -SkipIncrementalist" -ForegroundColor Cyan
    Write-Host "  ./build.ps1 PublishNuget -NugetApiKey your-api-key" -ForegroundColor Cyan
}

# Ensure tools are restored
function Restore-Tools {
    Write-Host "Restoring .NET tools..." -ForegroundColor Cyan
    
    dotnet tool restore
    
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to restore .NET tools."
    }
    
    Write-Host ".NET tools restored successfully." -ForegroundColor Green
}

# Main execution
try {
    # Restore tools first
    Restore-Tools
    
    # Execute the specified target
    switch ($Target) {
        "Clean" {
            Clean-Directories
        }
        "RestorePackages" {
            Clean-Directories
            Restore-Packages
        }
        "UpdateVersion" {
            Update-Version
        }
        "Build" {
            Clean-Directories
            Restore-Packages
            Update-Version
            Build-Solution
        }
        "RunTests" {
            Clean-Directories
            Restore-Packages
            Update-Version
            Build-Solution
            Run-Tests
        }
        "NBench" {
            Clean-Directories
            Restore-Packages
            Update-Version
            Build-Solution
            Run-NBench
        }
        "CreateNuget" {
            Clean-Directories
            Restore-Packages
            Update-Version
            Build-Solution
            Create-NuGet
        }
        "SignPackages" {
            Sign-Packages -SignClientUser $SignClientUser -SignClientSecret $SignClientSecret -SigningName $SigningName -SigningDescription $SigningDescription -SigningUrl $SigningUrl
        }
        "PublishNuget" {
            Publish-NuGet
        }
        "All" {
            Clean-Directories
            Restore-Packages
            Update-Version
            Build-Solution
            Run-Tests
            Create-NuGet
            if ($SignClientSecret -and $SignClientUser) {
                Sign-Packages -SignClientUser $SignClientUser -SignClientSecret $SignClientSecret -SigningName $SigningName -SigningDescription $SigningDescription -SigningUrl $SigningUrl
            }
            if ($NugetApiKey) {
                Publish-NuGet
            }
        }
        "Help" {
            Show-Help
        }
        default {
            Write-Host "Unknown target: $Target" -ForegroundColor Red
            Show-Help
            exit 1
        }
    }
    
    Write-Host "Build completed successfully." -ForegroundColor Green
}
catch {
    Write-Error "Build failed: $_"
    exit 1
}
