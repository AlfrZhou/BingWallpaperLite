[CmdletBinding()]
param([switch]$SkipBuild)
$ErrorActionPreference = 'Stop'
if(-not $SkipBuild) { & (Join-Path $PSScriptRoot 'Build.ps1') | Out-Null }
$release = Join-Path $PSScriptRoot 'build\release'
$testData = Join-Path $PSScriptRoot ('build\ui-lifetime-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testData -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'config.json') -Destination (Join-Path $testData 'config.json')
$native = Join-Path $release 'BingWallpaperLite.exe'
$settings = Join-Path $release 'BingWallpaperHelper.exe'
$trayArguments = @('--tray','--no-update','--data-dir',('"' + $testData + '"'))
$trayProcess = $null
try {
    $trayProcess = Start-Process -FilePath $native -ArgumentList $trayArguments -PassThru
    Start-Sleep -Milliseconds 500
    foreach($mode in @('--test-close','--test-minimize')) {
        $helper = Start-Process -FilePath $settings -ArgumentList @($mode,'--data-dir',('"' + $testData + '"')) -PassThru
        if(-not $helper.WaitForExit(20000)) { throw "Settings lifetime test timed out: $mode" }
        $helper.Refresh()
        if($helper.ExitCode -ne 0) { throw "Settings lifetime test failed: $mode" }
    }
    $duplicate = Start-Process -FilePath $native -ArgumentList $trayArguments -PassThru
    if(-not $duplicate.WaitForExit(5000)) { throw 'Second tray instance did not exit.' }
    $trayProcess.Refresh()
    if($trayProcess.HasExited) { throw 'Tray process exited with the settings window.' }
    $perf = Get-CimInstance Win32_PerfFormattedData_PerfProc_Process | Where-Object { $_.IDProcess -eq $trayProcess.Id }
    if(-not $perf) { throw 'Memory counter for the tray process was not found.' }
    $result = [ordered]@{
        CloseExitsSettings = $true
        MinimizeExitsSettings = $true
        TraySurvivesSettings = $true
        SecondTrayInstanceExits = $true
        PrivateWorkingSetMiB = [Math]::Round($perf.WorkingSetPrivate / 1MB, 2)
        PrivateBytesMiB = [Math]::Round($perf.PrivateBytes / 1MB, 2)
        TotalWorkingSetMiB = [Math]::Round($perf.WorkingSet / 1MB, 2)
    }
    $result | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'build\ui-lifetime-verification.json') -Encoding UTF8
    $result | ConvertTo-Json
    if($perf.PrivateBytes -ge 5MB -or $perf.WorkingSetPrivate -ge 5MB) { throw 'Tray memory exceeded the 5 MiB private-memory target.' }
} finally {
    if($trayProcess) {
        $stop = Start-Process -FilePath $native -ArgumentList @('--quit','--data-dir',('"' + $testData + '"')) -Wait -PassThru
        if($stop.ExitCode -ne 0) { throw 'Test tray did not exit promptly.' }
    }
    $resolvedTest = [IO.Path]::GetFullPath($testData)
    $expectedParent = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'build'))
    if([IO.Path]::GetDirectoryName($resolvedTest) -ne $expectedParent -or [IO.Path]::GetFileName($resolvedTest) -notmatch '^ui-lifetime-[a-f0-9]{32}$') { throw 'Unexpected test cleanup target.' }
    Remove-Item -LiteralPath $resolvedTest -Recurse -Force
}
