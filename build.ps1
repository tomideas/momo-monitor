<#
.SYNOPSIS
    Builds Momo Monitor into a single framework-dependent executable.

.DESCRIPTION
    The project lives on a network path, which MSBuild handles poorly, so the
    source is copied to a local temp folder and published from there. Requires
    the .NET 8 SDK (see README). The .NET 8 Desktop Runtime must be present on
    the machine that runs the exe.

.PARAMETER Test
    Build with the asInvoker manifest (no UAC prompt). Used for automated tests.

.PARAMETER Run
    Start the built executable when the build finishes.

.PARAMETER KeepWork
    Keep the temporary build folder instead of deleting it, for debugging a failed publish.
#>
param(
    [switch]$Test,
    [switch]$Run,
    [switch]$KeepWork
)

$ErrorActionPreference = 'Stop'

$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$src = Join-Path $here 'StatusMonitor'

$dotnet = Join-Path $env:USERPROFILE '.dotnet\dotnet.exe'
if (-not (Test-Path $dotnet)) { $dotnet = Join-Path $env:LOCALAPPDATA 'MomoBuild\dotnet\dotnet.exe' }
if (-not (Test-Path $dotnet)) { $dotnet = 'dotnet' }

$tempRoot = Join-Path $env:LOCALAPPDATA 'Temp'

# Sweep folders left by earlier runs. The one-hour guard keeps a concurrent build's folder
# alive; anything locked is skipped rather than failing the build.
foreach ($stale in Get-ChildItem $tempRoot -Directory -Filter 'MomoMonitor-build-*' -ErrorAction SilentlyContinue) {
    if ($stale.LastWriteTime -lt (Get-Date).AddHours(-1)) {
        Remove-Item $stale.FullName -Recurse -Force -ErrorAction SilentlyContinue
    }
}

$work = Join-Path $tempRoot ('MomoMonitor-build-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $work | Out-Null

try {
    Copy-Item (Join-Path $src '*') $work -Recurse -Force

    $extra = @()
    if ($Test) { $extra = @('-p:ApplicationManifest=app.asinvoker.manifest') }

    & $dotnet publish (Join-Path $work 'StatusMonitor.csproj') `
        -c Release -r win-x64 --self-contained false `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
        @extra -o (Join-Path $work 'out')
    if ($LASTEXITCODE -ne 0) { throw 'Momo Monitor publish failed.' }

    $outExe = Join-Path $work 'out\MomoMonitor.exe'
    $dest = Join-Path $here 'MomoMonitor.exe'
    # One executable, always. If the target is locked the build fails loudly rather than
    # leaving a second file behind for someone to mistake for the real one later.
    try {
        Copy-Item $outExe $dest -Force
        Write-Host "Built: $dest"
    }
    catch {
        throw "MomoMonitor.exe is running, so it could not be replaced. Close it (tray icon -> exit, so totals are saved) and build again."
    }

    if ($Run) { Start-Process $dest -WindowStyle Hidden }
}
finally {
    # Each publish leaves ~165 MB behind, so clean up even when the build failed.
    if ($KeepWork) { Write-Host "Work folder kept: $work" }
    else { Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue }
}
