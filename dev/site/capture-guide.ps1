param([switch]$Elevated)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
Set-Location -LiteralPath $projectRoot
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Normal documentation screenshots require administrator sensor access. Run this script elevated.'
}
$imageRoot = Join-Path $projectRoot 'site\assets\images'
$logFile = Join-Path $PSScriptRoot 'capture-results.txt'
$env:APPDATA = Join-Path $projectRoot 'dev\.build-env\appdata'
$jobs = @(
    @('paper-dashboard-en.png', '--theme', 'paper'),
    @('volt-dashboard-en.png', '--theme', 'volt'),
    @('settings-en.png', '--settings', '--theme', 'paper'),
    @('monitoring-en.png', '--settings', '--monitoring', '--theme', 'paper'),
    @('data-en.png', '--settings', '--data-info', '--theme', 'paper'),
    @('info-en.png', '--info', '--theme', 'paper'),
    @('process-en.png', '--process', '--theme', 'paper', '--wait-seconds', '15'),
    @('mini-en.png', '--mini', '--theme', 'paper'),
    @('fans-en.png', '--fans', '--theme', 'paper'),
    @('fan-custom-en.png', '--fans', '--fan-custom', '--theme', 'paper'),
    @('alerts-en.png', '--alerts', '--theme', 'paper')
)
'Capturing actual English WPF views with administrator sensor access; preview mode prevents data and fan writes.' | Set-Content -LiteralPath $logFile -Encoding utf8
foreach ($job in $jobs) {
    $output = Join-Path $imageRoot $job[0]
    $renderArgs = @('StatusMonitor\bin\Release\net8.0-windows\MomoMonitor.dll', '--render', $output, '--english') + $job[1..($job.Length - 1)]
    $process = Start-Process -FilePath 'C:\Program Files\dotnet\dotnet.exe' -ArgumentList $renderArgs -WindowStyle Hidden -PassThru
    if (-not $process.WaitForExit(35000)) {
        Stop-Process -Id $process.Id -Force
        throw ('Documentation render timed out: ' + $job[0])
    }
    if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $output)) { throw ('Render failed: ' + $job[0]) }
    ('PASS ' + $job[0]) | Add-Content -LiteralPath $logFile -Encoding utf8
}
& 'C:\Program Files\dotnet\dotnet.exe' 'dev\site\Capture\bin\Docs\Capture.dll' --render
if ($LASTEXITCODE -ne 0) { throw 'Settings detail captures failed.' }
'PASS settings detail captures; DONE' | Add-Content -LiteralPath $logFile -Encoding utf8
