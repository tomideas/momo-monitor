<#
.SYNOPSIS
    Builds a self-contained Windows x64 executable; packaging is opt-in.
.DESCRIPTION
    Publishes in a local temporary folder and copies MomoMonitor.exe to the repository root.
    Only -Portable creates a distribution folder and zip. Local settings and history are
    never distribution inputs.
.PARAMETER Portable
    Explicitly create the portable distribution folder and ZIP after building the executable.
.PARAMETER Test
    Use the asInvoker manifest (no UAC prompt) and a separately named test executable.
.PARAMETER FrameworkDependent
    Developer-only smaller build that requires the .NET 8 Desktop Runtime on the target PC.
.PARAMETER Run
    Start the root executable when the build finishes.
.PARAMETER KeepWork
    Keep the temporary build folder for debugging a failed publish.
#>
param(
    [switch]$Portable,
    [switch]$Test,
    [switch]$FrameworkDependent,
    [switch]$Run,
    [switch]$KeepWork
)

$ErrorActionPreference = 'Stop'
$here = [System.IO.Path]::GetFullPath((Split-Path -Parent $MyInvocation.MyCommand.Path))
$src = Join-Path $here 'StatusMonitor'
$sensorProvenance = Get-Content -LiteralPath (Join-Path $src 'libs\LibreHardwareMonitor.provenance.json') -Raw | ConvertFrom-Json
$sensorHash = (Get-FileHash -LiteralPath (Join-Path $src 'libs\LibreHardwareMonitorLib.dll') -Algorithm SHA256).Hash
if (-not $sensorHash.Equals($sensorProvenance.sha256, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Sensor library provenance does not match its DLL. Update the recorded version/hash before publishing.'
}
$dotnet = Join-Path $env:USERPROFILE '.dotnet\dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnet)) { $dotnet = Join-Path $env:LOCALAPPDATA 'MomoBuild\dotnet\dotnet.exe' }
if (-not (Test-Path -LiteralPath $dotnet)) { $dotnet = 'dotnet' }
$tempRoot = [System.IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Temp'))

function Remove-MomoBuildFolder([string]$PathToRemove) {
    # Every recursive deletion is restricted to a verified direct build child of this temp root.
    $targetPath = [System.IO.Path]::GetFullPath($PathToRemove)
    $parentPath = [System.IO.Path]::GetDirectoryName($targetPath)
    $leafName = [System.IO.Path]::GetFileName($targetPath)
    if (-not $parentPath.Equals($tempRoot, [System.StringComparison]::OrdinalIgnoreCase) -or
        $leafName -notmatch '^MomoMonitor-build-[0-9a-f]{32}$') {
        throw "Refusing to delete a path outside the Momo build temporary folders: $targetPath"
    }
    if (-not (Test-Path -LiteralPath $targetPath)) { return }
    $folder = Get-Item -LiteralPath $targetPath -Force
    if (($folder.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "Refusing to recursively delete a linked build folder: $targetPath"
    }
    $linkedChild = Get-ChildItem -LiteralPath $targetPath -Recurse -Force -ErrorAction Stop |
        Where-Object { ($_.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0 } |
        Select-Object -First 1
    if ($linkedChild) { throw "Refusing to recursively delete a build folder containing a link: $targetPath" }
    Remove-Item -LiteralPath $targetPath -Recurse -Force
}

# A one-hour guard protects concurrent builds. Locked or linked leftovers are left alone.
foreach ($stale in Get-ChildItem -LiteralPath $tempRoot -Directory -Filter 'MomoMonitor-build-*' -ErrorAction SilentlyContinue) {
    if ($stale.LastWriteTime -lt (Get-Date).AddHours(-1)) {
        try { Remove-MomoBuildFolder $stale.FullName } catch { Write-Warning $_.Exception.Message }
    }
}
$work = Join-Path $tempRoot ('MomoMonitor-build-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work | Out-Null
$originalAppData = $env:APPDATA

try {
    # Do not copy generated bin/obj trees, which contain host paths and waste temporary disk space.
    foreach ($item in Get-ChildItem -LiteralPath $src -Force) {
        if ($item.Name -in @('bin', 'obj')) { continue }
        Copy-Item -LiteralPath $item.FullName -Destination $work -Recurse -Force
    }
    $extra = @()
    if ($Test) { $extra += '-p:ApplicationManifest=app.asinvoker.manifest' }
    $selfContained = if ($FrameworkDependent) { 'false' } else { 'true' }
    # Keep NuGet's user configuration/cache writes inside the workspace. Explicit restore
    # configuration avoids probing the caller's protected Roaming/NuGet directory.
    $buildEnvironment = Join-Path $here 'dev\.build-env'
    $env:APPDATA = Join-Path $buildEnvironment 'AppData'
    $packageCache = Join-Path $buildEnvironment 'packages'
    New-Item -ItemType Directory -Path $env:APPDATA -Force | Out-Null
    New-Item -ItemType Directory -Path $packageCache -Force | Out-Null
    $project = Join-Path $work 'StatusMonitor.csproj'
    $nugetConfig = Join-Path $work 'NuGet.Config'
    if (-not (Test-Path -LiteralPath $nugetConfig)) { throw 'StatusMonitor/NuGet.Config is required for a reproducible restore.' }
    & $dotnet restore $project -r win-x64 --configfile $nugetConfig --packages $packageCache `
        -p:SelfContained=$selfContained -p:PublishSingleFile=true -p:PublishTrimmed=false @extra
    if ($LASTEXITCODE -ne 0) { throw 'Momo Monitor restore failed. Check NuGet access and the Windows x64 runtime packages.' }
    & $dotnet publish (Join-Path $work 'StatusMonitor.csproj') `
        -c Release -r win-x64 --self-contained $selfContained --no-restore `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:PublishTrimmed=false -p:DebugType=None -p:DebugSymbols=false `
        @extra -o (Join-Path $work 'out')
    if ($LASTEXITCODE -ne 0) { throw 'Momo Monitor publish failed.' }

    $outExe = Join-Path $work 'out\MomoMonitor.exe'
    if (-not (Test-Path -LiteralPath $outExe)) { throw 'Publish did not produce MomoMonitor.exe.' }
    $rootExeName = if ($Test) { 'MomoMonitor.Test.exe' } else { 'MomoMonitor.exe' }
    $dest = Join-Path $here $rootExeName
    $rootUpdated = $false
    try {
        Copy-Item -LiteralPath $outExe -Destination $dest -Force
        $rootUpdated = $true
        Write-Host "Built: $dest"
    }
    catch {
        Write-Warning "The executable was compiled, but $rootExeName was not updated. Exit Momo manually before rebuilding. The running app was not closed. $($_.Exception.Message)"
        if (-not $Portable) { throw 'The root executable could not be updated.' }
    }
    if (-not $Portable) {
        if ($Run -and $rootUpdated) { Start-Process -FilePath $dest -WindowStyle Hidden }
        return
    }
    $packageName = 'MomoMonitor-Portable-win-x64'
    if ($FrameworkDependent) { $packageName += '-FrameworkDependent' }
    if ($Test) { $packageName += '-Test' }
    $package = Join-Path $work $packageName
    New-Item -ItemType Directory -Path (Join-Path $package 'momo-data') -Force | Out-Null
    Copy-Item -LiteralPath $outExe -Destination (Join-Path $package 'MomoMonitor.exe')
    Copy-Item -LiteralPath (Join-Path $here 'LICENSE') -Destination (Join-Path $package 'LICENSE')
    $packageFiles = @('MomoMonitor.exe', 'PORTABLE-README.txt', 'LICENSE')
    $portableGuide = @'
Momo System Monitor - Portable / Momo 系統監測 - 可攜版

1. Extract the complete folder to a writable local folder before running MomoMonitor.exe.
   請先將整個資料夾解壓縮至可寫入的本機位置，再啟動 MomoMonitor.exe。
2. Keep momo-data beside the executable. Settings and histories live there.
   請保留程式旁的 momo-data。設定、用電歷史與提醒記錄會保存在此處。
3. To move or back up your data, exit Momo first and copy the complete folder together.
   搬移或備份前請先結束 Momo，再複製整個資料夾。不要只搬 exe。
4. Failed saves report the selected path. Momo never silently switches to AppData.
   Move the complete folder to a writable location and restart.
   無法儲存時會顯示資料路徑，不會偷偷改存 AppData。請搬到可寫入位置後重開。
5. On a new PC, preferences and history are kept; fan control returns to firmware Auto.
   Hardware calibration, startup preferences and window placement are reset.
   換電腦時保留一般偏好與歷史；風扇回韌體 Auto，硬體校正、啟動偏好與視窗位置重設。
6. Each JSON retains a last-good .bak. Damaged originals are preserved during recovery.
   每份 JSON 保留上一版 .bak；恢復資料時也會保留損毀的原檔。
7. CPU temperatures / board fans may need the optional PawnIO driver and administrator
   permission. Driver installation and Windows startup registration affect the current PC;
   they are not portable. Momo asks before installing a driver.
   CPU 溫度／主機板風扇可能需要 PawnIO 驅動與管理員權限。驅動安裝和 Windows
   自動啟動只作用於目前電腦，不會隨資料夾搬移；安裝驅動前會詢問。
8. Select CPU/GPU/RAM to view trends and top processes in the Process tab.
   點 CPU／GPU／RAM 可在程序分頁查看趨勢及主要程序。
9. Battery charge power is separate from computer consumption. Sleep and unobserved
   gaps are excluded from energy. Laptop/unknown hardware does not use desktop power defaults.
   電池充電功率不等於電腦耗電；睡眠與未觀測區間不补算能耗，筆電／未知機型不套桌機功率預設。

Existing users: an empty momo-data imports existing %APPDATA%\StatusMonitor data once.
Existing portable data is never merged with AppData. Remove momo-data only when deliberately
choosing legacy AppData mode. Settings provides local storage and move instructions.
既有用戶：空白 momo-data 首次啟動會匯入既有 AppData；已有可攜資料就不再合併。
只有刻意使用舊 AppData 模式時才移除 momo-data；保存與搬移說明可在設定查看。

This distribution contains no personal settings or histories.
此發佈包不含任何本機個人設定或歷史資料。
Licence: GPL-3.0; see LICENSE. User guide: https://tomideas.github.io/momo-monitor
授權為 GPL-3.0，請參閱 LICENSE；使用說明：https://tomideas.github.io/momo-monitor
'@
    if ($FrameworkDependent) {
        $portableGuide += "`r`nDeveloper build: .NET 8 Desktop Runtime is required.`r`n開發用精簡版：需要 .NET 8 Desktop Runtime。`r`n"
    }
    else {
        $portableGuide += "`r`nSelf-contained Windows x64: no .NET installation is needed.`r`nWindows x64 自包含版：使用者不需安裝 .NET。`r`n"
    }
    if ($Test) { $portableGuide += "`r`nTEST BUILD: no UAC elevation; some sensors will be unavailable.`r`n測試版：不會提升權限，部分感測資料可能不可用。`r`n" }
    Set-Content -LiteralPath (Join-Path $package 'PORTABLE-README.txt') -Value $portableGuide -Encoding UTF8

    # Add the empty directory explicitly: its presence selects portable storage on first launch.
    $stagedZip = Join-Path $work ($packageName + '.zip')
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::Open($stagedZip, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        [void]$archive.CreateEntry($packageName + '/momo-data/')
        foreach ($fileName in $packageFiles) {
            [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,
                (Join-Path $package $fileName), ($packageName + '/' + $fileName),
                [System.IO.Compression.CompressionLevel]::Optimal)
        }
    }
    finally { $archive.Dispose() }

    $dist = Join-Path $here 'dist'
    $destPackage = Join-Path $dist $packageName
    if (Test-Path -LiteralPath $destPackage) {
        # Never clean a folder that someone might have run and populated with personal data.
        foreach ($child in Get-ChildItem -LiteralPath $destPackage -Force) {
            if ($child.Name -notin @('MomoMonitor.exe', 'PORTABLE-README.txt', 'LICENSE', 'momo-data') -or
                ($child.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0 -or
                ($child.Name -eq 'momo-data' -and (-not $child.PSIsContainer -or
                    @(Get-ChildItem -LiteralPath $child.FullName -Force).Count -ne 0)) -or
                ($child.Name -ne 'momo-data' -and $child.PSIsContainer)) {
                throw "Distribution folder contains user data or unexpected files. Move it before rebuilding: $destPackage"
            }
        }
    }
    New-Item -ItemType Directory -Path (Join-Path $destPackage 'momo-data') -Force | Out-Null
    foreach ($fileName in $packageFiles) {
        Copy-Item -LiteralPath (Join-Path $package $fileName) -Destination (Join-Path $destPackage $fileName) -Force
    }
    $destZip = Join-Path $dist ($packageName + '.zip')
    Copy-Item -LiteralPath $stagedZip -Destination $destZip -Force
    Write-Host "Portable folder: $destPackage"
    Write-Host "Portable zip: $destZip"
    if ($Run -and $rootUpdated) { Start-Process -FilePath $dest -WindowStyle Hidden }
    elseif ($Run) { Write-Warning 'Skipped -Run because the root executable was not updated.' }
}
finally {
    $env:APPDATA = $originalAppData
    if ($KeepWork) { Write-Host "Work folder kept: $work" }
    else {
        try { Remove-MomoBuildFolder $work } catch { Write-Warning "Could not clean the verified build folder: $($_.Exception.Message)" }
    }
}
