[CmdletBinding()]
param(
    [switch]$Baseline,
    [ValidateSet('Debug','Release')][string]$Configuration = 'Debug',
    [switch]$Publish
)
. "$PSScriptRoot\Enter-ProjectEnvironment.ps1"
$dotnet = Join-Path $env:DOTNET_ROOT 'dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnet)) { throw 'Run scripts\Install-DotnetSdk.ps1 first.' }
$source = if ($Baseline) { 'work\upstream\pcl-ce' } else { 'src\PCL-ThirdParty' }
$project = Join-Path $projectRoot "$source\Plain Craft Launcher 2\Plain Craft Launcher 2.csproj"
$label = if ($Baseline) { 'baseline' } else { 'thirdparty' }
$log = Join-Path $projectRoot "reports\build-$label-$Configuration.log"
$arguments = @('build', $project, '-c', $Configuration, '-p:Platform=x64', '--nologo', '-v:minimal')
if ($Publish) {
    $arguments = @('publish', $project, '-c', $Configuration, '-p:Platform=x64', '-r', 'win-x64',
        '--self-contained', 'true', '-p:PublishSingleFile=true', '-o', (Join-Path $projectRoot 'artifacts\launcher'), '--nologo', '-v:minimal')
}
Push-Location $projectRoot
try {
    & $dotnet @arguments *> $log
    $buildExitCode = $LASTEXITCODE
    Get-Content -LiteralPath $log -Tail 14
    if ($buildExitCode -ne 0) { throw "Build failed ($buildExitCode). See $log" }
} finally { Pop-Location }
