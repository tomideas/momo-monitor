# Regenerate fixtures from current production markup, then run native WPF checks.
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
Push-Location $root
$previousAppData = $env:APPDATA
try {
    $markup = Get-Content StatusMonitor/SplashWindow.xaml -Raw
    $markup.Replace('clr-namespace:StatusMonitor.I18n', 'clr-namespace:StatusMonitor.I18n;assembly=MomoMonitor') |
        Set-Content (Join-Path $PSScriptRoot 'SplashWindow.xaml')
    $resources = Get-Content StatusMonitor/App.xaml -Raw
    $resources = $resources.Replace('<Application x:Class="StatusMonitor.App"', '<ResourceDictionary').
        Replace('</Application>', '</ResourceDictionary').Replace('<Application.Resources>', '').Replace('</Application.Resources>', '')
    $resources = [regex]::Replace($resources, 'clr-namespace:StatusMonitor\.([A-Za-z0-9]+)"', 'clr-namespace:StatusMonitor.$1;assembly=MomoMonitor"')
    Set-Content (Join-Path $PSScriptRoot 'Resources.xaml') $resources
    $env:APPDATA = Join-Path $root 'dev/.build-env/AppData'
    $sdk = Join-Path $env:USERPROFILE '.dotnet/dotnet.exe'
    & $sdk restore (Join-Path $PSScriptRoot 'SplashChecks.csproj') --configfile StatusMonitor/NuGet.Config -p:NuGetAudit=false
    if ($LASTEXITCODE) { throw 'Restore failed.' }
    & $sdk run --no-restore --project $PSScriptRoot -p:NuGetAudit=false
    if ($LASTEXITCODE) { throw 'Native splash checks failed.' }
} finally { $env:APPDATA = $previousAppData; Pop-Location }
