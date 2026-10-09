<#
.SYNOPSIS
    Publishes Glystrata as a self-contained win-x64 single-file build and
    packages it into a zip under dist/, ready to copy to another machine.

.PARAMETER Fdd
    Build the framework-dependent variant (profile win-x64-fdd, output in
    dist/win-x64-fdd/, zip suffix "-fdd"). The target machine needs the
    .NET 10 Desktop Runtime (x64) installed.

.PARAMETER SkipPublish
    Skip the "dotnet publish" step and just zip whatever is already in
    dist/win-x64/. Use this if you published from Visual Studio's Publish UI
    instead (right-click the Glystrata project > Publish > win-x64 profile) -
    that also writes to dist/win-x64/ since the profile sets PublishDir.
#>
param(
    [string]$Configuration = "Release",
    [switch]$SkipPublish,
    [switch]$Fdd
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$csproj = Join-Path $repoRoot "src\Glystrata\Glystrata.csproj"
$propsPath = Join-Path $repoRoot "Directory.Build.props"
$profileName = if ($Fdd) { "win-x64-fdd" } else { "win-x64" }
$publishDir = Join-Path $repoRoot "dist\$profileName"

$version = "0.0.0"
if (Test-Path $propsPath) {
    $match = Select-String -Path $propsPath -Pattern "<Version>(.*?)</Version>" | Select-Object -First 1
    if ($match) {
        $version = $match.Matches[0].Groups[1].Value
    }
}

if (-not $SkipPublish) {
    $dotnetExe = $null
    $dotnetCmd = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($dotnetCmd) {
        $dotnetExe = $dotnetCmd.Source
    } else {
        foreach ($candidate in @("$env:ProgramFiles\dotnet\dotnet.exe", "${env:ProgramFiles(x86)}\dotnet\dotnet.exe")) {
            if (Test-Path $candidate) {
                $dotnetExe = $candidate
                break
            }
        }
    }

    if (-not $dotnetExe) {
        Write-Warning "dotnet CLI not found (not installed, and not at the default install path)."
        Write-Warning "Use Visual Studio instead: Solution Explorer > right-click Glystrata project > Publish > win-x64 profile > Publish."
        Write-Warning "After that finishes, re-run this script with -SkipPublish to just build the zip:"
        Write-Warning "  .\scripts\publish.ps1 -SkipPublish"
        exit 1
    }

    Write-Host "Publishing Glystrata v$version ($Configuration, $profileName)..." -ForegroundColor Cyan

    if (Test-Path $publishDir) {
        Remove-Item $publishDir -Recurse -Force
    }

    & $dotnetExe publish $csproj -c $Configuration -p:PublishProfile=$profileName
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE"
    }
}

if (-not (Test-Path $publishDir)) {
    throw "Publish output directory not found: $publishDir. Publish first (see notes above), or drop -SkipPublish."
}

$zipSuffix = if ($Fdd) { "-fdd" } else { "" }
$zipName = "Glystrata-v$version-win-x64$zipSuffix.zip"
$zipPath = Join-Path $repoRoot "dist\$zipName"
if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath

Write-Host ""
Write-Host "Done." -ForegroundColor Green
Write-Host "  Raw output:  $publishDir"
Write-Host "  Zip package: $zipPath"
