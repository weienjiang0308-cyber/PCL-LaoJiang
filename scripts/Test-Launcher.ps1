[CmdletBinding()]
param(
    [string]$InstallVersion,
    [ValidateSet('official','mirror')][string]$Source = 'official',
    [string]$FabricVersion,
    [string]$SandboxName = 'verification',
    [string]$DemoInstance,
    [string]$ShaderFile,
    [switch]$VerifyWorld,
    [switch]$Published
)
. "$PSScriptRoot\Enter-ProjectEnvironment.ps1"
if ($SandboxName -notmatch '^[a-zA-Z0-9-]+$') { throw 'Invalid sandbox name.' }
$env:PCL_THIRDPARTY_DATA_ROOT = Join-Path $projectRoot "sandbox\$SandboxName"
$env:PCL_DISABLE_DEBUG_HINT = '1'
$launcher = Join-Path $projectRoot 'src\PCL-ThirdParty\Plain Craft Launcher 2\bin\Debug\net10.0-windows\win-x64\Plain Craft Launcher 2.exe'
if ($Published) { $launcher = Join-Path $projectRoot 'artifacts\launcher\Plain Craft Launcher 2.exe' }
if (-not (Test-Path -LiteralPath $launcher)) { throw 'Build the Debug launcher first.' }
$arguments = @('--workspace-verify')
if ($VerifyWorld) { $arguments += '--verify-world' }
if ($InstallVersion) { $arguments += @('--install-version', $InstallVersion, '--source', $Source) }
if ($FabricVersion) { $arguments += @('--fabric-version', $FabricVersion) }
if ($ShaderFile) {
    if ($ShaderFile -notmatch '^[a-zA-Z0-9_.+-]+\.zip$') { throw 'Use a shader filename without spaces for verification.' }
    $arguments += @('--shader-file', $ShaderFile)
}
if ($DemoInstance) {
    $arguments += @('--demo-instance', $DemoInstance)
    $javaReport = Get-Content -LiteralPath (Join-Path $projectRoot 'reports\java-17.json') -Raw | ConvertFrom-Json
    $env:PCL_THIRDPARTY_JAVA_PATH = $javaReport.javaPath
}
$process = Start-Process -FilePath $launcher -ArgumentList $arguments -WorkingDirectory $projectRoot -WindowStyle Hidden -PassThru
Write-Output "Verification PID: $($process.Id)"
Write-Output "Report: $env:PCL_THIRDPARTY_DATA_ROOT\verification.json"
$process.WaitForExit()
$mode = if ($DemoInstance) { 'demo' } elseif ($InstallVersion) { 'install' } else { 'ui' }
$reportFile = Join-Path $env:PCL_THIRDPARTY_DATA_ROOT 'verification.json'
if (Test-Path -LiteralPath $reportFile) {
    Copy-Item -LiteralPath $reportFile -Destination (Join-Path $projectRoot "reports\acceptance-$SandboxName-$mode.json")
}
if ($process.ExitCode -ne 0) { throw "Launcher verification failed: exit $($process.ExitCode)." }
Get-Content -LiteralPath $reportFile -Raw
