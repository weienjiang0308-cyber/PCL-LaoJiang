[CmdletBinding()]
param()
. "$PSScriptRoot\Enter-ProjectEnvironment.ps1"
$manifest = Get-Content -LiteralPath (Join-Path $projectRoot 'reports\installer-manifest.json') -Raw | ConvertFrom-Json
$testRoot = Join-Path $projectRoot ('sandbox\installer-p1-' + [Guid]::NewGuid().ToString('N'))
$installation = Join-Path $testRoot '安装 Test'
$results = [ordered]@{ status='running'; installer=$manifest.installer; installerSha256=$manifest.installerSha256; directory=$installation }
$results | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $projectRoot 'reports\installer-p1-acceptance.json') -Encoding utf8
trap {
    if ($results) {
        $results.status='failed'
        $results.error=$_.Exception.Message
        $results | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $projectRoot 'reports\installer-p1-acceptance.json') -Encoding utf8
    }
    throw
}
function Run-Setup([string]$Target,[string]$Label,[bool]$ExpectedSuccess=$true) {
    $report = Join-Path $projectRoot ("reports\installer-$Label.json")
    if (Test-Path -LiteralPath $report) { Remove-Item -LiteralPath $report }
    $process = Start-Process -FilePath $manifest.installer -ArgumentList @('--silent','--no-registration','--install-dir',('"'+$Target+'"'),'--report',('"'+$report+'"')) -WorkingDirectory $projectRoot -WindowStyle Hidden -PassThru -Wait
    $result = Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
    if ($ExpectedSuccess -and ($process.ExitCode -ne 0 -or $result.status -ne 'passed')) { throw "Installer test failed: $Label" }
    if (-not $ExpectedSuccess -and ($process.ExitCode -eq 0 -or $result.status -ne 'failed')) { throw 'Existing unrelated files were not rejected.' }
}
Run-Setup -Target $installation -Label 'p1-install'
$results.install='passed'
foreach ($relative in $manifest.installedFiles) {
    $actual = (Get-FileHash -LiteralPath (Join-Path $installation $relative) -Algorithm SHA256).Hash
    if ($actual -ne $manifest.fileHashes.$relative) { throw "Installed file hash mismatch: $relative" }
}
$results.installedHashes='passed'
$dataSentinel = Join-Path $installation 'Data\keep-game-data.txt'
New-Item -ItemType Directory -Path (Split-Path $dataSentinel -Parent) -Force | Out-Null
'preserve-game-data' | Set-Content -LiteralPath $dataSentinel -Encoding utf8
$privateConfiguration = Join-Path $installation 'config\credentials.local.json'
'{"microsoftClientId":"","curseforgeApiKey":""}' | Set-Content -LiteralPath $privateConfiguration -Encoding utf8
Run-Setup -Target $installation -Label 'p1-upgrade'
if ((Get-Content -LiteralPath $dataSentinel -Raw).Trim() -ne 'preserve-game-data' -or -not (Test-Path -LiteralPath $privateConfiguration)) { throw 'Upgrade altered user data.' }
$results.upgradePreservesData='passed'
$env:PCL_THIRDPARTY_DATA_ROOT = Join-Path $installation 'Data\sandbox-qa'
$native = Start-Process -FilePath (Join-Path $installation 'PCL老蒋定制版.exe') -ArgumentList '--workspace-verify' -WorkingDirectory $projectRoot -WindowStyle Hidden -PassThru
if (-not $native.WaitForExit(90000)) {
    Get-Process | Where-Object { $_.Path -and $_.Path.StartsWith($installation + '\',[StringComparison]::OrdinalIgnoreCase) } | Stop-Process
    throw 'Installed EXE startup verification timed out.'
}
$nativeReport = Join-Path $env:PCL_THIRDPARTY_DATA_ROOT 'verification.json'
$nativeResult = Get-Content -LiteralPath $nativeReport -Raw | ConvertFrom-Json
if ($native.ExitCode -ne 0 -or $nativeResult.stage -ne 'passed') { throw 'Installed EXE entry did not pass acceptance.' }
Copy-Item -LiteralPath $nativeReport -Destination (Join-Path $projectRoot 'reports\installer-p1-native-entry.json')
foreach ($language in @('zh-CN','en-US')) {
    Copy-Item -LiteralPath (Join-Path $env:PCL_THIRDPARTY_DATA_ROOT ("home-$language.png")) -Destination (Join-Path $projectRoot ("reports\p1-home-$language.png"))
    Copy-Item -LiteralPath (Join-Path $env:PCL_THIRDPARTY_DATA_ROOT ("window-$language.png")) -Destination (Join-Path $projectRoot ("reports\p1-window-$language.png"))
}
$results.nativeEntry='passed'
$protected = Join-Path $testRoot 'existing-launcher'
New-Item -ItemType Directory -Path $protected -Force | Out-Null
$protectedFile = Join-Path $protected 'original-file.txt'
'do-not-change' | Set-Content -LiteralPath $protectedFile -Encoding utf8
Run-Setup -Target $protected -Label 'p1-protect-existing' -ExpectedSuccess $false
if ((Get-Content -LiteralPath $protectedFile -Raw).Trim() -ne 'do-not-change') { throw 'Unrelated existing file was changed.' }
$results.protectsExistingLauncher='passed'
$uninstallReport = Join-Path $projectRoot 'reports\installer-p1-uninstall.json'
if (Test-Path -LiteralPath $uninstallReport) { Remove-Item -LiteralPath $uninstallReport }
$uninstaller = Start-Process -FilePath (Join-Path $installation 'Uninstall.exe') -ArgumentList @('--silent','--report',('"'+$uninstallReport+'"')) -WorkingDirectory $projectRoot -WindowStyle Hidden -PassThru -Wait
for ($attempt=0; $attempt -lt 100 -and -not (Test-Path -LiteralPath $uninstallReport); $attempt++) { Start-Sleep -Milliseconds 100 }
$uninstalled = Get-Content -LiteralPath $uninstallReport -Raw | ConvertFrom-Json
if ($uninstalled.status -ne 'passed') { throw 'Uninstall test failed.' }
foreach ($relative in $manifest.installedFiles) { if (Test-Path -LiteralPath (Join-Path $installation $relative)) { throw "Uninstall left application file: $relative" } }
if (-not (Test-Path -LiteralPath $dataSentinel) -or -not (Test-Path -LiteralPath $privateConfiguration)) { throw 'Uninstall removed user data.' }
$results.uninstallPreservesData='passed'
$results.status='passed'
$results | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $projectRoot 'reports\installer-p1-acceptance.json') -Encoding utf8
$results | ConvertTo-Json -Depth 5
