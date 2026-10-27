[CmdletBinding()]
param([switch]$DebugBuild)
. "$PSScriptRoot\Enter-ProjectEnvironment.ps1"
$credentialsFile = Join-Path $projectRoot 'config\credentials.local.json'
if (Test-Path -LiteralPath $credentialsFile) {
    $credentials = Get-Content -LiteralPath $credentialsFile -Raw | ConvertFrom-Json
    if ($credentials.microsoftClientId) { $env:PCL_THIRDPARTY_MS_CLIENT_ID = $credentials.microsoftClientId }
    if ($credentials.curseforgeApiKey) { $env:PCL_THIRDPARTY_CURSEFORGE_API_KEY = $credentials.curseforgeApiKey }
}
$env:PCL_THIRDPARTY_DATA_ROOT = Join-Path $projectRoot 'sandbox\user'
$env:PCL_DISABLE_DEBUG_HINT = '1'
$launcher = if ($DebugBuild) {
    Join-Path $projectRoot 'src\PCL-ThirdParty\Plain Craft Launcher 2\bin\Debug\net10.0-windows\win-x64\Plain Craft Launcher 2.exe'
} else {
    Join-Path $projectRoot 'artifacts\launcher\Plain Craft Launcher 2.exe'
}
if (-not (Test-Path -LiteralPath $launcher)) { throw 'Build the launcher using scripts\Build-Launcher.ps1 -Configuration Release -Publish.' }
Start-Process -FilePath $launcher -WorkingDirectory $projectRoot -WindowStyle Hidden
